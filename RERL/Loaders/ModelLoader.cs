using System.Text;
using System.Text.Json;
using OpenTK.Mathematics;
using RCS;
using RCS.Components;
using static RERL.Loaders.MaterialLoader;
using static RERL.RenderData;
using static RERL.RERL_Core;

namespace RERL.Loaders;

/// <summary>
/// Provides functionality for loading mesh data from OBJ files and converting
/// them into <see cref="RenderData.Mesh"/> instances. Includes built‑in paths
/// for common primitive meshes.
/// </summary>
public static class ModelLoader
{
    public class Model(Mesh[] subMeshes)
    {
        public Mesh[] SubMeshes = subMeshes;
    }
    
    public record ModelReturn
    {
        public string Name;
        public Model? Model;
        public Transform Transform;
        public string ParrentName;
    }
    
    public const string Cube = @"./RocketEngine/Models/Cube.glb";
    public const string Icosahedron = @"./RocketEngine/Models/Icosahedron.glb";
    public const string UVSphere = @"./RocketEngine/Models/UVSphere.glb";
    
    public static Model CubeMesh => ParseMesh(Cube)[0].Model;
    public static Model IcosahedronMesh => ParseMesh(Icosahedron)[0].Model;
    public static Model UVSphereMesh => ParseMesh(UVSphere)[0].Model;

    /// <summary>
    /// Parses a mesh file based on its extension.
    /// Currently, supports only OBJ files.
    /// </summary>
    /// <param name="filename">The file path to load.</param>
    /// <returns>A new <see cref="RenderData.Mesh"/> instance.</returns>
    /// <exception cref="Exception">Thrown if the file format is unsupported.</exception>
    public static List<ModelReturn> ParseMesh(string filename)
    {
        if(filename.EndsWith(".glb") || filename.EndsWith(".gltf"))
            return ParseGltf(filename, filename.EndsWith(".glb"));

        throw new Exception($"ERR: Unsupported file format '{filename}'.");
    }

    static readonly string[] AlbedoEndings = ["_n", "_normal", "_ddn", "_nrm"];
    static readonly string[] NormalEndings = ["_albedo", "_diff", "_diffuse", "_col", "_color", "_basecolor", "_colour", "_basecolour"];

    struct AccessorView<T> where T : unmanaged
    {
        public byte[] Buffer;
        public int Offset;
        public int Count;
        public int Stride;
    }
    
    static Dictionary<int, AccessorView<byte>> BuildAccessorViews(JsonElement accessors, JsonElement bufferViews, Dictionary<uint, byte[]> buffers) {
        var dict = new Dictionary<int, AccessorView<byte>>();

        for (int i = 0; i < accessors.GetArrayLength(); i++) {
            var acc = accessors[i];
            int viewIndex = acc.GetProperty("bufferView").GetInt32();
            var view = bufferViews[viewIndex];

            uint bufferIndex = view.GetProperty("buffer").GetUInt32();
            byte[] buffer = buffers[bufferIndex];

            int offset = view.TryGetProperty("byteOffset", out var off) ? off.GetInt32() : 0;
            offset += acc.TryGetProperty("byteOffset", out var aoff) ? aoff.GetInt32() : 0;

            int count = acc.GetProperty("count").GetInt32();
            int stride = view.TryGetProperty("byteStride", out var s) ? s.GetInt32() : 0;

            dict[i] = new AccessorView<byte>
            {
                Buffer = buffer,
                Offset = offset,
                Count = count,
                Stride = stride
            };
        }

        return dict;
    }
    
    static readonly Dictionary<int, int> MaterialCache = new();

