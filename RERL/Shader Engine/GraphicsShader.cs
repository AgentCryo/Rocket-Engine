using OpenTK.Graphics.OpenGL4;

namespace RERL.Shader_Engine;

public sealed class GraphicsShader : ShaderProgram
{
    public GraphicsShader(string vertexPath, string fragmentPath, ShaderVariant? variant = null)
    {
        Compile(vertexPath, fragmentPath, variant);
    }

    void Compile(string vertexPath, string fragmentPath, ShaderVariant? variant)
    {
        int vertex = CompileStage(ShaderType.VertexShader, vertexPath, variant);
        int fragment = CompileStage(ShaderType.FragmentShader, fragmentPath, variant);

        CreateProgram();

        GL.AttachShader(Handle, vertex);
        GL.AttachShader(Handle, fragment);

        Link();

        GL.DetachShader(Handle, vertex);
        GL.DetachShader(Handle, fragment);

        GL.DeleteShader(vertex);
        GL.DeleteShader(fragment);

        Reflect();
    }

    static int CompileStage(ShaderType type, string path, ShaderVariant? variant)
    {
        int shader = GL.CreateShader(type);
        string source = ShaderSource.Load(path, variant);

        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);

        CheckCompile(shader, type.ToString().ToUpperInvariant(), path);

        return shader;
    }
}