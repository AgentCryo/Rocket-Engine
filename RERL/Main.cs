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

public static class Main
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
        public Vector3 Position = default;
        public Quaternion Rotation = Quaternion.Identity;
        public Vector3 Scale = Vector3.One;

        public RenderTransform(Quaternion rotation)
        {
            Rotation = rotation;
        }
        public RenderTransform()
        {
            
        }
    }

    #region Temp

    static readonly float[] Vertices = {
        -0.5f, -0.5f, 0.0f, //Bottom-left vertex
        0.5f, -0.5f, 0.0f, //Bottom-right vertex
        0.0f,  0.5f, 0.0f  //Top vertex
    };

    static int _vertexBufferObject;

    static Shader? _tempShader;

    static int _vertexArrayObject;

    static float _time;
    
    static Matrix4 _model = Matrix4.Identity;

    static MeshRender _meshObject = new();
    
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
        
        _model *= Matrix4.CreateRotationX(float.DegreesToRadians(-20));

        _vertexArrayObject = GL.GenVertexArray();
        GL.BindVertexArray(_vertexArrayObject);
        
        _vertexBufferObject = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
        GL.BufferData(BufferTarget.ArrayBuffer, Vertices.Length * sizeof(float), Vertices, BufferUsageHint.StaticDraw);
        
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

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
        _tempShader.SetUniform("uModel", _model);
        _tempShader.SetUniform("uView", camera.View);
        _tempShader.SetUniform("uProjection", camera.Projection);

        GL.BindVertexArray(_vertexArrayObject);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        
        _meshObject.Render(_tempShader, new RenderTransform(Quaternion.FromAxisAngle(new Vector3(0, 1, 0), _time)));
        
        #endregion
        
        gameWindow.SwapBuffers();
    }
}