using OpenTK.Graphics.OpenGL4;

namespace RERL.ShaderTypes;

public class Compute : Shader
{
    public Shader AttachComputeShader(string computePath)
    {
        string computeSource = File.ReadAllText(computePath);

        int computeShader = GL.CreateShader(OpenTK.Graphics.OpenGL4.ShaderType.ComputeShader);
        if (computeShader == -1)
            throw new Exception("ERR: Compute Shader could not be created!");

        GL.ShaderSource(computeShader, "#version 460 core\n" + computeSource);
        GL.CompileShader(computeShader);
        CheckCompile(computeShader, "COMPUTE", computePath);

        if (Handle != 0)
            GL.DeleteProgram(Handle);

        Handle = GL.CreateProgram();
        _uniformCache.Clear();
        _autoUniforms.Clear();

        GL.AttachShader(Handle, computeShader);
        GL.LinkProgram(Handle);
        CheckLink(Handle);

        GL.DetachShader(Handle, computeShader);
        GL.DeleteShader(computeShader);

        return this;
    }
    
    public void Dispatch(int x, int y = 1, int z = 1)
    {
        GL.DispatchCompute(x, y, z);
    }
}