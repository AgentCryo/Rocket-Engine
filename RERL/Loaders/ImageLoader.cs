using System;
using System.Collections.Generic;
using System.IO;
using RCS;
using StbImageSharp;
using OpenTK.Graphics.OpenGL4;
using System.Diagnostics;
using System.Text.Json;
using Buffer = System.Buffer;

namespace RERL.Loaders;

public static class ImageLoader
{
    static readonly Dictionary<string, int> _textureCache   = new();
    static readonly Dictionary<int, ulong>  _bindlessCache  = new();

    public sealed class TextureMeta
    {
        public string SourcePath { get; set; } = "";
        public long   SourceSize { get; set; }
        public long   SourceTimestampTicks { get; set; }
        public string DdsPath { get; set; } = "";
        public bool   IsLinear { get; set; }
    }
    
    static readonly string CacheRoot = Path.GetFullPath("EngineCache/Textures").Replace('\\', '/');

    static string ComputeKey(string sourcePath, FileInfo info, bool isLinear)
    {
        // isLinear is part of the cache key - the same source file loaded once
        // as color data and once as linear data (e.g. reused as both an albedo
        // and, unusually, a normal map) must produce two distinct cached DDS
        // files, since the GPU internal format differs.
        return $"{sourcePath}:{info.LastWriteTimeUtc.Ticks}:{info.Length}:{(isLinear ? "linear" : "srgb")}";
    }

    static string SHA1(string text)
    {
        using var sha = System.Security.Cryptography.SHA1.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var hash  = sha.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    static (string ddsPath, string metaPath) GetCachePaths(string hash)
    {
        Directory.CreateDirectory(CacheRoot);
        string dds  = Path.Combine(CacheRoot, $"{hash}.dds").Replace('\\', '/');
        string meta = Path.Combine(CacheRoot, $"{hash}.meta").Replace('\\', '/');
        return (dds, meta);
    }

    /// <summary>
    /// Loads a texture from disk. <paramref name="isLinear"/> must be true for
    /// data textures (normal maps, roughness/metallic, etc.) and false for
    /// color textures (albedo/base color) - it controls whether the texture is
    /// uploaded/converted as sRGB (gamma-decoded on sample) or linear. Getting
    /// this wrong silently corrupts the sampled values: sRGB-decoding a normal
    /// map distorts its XYZ components, and skipping sRGB decode on an albedo
    /// map washes out its colors.
    /// </summary>
    public static int LoadTexture(string path, bool isLinear = false)
    {
        path = Path.GetFullPath(path).Replace('\\', '/');

        // Cache key includes isLinear (via the internal cache dictionary key
        // below) so the same path loaded both ways doesn't collide.
        string cacheKey = isLinear ? $"{path}:linear" : path;

        if (_textureCache.TryGetValue(cacheKey, out int cachedTex))
        {
            Logger.Log($"Reusing cached texture: {cachedTex}: {path} (linear={isLinear})");
            LogBindlessReuse(cachedTex);
            return cachedTex;
        }

        string ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext == ".dds")
        {
            int texFromDds = LoadDdsTexture(path, isLinear);
            return RegisterTexture(cacheKey, texFromDds);
        }

        if (ext is ".png" or ".jpg" or ".jpeg")
        {
            var info = new FileInfo(path);
            var key  = ComputeKey(path, info, isLinear);
            var hash = SHA1(key);
            var (ddsPath, metaPath) = GetCachePaths(hash);

            if (File.Exists(ddsPath) && File.Exists(metaPath))
            {
                Logger.Log($"Using cached DDS for {path} -> {ddsPath} (linear={isLinear})");
                int texFromDds = LoadDdsTexture(ddsPath, isLinear);
                return RegisterTexture(cacheKey, texFromDds);
            }

            if (TryConvertToDds(path, ddsPath, isLinear))
            {
                SaveMeta(metaPath, new TextureMeta
                {
                    SourcePath = path,
                    SourceSize = info.Length,
                    SourceTimestampTicks = info.LastWriteTimeUtc.Ticks,
                    DdsPath = ddsPath,
                    IsLinear = isLinear
                });

                int texFromDds = LoadDdsTexture(ddsPath, isLinear);
                return RegisterTexture(cacheKey, texFromDds);
            }

            Logger.Warning($"DDS conversion failed for {path}, falling back to direct upload.");
            int texFallback = LoadUncompressedTexture(path, isLinear);
            return RegisterTexture(cacheKey, texFallback);
        }

        int tex = LoadUncompressedTexture(path, isLinear);
        return RegisterTexture(cacheKey, tex);
    }