    static Mesh BuildMesh(JsonElement primitive, Dictionary<int, AccessorView<byte>> accessors, JsonElement root, string filePath) {
        var attrs = primitive.GetProperty("attributes");

        int posIndex = attrs.GetProperty("POSITION").GetInt32();
        int idxIndex = primitive.GetProperty("indices").GetInt32();
        int nrmIndex = attrs.TryGetProperty("NORMAL", out var nrmProp) ? nrmProp.GetInt32() : -1;
        int uvIndex = attrs.TryGetProperty("TEXCOORD_0", out var uvProp) ? uvProp.GetInt32() : -1;

        var posView = accessors[posIndex];
        var idxView = accessors[idxIndex];
        var nrmView = accessors[nrmIndex];
        var uvView  = accessors[uvIndex];

        int vCount = posView.Count;
        int iCount = idxView.Count;

        var idxAccessor = root.GetProperty("accessors")[idxIndex];
        string type = idxAccessor.GetProperty("type").GetString();

        if (type != "SCALAR")
            throw new Exception($"Index accessor at {idxIndex} is not SCALAR (got {type})");

        uint componentType = idxAccessor.GetProperty("componentType").GetUInt32();
        
        // Allocate ONLY the final arrays (this is what your renderer needs)
        Vertex[] vertices = new Vertex[vCount];
        uint[] indices = new uint[iCount];

        // Fill vertices
        for (int i = 0; i < vCount; i++)
        {
            ReadVec3(posView, i, out float px, out float py, out float pz);
            
            float nx, ny, nz;
            if (nrmIndex >= 0) ReadVec3(nrmView, i, out nx, out ny, out nz);
            else nx = ny = nz = 0;

            float u, v;
            if (uvIndex >= 0) ReadVec2(uvView, i, out u, out v);
            else u = v = 0;

            vertices[i] = new Vertex(
                new Vector3(px, py, -pz),
                new Vector3(nx, ny, -nz),
                new Vector2(u, 1 - v)
            );
        }

        // Fill indices
        for (int i = 0; i < iCount; i++)
            indices[i] = componentType switch
            {
                5121 => // UNSIGNED_BYTE
                    ReadByte(idxView, i),
                5123 => // UNSIGNED_SHORT
                    ReadUShort(idxView, i),
                5125 => // UNSIGNED_INT
                    ReadUInt(idxView, i),
                _ => throw new Exception($"Unsupported index type {componentType}")
            };

        // Material
        if (!primitive.TryGetProperty("material", out var matElem)) return new Mesh(vertices, indices) { MaterialIndex = RenderPipeline.RegisterMaterial(DefaultMaterial) };
        int gltfMatIndex = matElem.GetInt32();

        if (MaterialCache.TryGetValue(gltfMatIndex, out var matIndex))
        {
            //Logger.Log($"Reusing material {matIndex} for glTF material {gltfMatIndex}");
        } else {
            var mat = LoadGltfMaterial(root.GetProperty("materials")[gltfMatIndex], root, filePath);
            matIndex = RenderPipeline.RegisterMaterial(mat);

            MaterialCache[gltfMatIndex] = matIndex;

            Logger.Log($"Loaded new material {matIndex} for glTF material {gltfMatIndex}");
        }

        return new Mesh(vertices, indices) { MaterialIndex = matIndex };
    }
    
