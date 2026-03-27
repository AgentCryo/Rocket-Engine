using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using static RERL.Loaders.MaterialLoader;
using static RERL.RERL_Core;

namespace RERL;

public static class RenderData
{
    /// <summary>
    /// Represents a single vertex containing position, normal, and UV coordinates.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public Vector3 Position;   // 12 bytes
        public Vector3 Normal;     // 12 bytes
        public Vector2 UV;         // 8 bytes
        public Vector4 Tangent;    // 16 bytes (xyz + handedness)
        public Vertex(Vector3 position, Vector3 normal, Vector2 uv) 
        {
            Position = position;
            Normal = normal;
            UV = uv;
        }
    }

    /// <summary>
    /// Represents a mesh consisting of vertices and indices.
    /// </summary>
    public struct Mesh(Vertex[] vertices, uint[] indices)
    {
        public Vertex[] Vertices = vertices;
        public uint[] Indices    = indices;
        public int MaterialIndex = -1;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct GPUMaterial
    {
        public Vector4 BaseColor;
        public int albedo;
        //public ulong normalHandle;
        //public ulong ormHandle;
        Vector3 _padding;
    }
    
    public static GPUMaterial ToGpu(Material mat)
    {
        return new GPUMaterial
        {
            BaseColor = new Vector4(mat.BaseAlbedo, 1.0f),
            albedo    = -1
        };
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct DrawElementsIndirectCommand
    {
        public uint Count;
        public uint InstanceCount;
        public uint FirstIndex;
        public uint BaseVertex;
        public uint BaseInstance;
    }

}