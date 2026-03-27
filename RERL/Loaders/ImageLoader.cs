using StbImageSharp;
using OpenTK.Graphics.OpenGL4;
using RCS;

namespace RERL.Loaders
{
	public enum TextureType
	{
		Albedo,
		Normal
	}
	
public static class ImageLoader
{
    private static readonly Dictionary<string, int> _cache = new();

    public static int LoadTexture(string imagePath, TextureType type)
    {
        if (_cache.TryGetValue(imagePath, out var cachedHandle))
            return cachedHandle;

        try
        {
            using var stream = File.OpenRead(imagePath);
            StbImage.stbi_set_flip_vertically_on_load(1);
            var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            if (image == null) {
                Logger.Warning($"Failed to decode image: {imagePath}");
                return -1;
            }

            int internalFormat = type switch
            {
                TextureType.Normal => (int)PixelInternalFormat.Rgba,
                _                   => (int)PixelInternalFormat.SrgbAlpha
            };

            int textureHandle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, textureHandle);

            GL.TexImage2D(TextureTarget.Texture2D, 0, (PixelInternalFormat)internalFormat,
                image.Width, image.Height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy, out float maxAniso);
            GL.TexParameter(TextureTarget.Texture2D, (TextureParameterName)All.TextureMaxAnisotropyExt, MathF.Min(16f, maxAniso));
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            Console.WriteLine($"Loaded {imagePath} → {image.Width}x{image.Height}");

            _cache[imagePath] = textureHandle;
            return textureHandle;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to load texture: {ex.Message}");
            return -1;
        }
    }
}
}