    public static List<ModelReturn> ParseGltf(string filePath, bool isGlb)
    {
        Logger.Log("ParseGltf CALLED");
        MaterialCache.Clear();

        var (json, buffers) = isGlb ? ExtractFromGlb() : ExtractFromGltf();
        if (json == null) return [];

        var root = json.RootElement;

        var accessors = BuildAccessorViews(
            root.GetProperty("accessors"),
            root.GetProperty("bufferViews"),
            buffers
        );

        var nodes = root.GetProperty("nodes").EnumerateArray().ToArray();
        var parentOf = Enumerable.Range(0, nodes.Length).ToDictionary(i => i, _ => (int?)null);

        for (int p = 0; p < nodes.Length; p++)
        {
            if (!nodes[p].TryGetProperty("children", out var children)) continue;
            foreach (var c in children.EnumerateArray())
                parentOf[c.GetInt32()] = p;
        }
        
        List<ModelReturn> results = [];

        foreach (var node in root.GetProperty("nodes").EnumerateArray())
        {
            string name = node.GetProperty("name").GetString() ?? "";
            
            int nodeIndex = Array.FindIndex(nodes, n => n.GetProperty("name").GetString() == name);
            int? parentIndex = parentOf[nodeIndex];
            string parentName = parentIndex.HasValue ? nodes[parentIndex.Value].GetProperty("name").GetString() ?? "" : "";

            #region Transform

            Transform entityTransform;

            if (node.TryGetProperty("matrix", out var mElem))
            {
                var mGltf = ReadMatrix(mElem);
                DecomposeTRS(ConvertGltfMatrixToLH(mGltf), out var t, out var r, out var s);
                entityTransform = new Transform(t, r, s);
            }
            else
            {
                var t = node.TryGetProperty("translation", out var tElem) ? ReadVector3(tElem) : Vector3.Zero;
                var r = node.TryGetProperty("rotation", out var rElem) ? ReadQuaternion(rElem) : Quaternion.Identity;
                var s = node.TryGetProperty("scale", out var sElem) ? ReadVector3(sElem) : Vector3.One;

                var mGltf =
                    Matrix4.CreateScale(s) *
                    Matrix4.CreateFromQuaternion(r) *
                    Matrix4.CreateTranslation(t);

                DecomposeTRS(ConvertGltfMatrixToLH(mGltf), out t, out r, out s);
                entityTransform = new Transform(t, r, s);
            }

            #endregion

            if (!node.TryGetProperty("mesh", out var meshElem))
            {
                results.Add(new ModelReturn {
                    Name = name,
                    Model = null,
                    Transform = entityTransform,
                    ParrentName = parentName
                });
                continue;
            }

            var gltfMesh = root.GetProperty("meshes")[meshElem.GetInt32()];
            List<Mesh> subMeshes = [];

            foreach (var prim in gltfMesh.GetProperty("primitives").EnumerateArray())
                subMeshes.Add(BuildMesh(prim, accessors, root, filePath));

            results.Add(new ModelReturn {
                Name = name,
                Model = new Model(subMeshes.ToArray()),
                Transform = entityTransform,
                ParrentName = parentName
            });
        }

        accessors.Clear();
        buffers.Clear();
        json.Dispose();
        return results;
        
        (JsonDocument? json, Dictionary<uint, byte[]>) ExtractFromGltf()
        {
            string jsonText = File.ReadAllText(filePath);

            var doc = JsonDocument.Parse(jsonText);
            var jsonRoot = doc.RootElement;

            Dictionary<uint, byte[]> buffers = new();
            uint currentBuffer = 0;
            foreach (var buffer in jsonRoot.GetProperty("buffers").EnumerateArray()) {
                string binUri = buffer.GetProperty("uri").GetString() ?? "";
                if (string.IsNullOrEmpty(binUri)) {
                    Logger.Error($"Missing buffer URI in glTF file: {filePath}", false);
                    return (null, []);
                }

                // ReSharper disable once NullableWarningSuppressionIsUsed
                string binPath = Path.Combine(Path.GetDirectoryName(filePath)!, binUri);
                byte[] binData = File.ReadAllBytes(binPath);
                buffers.Add(currentBuffer, binData);
                currentBuffer++;
            }

            return (doc, buffers);
        }

        (JsonDocument? json, Dictionary<uint, byte[]> bin) ExtractFromGlb()
        {
            using var reader = new BinaryReader(File.Open(filePath, FileMode.Open));

            #region Header

            uint magic = reader.ReadUInt32(); // should be 0x46546C67
            if (magic != 0x46546C67) {
                Logger.Error($"Wrong \"magic\" variable in file: {filePath}", throwException: false);
                return (null, []);
            }

            uint version = reader.ReadUInt32(); // should be v2.0
            if (version != 2) {
                Logger.Error($"Unsupported glb version in file: {filePath}", throwException: false);
                return (null, []);
            }

            uint length = reader.ReadUInt32(); // total file length

            #endregion

            #region Json

            uint jsonChunkLength = reader.ReadUInt32();
            uint jsonChunkType = reader.ReadUInt32(); // should be 0x4E4F534A ("JSON")
            if (jsonChunkType != 0x4E4F534A) {
                Logger.Error($"Can't find glb JSON in file: {filePath}", throwException: false);
                return (null, []);
            }

            string jsonText = Encoding.UTF8.GetString(reader.ReadBytes((int)jsonChunkLength));
            JsonDocument doc = JsonDocument.Parse(jsonText);

            #endregion

            #region Binary Buffers

            uint binChunkLength = reader.ReadUInt32();
            uint binChunkType = reader.ReadUInt32(); // should be 0x004E4942 ("BIN")
            if (binChunkType != 0x004E4942) {
                Logger.Error($"Can't find glb BIN in file: {filePath}", throwException: false);
                return (null, []);
            }

            byte[] binData = reader.ReadBytes((int)binChunkLength);

            #endregion

            Dictionary<uint, byte[]> buffer = [];
            buffer.Add(0, binData);
            return (doc, buffer);
        }
    }
    
    #region JSON Readers

    static Matrix4 ReadMatrix(JsonElement matrixElement)
    {
        Span<float> m = stackalloc float[16];
        int i = 0;
        
        foreach (var v in matrixElement.EnumerateArray())
            m[i++] = v.GetSingle();
        
        return new Matrix4(
            m[0],  m[1],  m[2],  m[3],
            m[4],  m[5],  m[6],  m[7],
            m[8],  m[9],  m[10], m[11],
            m[12], m[13], m[14], m[15]
        );
    }

