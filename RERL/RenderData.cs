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
    public struct Vertex {
        public Vector3 Position;   // 12 bytes
        public Vector3 Normal;     // 12 bytes
        public Vector2 UV;         // 8 bytes
        public Vector4 Tangent;    // 16 bytes (xyz + handedness)
        public Vertex(Vector3 position, Vector3 normal, Vector2 uv) {
            Position = position;
            Normal = normal;
            UV = uv;
        }
    }

    /// <summary>
    /// Represents a mesh consisting of vertices and indices.
    /// </summary>
    public struct Mesh(Vertex[] vertices, uint[] indices) {
        public Vertex[] Vertices = vertices;
        public uint[] Indices    = indices;
        public int MaterialIndex = -1;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct GPUMaterial {
        public Vector4 BaseColor;    // 16
        public uint AlbedoHandleLo;  // 4
        public uint AlbedoHandleHi;  // 4
        public Vector2 Padding;      // 8 -> total 32
    }

    public static GPUMaterial ToGpu(Material mat) {
        ulong h = mat.AlbedoHandle;
        return new GPUMaterial {
            BaseColor    = new Vector4(mat.BaseAlbedo, 1.0f),
            AlbedoHandleLo  = (uint)(h & 0xFFFFFFFF),
            AlbedoHandleHi  = (uint)(h >> 32),
        };
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct DrawElementsIndirectCommand {
        public uint Count;
        public uint InstanceCount;
        public uint FirstIndex;
        public uint BaseVertex;
        public uint BaseInstance;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public struct Cluster {
        Vector3 minPoint;
        uint   _pad0;
        Vector3 maxPoint;
        uint   _pad1;
        uint[]  LightIndices;
    }
    
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public struct Light {
        public uint    Data;         
        // Data bit layout (uint32):
        //     [ 1:0  ] LightType
        //               00 = Point
        //               01 = Spot
        //               10 = Directional
        //     [ 2    ] SmoothEdgeClamping
        //     [ 3    ] GlobalLight (1 = global, 0 = clustered)
        //     [ 31:4 ]  Reserved
               uint   _pad0;
               uint   _pad1;
               uint   _pad2;
        public Vector3 Color;
        public float   Intensity;
        public Vector3 Position;
        public float   Radius;
        public Vector3 Direction;
        public float   Angle;
    }
}