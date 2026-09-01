using OpenTK.Graphics.OpenGL4;

namespace RERL.Shader_Engine;

public abstract class ShaderProgram : IDisposable
{
    protected int Handle { get; private set; }

    readonly Dictionary<string, ShaderParameter> _parameters = new();
    readonly Dictionary<string, Func<object?>> _autoUniforms = new();

    public IReadOnlyDictionary<string, ShaderParameter> Parameters => _parameters;

    public bool IsValid => Handle != 0;

    public void Use()
    {
        if (Handle == 0)
            throw new InvalidOperationException("Shader program is not initialized.");

        GL.UseProgram(Handle);
    }

    public int GetHandle() => Handle;

    protected void CreateProgram()
    {
        Dispose();

        Handle = GL.CreateProgram();

        if (Handle == 0)
            throw new Exception("Failed to create shader program.");
    }

    protected void Link()
    {
        GL.LinkProgram(Handle);
        GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int status);

        if (status == 0)
        {
            string log = GL.GetProgramInfoLog(Handle);
            throw new Exception($"SHADER LINKING ERROR:\n{log}");
        }
    }

    protected static void CheckCompile(int shader, string type, string path)
    {
        GL.GetShader(shader, OpenTK.Graphics.OpenGL4.ShaderParameter.CompileStatus, out int status);

        if (status == 0)
        {
            string log = GL.GetShaderInfoLog(shader);
            throw new Exception($"{type} SHADER COMPILATION ERROR in {path}:\n{log}");
        }
    }

    /// <summary>
    /// Reflects the linked program's active uniforms into <see cref="Parameters"/>.
    /// Subclasses must call this once, immediately after <see cref="Link"/>.
    /// </summary>
    protected void Reflect()
    {
        _parameters.Clear();

        GL.GetProgram(Handle, GetProgramParameterName.ActiveUniforms, out int count);

        for (int i = 0; i < count; i++)
        {
            GL.GetActiveUniform(
                Handle,
                i,
                256,
                out _,
                out int size,
                out ActiveUniformType type,
                out string name);

            int location = GL.GetUniformLocation(Handle, name);

            if (location >= 0)
                _parameters[name] = new ShaderParameter(name, location, type, size, Handle);
        }
    }

    public bool Set(string name, object? value) => Set(name, value, false);

    public bool Set(string name, object? value, bool transposeMatrix)
    {
        if (!_parameters.TryGetValue(name, out ShaderParameter? parameter))
            return false;

        return parameter.Apply(value, transposeMatrix);
    }

    public bool RegisterAutoUniform(string name, Func<object?> getter, bool silence = false)
    {
        if (!_parameters.ContainsKey(name))
        {
            if (silence)
                return false;

            throw new Exception($"ERR: Uniform '{name}' not found.");
        }

        _autoUniforms[name] = getter;
        return true;
    }

    /// <summary>
    /// Refreshes every registered auto-uniform. Does NOT bind the program itself -
    /// callers must have already called <see cref="Use"/>. This is intentional: it
    /// lets a single call site (Dispatch, PostProcessShader.Render, etc.) own both
    /// the bind and the refresh without redundant GL.UseProgram calls upstream.
    /// </summary>
    public void ApplyAutoUniforms()
    {
        foreach (var uniform in _autoUniforms)
            Set(uniform.Key, uniform.Value());
    }

    public virtual void Dispose()
    {
        if (Handle == 0)
            return;

        GL.DeleteProgram(Handle);
        Handle = 0;
    }
}