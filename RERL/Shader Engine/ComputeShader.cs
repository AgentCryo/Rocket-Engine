using OpenTK.Graphics.OpenGL4;

namespace RERL.Shader_Engine;

public sealed class ComputeShader : ShaderProgram
{
    public ComputeShader(string path, ShaderVariant? variant = null)
    {
        Compile(path, variant);
    }

    void Compile(string path, ShaderVariant? variant)
    {
        int shader = GL.CreateShader(ShaderType.ComputeShader);

        GL.ShaderSource(shader, ShaderSource.Load(path, variant));
        GL.CompileShader(shader);

        CheckCompile(shader, "COMPUTE", path);

        CreateProgram();

        GL.AttachShader(Handle, shader);
        Link();

        GL.DetachShader(Handle, shader);
        GL.DeleteShader(shader);

        Reflect();
    }

    /// <summary>
    /// Binds the program, refreshes its auto-uniforms, then dispatches. This is the
    /// only place Use()/ApplyAutoUniforms() happen for compute work - callers should
    /// never call them again before or after this.
    /// </summary>
    public void Dispatch(int x, int y = 1, int z = 1)
    {
        Use();
        ApplyAutoUniforms();
        GL.DispatchCompute(x, y, z);
    }
}