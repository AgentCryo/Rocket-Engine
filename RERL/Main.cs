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

    static readonly float[] _vertices = {
        -0.5f, -0.5f, 0.0f, //Bottom-left vertex
        0.5f, -0.5f, 0.0f, //Bottom-right vertex
        0.0f,  0.5f, 0.0f  //Top vertex
    };

    static int _vertexBufferObject;

    static Shader _tempShader;

    static int _vertexArrayObject;

    static Vector3[] _colors =
    {
        new(1f, 0f, 0f),
        new(0f, 1f, 0f),
        new(0f, 0f, 1f)
    };

    static float _time;
    
    #endregion
    
    public static void Load()
    {
        var assembly = AppDomain.CurrentDomain .GetAssemblies() .FirstOrDefault(a => a.GetName().Name == "RERL");

        Console.WriteLine(assembly != null ? $"Library Found: {assembly.FullName}" : "Library not Found");

        GL.ClearColor(Color.FromArgb(255, 20,25,35));

        #region Temp

        _vertexArrayObject = GL.GenVertexArray();
        GL.BindVertexArray(_vertexArrayObject);
        
        _vertexBufferObject = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
        GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);
        
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        _tempShader = new Shader().AttachShader("./Shaders/Default/default.vert", "./Shaders/Default/default.frag");

        #endregion
    }
    
    public static void RenderFrame(GameWindow gameWindow, FrameEventArgs args)
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

        _tempShader.Use();
        _tempShader.SetUniform("uColors", colors);

        GL.BindVertexArray(_vertexArrayObject);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        
        #endregion
        
        gameWindow.SwapBuffers();
    }
}