    static Vector3 ReadVector3(JsonElement vector3Element)
    {
        float x = vector3Element[0].GetSingle();
        float y = vector3Element[1].GetSingle();
        float z = vector3Element[2].GetSingle();
        return new Vector3(x, y, z);
    }
    
    static Quaternion ReadQuaternion(JsonElement e)
    {
        var x = e[0].GetSingle();
        var y = e[1].GetSingle();
        var z = e[2].GetSingle();
        var w = e[3].GetSingle();
        return new Quaternion(x, y, z, w);
    }

    #endregion

    #region Binary Readers
    
    static unsafe void ReadVec3(AccessorView<byte> view, int index, out float x, out float y, out float z)
    {
        int stride = view.Stride == 0 ? sizeof(float) * 3 : view.Stride;
        fixed (byte* ptr = &view.Buffer[view.Offset + index * stride])
        {
            float* f = (float*)ptr;
            x = f[0];
            y = f[1];
            z = f[2];
        }
    }

    static unsafe void ReadVec2(AccessorView<byte> view, int index, out float x, out float y)
    {
        int stride = view.Stride == 0 ? sizeof(float) * 2 : view.Stride;
        fixed (byte* ptr = &view.Buffer[view.Offset + index * stride])
        {
            float* f = (float*)ptr;
            x = f[0];
            y = f[1];
        }
    }

    static unsafe uint ReadUInt(AccessorView<byte> view, int index)
    {
        int stride = view.Stride != 0 ? view.Stride : sizeof(uint);
        fixed (byte* ptr = &view.Buffer[view.Offset + index * stride])
            return *(uint*)ptr;
    }

    static unsafe ushort ReadUShort(AccessorView<byte> view, int index)
    {
        int stride = view.Stride != 0 ? view.Stride : sizeof(ushort);
        fixed (byte* ptr = &view.Buffer[view.Offset + index * stride])
            return *(ushort*)ptr;
    }

    static unsafe byte ReadByte(AccessorView<byte> view, int index)
    {
        int stride = view.Stride != 0 ? view.Stride : sizeof(byte);
        return view.Buffer[view.Offset + index * stride];
    }
    
    #endregion
    
    static unsafe T[] ReadFromByteArray<T>(byte[] buffer, (uint offset, uint count, uint stride) byteArea) where T : unmanaged
    {
        int elementSize = sizeof(T);
        T[] result = new T[byteArea.count];
        
        fixed (byte* src = &buffer[byteArea.offset])
        fixed (T* dst = result) {
            for (uint i = 0; i < byteArea.count; i++)
            {
                byte* elementPtr = src + i * (byteArea.stride == 0 ? elementSize : byteArea.stride);
                Buffer.MemoryCopy(elementPtr, dst + i, elementSize, elementSize);
            }
        }

        return result;
    }
    
    public static Vertex[] BuildVertices(Vector3[] positions, Vector3[] normals, Vector2[] texCoords)
    {
        int count = positions.Length;
        Vertex[] verts = new Vertex[count];

        for (int i = 0; i < count; i++)
        {
            verts[i] = new Vertex(
                positions[i],
                normals != null && i < normals.Length ? normals[i] : Vector3.Zero,
                texCoords[i]
            );
        }

        return verts;
    }
    
    public static void DecomposeTRS(Matrix4 m, out Vector3 translation, out Quaternion rotation, out Vector3 scale)
    {
        translation = new Vector3(m.M41, m.M42, m.M43);

        var x = new Vector3(m.M11, m.M21, m.M31);
        var y = new Vector3(m.M12, m.M22, m.M32);
        var z = new Vector3(m.M13, m.M23, m.M33);
        
        var sx = x.Length;
        var sy = y.Length;
        var sz = z.Length;

        var det = Vector3.Dot(x, Vector3.Cross(y, z));
        if (det < 0)
            sx = -sx;

        scale = new Vector3(sx, sy, sz);

        var rotMat = new Matrix3(
            x.X / sx, x.Y / sx, x.Z / sx,
            y.X / sy, y.Y / sy, y.Z / sy,
            z.X / sz, z.Y / sz, z.Z / sz
        );

        rotation = Quaternion.FromMatrix(rotMat);
    }
    
    static Matrix4 ConvertGltfMatrixToLH(Matrix4 m)
    {
        Matrix4 S = Matrix4.CreateScale(1, 1, -1);
        return S * m * S;
    }
}