    static int LoadUncompressedTexture(string path, bool isLinear)
    {
        using var stream = File.OpenRead(path);
        StbImage.stbi_set_flip_vertically_on_load(1);

        var img = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        if (img == null)
        {
            Logger.Warning($"Failed to decode image: {path}");
            return 0;
        }
        
        int tex = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, tex);

        Logger.Log($"Loading texture (uncompressed, {(isLinear ? "linear" : "sRGB")}): {tex}: {path}");

        GL.TexImage2D(
            TextureTarget.Texture2D,
            level: 0,
            internalformat: isLinear ? PixelInternalFormat.Rgba8 : PixelInternalFormat.Srgb8Alpha8,
            width: img.Width,
            height: img.Height,
            border: 0,
            format: PixelFormat.Rgba,
            type: PixelType.UnsignedByte,
            pixels: img.Data
        );

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear); //8ms with texture
        //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear); // 14ms with texture.
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

        return tex;
    }

    static TextureMeta LoadMeta(string metaPath)
    {
        string json = File.ReadAllText(metaPath);
        return JsonSerializer.Deserialize<TextureMeta>(json)!;
    }

    static bool TryConvertToDds(string sourcePath, string ddsOutPath, bool isLinear)
    {
        try
        {
            string cacheDir = Path.GetDirectoryName(ddsOutPath)!;
            Directory.CreateDirectory(cacheDir);

            string texconvPath = Path.Combine(AppContext.BaseDirectory, "RocketEngine/Tools", "texconv.exe");
            // -srgb:i only applies for color data - normal/data maps must NOT
            // be tagged sRGB, or the DDS's stored format will trigger gamma
            // decode on sample regardless of what internal format we later
            // request when uploading.
            string srgbFlag = isLinear ? "" : "-srgb:i ";
            string args = $"-f R8G8B8A8_UNORM {srgbFlag}-nologo -m 1 -o \"{cacheDir}\" \"{sourcePath}\"";

            var psi = new ProcessStartInfo {
                FileName = texconvPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi)!;
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            Logger.Log($"texconv stdout: {stdout}");
            Logger.Log($"texconv stderr: {stderr}");

            if (proc.ExitCode != 0) {
                Logger.Warning($"texconv failed for {sourcePath}: {proc.StandardError.ReadToEnd()}");
                return false;
            }

            string produced = Path.Combine(cacheDir, Path.GetFileNameWithoutExtension(sourcePath) + ".dds");

            if (!File.Exists(produced)) {
                Logger.Warning($"texconv did not produce expected file: {produced}");
                return false;
            }

            if (produced.Equals(ddsOutPath, StringComparison.OrdinalIgnoreCase)) return true;
            
            if (File.Exists(ddsOutPath)) File.Delete(ddsOutPath);

            File.Move(produced, ddsOutPath);

            return true;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Exception during DDS conversion for {sourcePath}: {ex}");
            return false;
        }
    }

    static void SaveMeta(string metaPath, TextureMeta meta)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(meta, options);
        File.WriteAllText(metaPath, json);
    }

    struct DDSHeaderDXT10
    {
        public uint DxgiFormat;
        public uint ResourceDimension;
        public uint MiscFlag;
        public uint ArraySize;
        public uint MiscFlags2;
    }
    
    struct DDSPixelFormat
    {
        public uint Size;
        public uint Flags;
        public uint FourCC;
        public uint RGBBitCount;
        public uint RBitMask;
        public uint GBitMask;
        public uint BBitMask;
        public uint ABitMask;
    }
    struct DDSHeader
    {
        public uint Size;
        public uint Flags;
        public uint Height;
        public uint Width;
        public uint PitchOrLinearSize;
        public uint Depth;
        public uint MipMapCount;

        // skip 11 reserved uints
        public DDSPixelFormat PixelFormat;
        public uint Caps;
        public uint Caps2;
        public uint Caps3;
        public uint Caps4;
        public uint Reserved2;
    }
    
    /// <summary>
    /// Maps a DXGI format to a GL internal format. <paramref name="isLinear"/>
    /// forces the non-sRGB variant regardless of what texconv actually wrote,
    /// since -srgb:i is skipped for linear textures during conversion but the
    /// DXGI format code texconv emits for R8G8B8A8_UNORM is the same either
    /// way - the sRGB-ness only shows up in whether texconv chose the _SRGB
    /// DXGI value at all, and we've already ensured it won't for linear data.
    /// </summary>
    static PixelInternalFormat DxgiToGL(uint dxgi, bool isLinear) =>
        dxgi switch
        {
            28 => PixelInternalFormat.Rgba8,                                                     // DXGI_FORMAT_R8G8B8A8_UNORM
            29 => isLinear ? PixelInternalFormat.Rgba8 : PixelInternalFormat.Srgb8Alpha8,         // DXGI_FORMAT_R8G8B8A8_UNORM_SRGB

            98 => isLinear ? PixelInternalFormat.CompressedRgbaBptcUnorm : PixelInternalFormat.CompressedRgbaBptcUnorm,        // BC7 linear
            99 => isLinear ? PixelInternalFormat.CompressedRgbaBptcUnorm : PixelInternalFormat.CompressedSrgbAlphaBptcUnorm,   // BC7 sRGB

            95 => PixelInternalFormat.CompressedRgbBptcSignedFloat,   // BC6H signed
            96 => PixelInternalFormat.CompressedRgbBptcUnsignedFloat, // BC6H unsigned

            71 => PixelInternalFormat.CompressedRgbaS3tcDxt1Ext,                                                                 // BC1 linear
            72 => isLinear ? PixelInternalFormat.CompressedRgbaS3tcDxt1Ext : PixelInternalFormat.CompressedSrgbS3tcDxt1Ext,      // BC1 sRGB

            77 => PixelInternalFormat.CompressedRgbaS3tcDxt5Ext,                                                                 // BC3 linear
            78 => isLinear ? PixelInternalFormat.CompressedRgbaS3tcDxt5Ext : PixelInternalFormat.CompressedSrgbAlphaS3tcDxt5Ext, // BC3 sRGB

            83 => PixelInternalFormat.CompressedRgRgtc2,              // BC5 (normal maps typically land here - always linear, no sRGB variant exists)

            _ => throw new NotSupportedException($"DXGI format {dxgi} not supported")
        };

    static int LoadDdsTexture(string ddsPath, bool isLinear)
    {
        using var fs = File.OpenRead(ddsPath);
        using var br = new BinaryReader(fs);

        if (br.ReadUInt32() != 0x20534444) Logger.Error("Not a DDS file");

        DDSHeader header = new DDSHeader {
            Size = br.ReadUInt32(),
            Flags = br.ReadUInt32(),
            Height = br.ReadUInt32(),
            Width = br.ReadUInt32(),
            PitchOrLinearSize = br.ReadUInt32(),
            Depth = br.ReadUInt32(),
            MipMapCount = br.ReadUInt32()
        };

        br.ReadBytes(11 * 4);

        DDSPixelFormat pf = new DDSPixelFormat {
            Size = br.ReadUInt32(),
            Flags = br.ReadUInt32(),
            FourCC = br.ReadUInt32(),
            RGBBitCount = br.ReadUInt32(),
            RBitMask = br.ReadUInt32(),
            GBitMask = br.ReadUInt32(),
            BBitMask = br.ReadUInt32(),
            ABitMask = br.ReadUInt32()
        };

        header.PixelFormat = pf;
        
        header.Caps = br.ReadUInt32();
        header.Caps2 = br.ReadUInt32();
        header.Caps3 = br.ReadUInt32();
        header.Caps4 = br.ReadUInt32();
        header.Reserved2 = br.ReadUInt32();

        bool hasDx10 = (pf.FourCC == 0x30315844); // 'DX10'

        PixelInternalFormat internalFormat;
        if (hasDx10) {
            DDSHeaderDXT10 dx10 = new DDSHeaderDXT10
            {
                DxgiFormat = br.ReadUInt32(),
                ResourceDimension = br.ReadUInt32(),
                MiscFlag = br.ReadUInt32(),
                ArraySize = br.ReadUInt32(),
                MiscFlags2 = br.ReadUInt32()
            };

            internalFormat = DxgiToGL(dx10.DxgiFormat, isLinear);
        } else internalFormat = isLinear ? PixelInternalFormat.Rgba8 : PixelInternalFormat.Srgb8Alpha8;

        int width = (int)header.Width;
        int height = (int)header.Height;

        int dataSize = width * height * 4;
        byte[] data = br.ReadBytes(dataSize);

        byte[] flipped = new byte[dataSize];
        int rowBytes = width * 4;

        for (int row = 0; row < height; row++) {
            int src = row * rowBytes;
            int dst = (height - 1 - row) * rowBytes;
            Buffer.BlockCopy(data, src, flipped, dst, rowBytes);
        }

        int tex = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, tex);
        
        GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, flipped);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

        return tex;
    }

    static int RegisterTexture(string cacheKey, int tex)
    {
        if (tex == 0)
            return 0;

        _textureCache[cacheKey] = tex;

        ulong handle = (ulong)GL.Arb.GetTextureHandle(tex);
        GL.Arb.MakeTextureHandleResident(handle);
        _bindlessCache[tex] = handle;

        return tex;
    }
    
    public static ulong GetBindlessHandle(int texId)
    {
        if (_bindlessCache.TryGetValue(texId, out ulong handle)) {
            Logger.Log($"[Bindless] Reusing handle for tex {texId}: 0x{handle:X16}");
            return handle;
        }

        Logger.Warning($"[Bindless] Handle miss for tex {texId}, creating on demand.");

        ulong newHandle = (ulong)GL.Arb.GetTextureHandle(texId);
        GL.Arb.MakeTextureHandleResident(newHandle);
        _bindlessCache[texId] = newHandle;

        LogBindlessCreate(texId, newHandle);

        return newHandle;
    }

    public static void DisposeTexture(int texId)
    {
        if (_bindlessCache.TryGetValue(texId, out ulong handle)) {
            Logger.Log($"[Bindless] Making handle non-resident for tex {texId}: 0x{handle:X16}");
            GL.Arb.MakeTextureHandleNonResident(handle);
            _bindlessCache.Remove(texId);
        }

        if (texId != 0) {
            Logger.Log($"Deleting texture {texId}");
            GL.DeleteTexture(texId);
        }
    }

    static void LogBindlessCreate(int texId, ulong handle)
    {
        uint lo = (uint)(handle & 0xFFFFFFFF);
        uint hi = (uint)(handle >> 32);

        Logger.Log($"[Bindless] Texture {texId}");
        Logger.Log($"[Bindless]   Handle (ulong): {handle}");
        Logger.Log($"[Bindless]   Handle (hex): 0x{handle:X16}");
        Logger.Log($"[Bindless]   Lo: {lo} (0x{lo:X8})");
        Logger.Log($"[Bindless]   Hi: {hi} (0x{hi:X8})");
    }

    static void LogBindlessReuse(int texId) =>
        Logger.Log(_bindlessCache.TryGetValue(texId, out ulong handle)
            ? $"[Bindless] Reusing handle for tex {texId}: 0x{handle:X16}"
            : $"[Bindless] No cached handle for tex {texId} (yet).");
}