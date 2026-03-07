using OpenTK.Mathematics;
using System.Drawing;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using RERL.Objects;

namespace RERL;

public static class Main
{
    struct Vertex
    {
        Vector3 _position;
        Vector3 _normal;
        Vector2 _textureCoord;
    }

    struct Mesh
    {
        Vertex[] _vertices;
        int[] _indices;
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

    static Vector3[] _colors =
    {
        new(1f, 0f, 0f),
        new(0f, 1f, 0f),
        new(0f, 0f, 1f)
    };

    static float _time;
    
    static Matrix4 _model = Matrix4.Identity;
    
    #endregion
    
    public static void Load()
    {
        var assembly = AppDomain.CurrentDomain .GetAssemblies() .FirstOrDefault(a => a.GetName().Name == "RERL");

        Console.WriteLine(assembly != null ? $"Library Found: {assembly.FullName}" : "Library not Found");

        GL.ClearColor(Color.FromArgb(255, 20,25,35));
        
        #region Temp
        
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
        GL.Clear(ClearBufferMask.ColorBufferBit);
        
        #region Temp

        _time += (float)args.Time;
        float t = _time * 3.14f;

        Vector3[] colors =
        {
            new(MathF.Sin(t + 0f) * 0.5f + 0.5f,
                MathF.Sin(t + 2f) * 0.5f + 0.5f,
                MathF.Sin(t + 4f) * 0.5f + 0.5f),

            new(MathF.Sin(t + 2f) * 0.5f + 0.5f,
                MathF.Sin(t + 4f) * 0.5f + 0.5f,
                MathF.Sin(t + 0f) * 0.5f + 0.5f),

            new(MathF.Sin(t + 4f) * 0.5f + 0.5f,
                MathF.Sin(t + 0f) * 0.5f + 0.5f,
                MathF.Sin(t + 2f) * 0.5f + 0.5f),
        };
        
        camera.SetPosition(new Vector3(float.Sin(_time) * 10, float.Cos(_time) * 10, 10));
        camera.SetRotation(new Vector3(0, 0, 0));
        camera.UpdateViewMatrix();
        
        _tempShader.Use();
        _tempShader.SetUniform("uColors", colors);
        _tempShader.SetUniform("uModel", _model);
        _tempShader.SetUniform("uView", camera.View);
        _tempShader.SetUniform("uProjection", camera.Projection);

        GL.BindVertexArray(_vertexArrayObject);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        
        #endregion
        
        gameWindow.SwapBuffers();
    }
}