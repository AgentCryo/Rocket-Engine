using OpenTK.Mathematics;
using System.Drawing;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using RERL.Loaders;
using RERL.Objects;

namespace RERL;

public static class RERL_Core
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex(Vector3 position, Vector3 normal, Vector2 uv)
    {
        public Vector3 Position = position;
        public Vector3 Normal = normal;
        public Vector2 UV = uv;
    }

    public struct Mesh(Vertex[] vertices, uint[] indices)
    {
        public Vertex[] Vertices = vertices;
        public uint[] Indices = indices;
    }

    public struct RenderTransform
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        
        public static RenderTransform Identity =>
            new RenderTransform(Vector3.Zero, Quaternion.Identity, Vector3.One);
        
        public RenderTransform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public RenderTransform(Vector3 position)
            : this(position, Quaternion.Identity, Vector3.One) { }

        public RenderTransform(Quaternion rotation)
            : this(Vector3.Zero, rotation, Vector3.One) { }

        public RenderTransform(Vector3 position, Quaternion rotation)
            : this(position, rotation, Vector3.One) { }
    }

    #region Temp
    
    static Shader? _tempShader;

    static float _time;

    static MeshRender _meshObject = new();
    static MeshRender _icosahedron = new();
    
    #endregion
    
    public static void Load()
    {
        var assembly = AppDomain.CurrentDomain .GetAssemblies() .FirstOrDefault(a => a.GetName().Name == "RERL");

        Console.WriteLine(assembly != null ? $"Library Found: {assembly.FullName}" : "Library not Found");

        GL.ClearColor(Color.FromArgb(255, 20,25,35));
        GL.Enable(EnableCap.DepthTest);
        
        #region Temp

        Mesh mesh = MeshLoader.ParseMesh(
            @".\Models\Cube.obj");

        _meshObject.AttachMesh(mesh);
        _meshObject.BuildMeshBuffers();
        
        mesh = MeshLoader.ParseMesh(
            @".\Models\Icosahedron.obj");
        
        _icosahedron.AttachMesh(mesh);
        _icosahedron.BuildMeshBuffers();

        _tempShader = new Shader().AttachShader("./Shaders/Default/default.vert", "./Shaders/Default/default.frag");

        #endregion
    }
    
    public static void RenderFrame(GameWindow gameWindow, Camera camera, FrameEventArgs args)
    {
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        
        #region Temp

        _time += (float)args.Time;
        
        camera.SetPosition(new Vector3(float.Sin(_time/2) * 8, float.Cos(_time/2) * 5, 15));
        camera.SetRotation(new Vector3(0, 0, 0));
        camera.UpdateViewMatrix();
        
        _tempShader.Use();
        _tempShader.SetUniform("uView", camera.View);
        _tempShader.SetUniform("uProjection", camera.Projection);
        
        _meshObject.Render(_tempShader, new RenderTransform(Quaternion.FromAxisAngle(new Vector3(0, 1, 0), _time)));
        _icosahedron.Render(_tempShader, new RenderTransform(new Vector3(3, 0, 0), Quaternion.FromAxisAngle(new Vector3(0, 1, 0), _time)));
        
        #endregion
        
        gameWindow.SwapBuffers();
    }
}