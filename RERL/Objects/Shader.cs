using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace RERL.Objects;

public class Shader
{
    int Handle { get; set; }
    readonly Dictionary<string, int> _uniformCache = new();
    readonly Dictionary<string, Func<object?>> _autoUniforms = new();

    public void Use() => GL.UseProgram(Handle);
    public int GetHandle() => Handle;

    public Shader AttachShader(string vertexPath, string fragmentPath)
    {
        string vertexSource = File.ReadAllText(vertexPath);
        string fragmentSource = File.ReadAllText(fragmentPath);

        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        if (vertexShader == -1) throw new Exception("ERR: Vertex Shader could not be created!");
        GL.ShaderSource(vertexShader, vertexSource);
        GL.CompileShader(vertexShader);
        CheckCompile(vertexShader, "VERTEX");

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        if (fragmentShader == -1) throw new Exception("ERR: Fragment Shader could not be created!");
        GL.ShaderSource(fragmentShader, fragmentSource);
        GL.CompileShader(fragmentShader);
        CheckCompile(fragmentShader, "FRAGMENT");

        Handle = GL.CreateProgram();
        GL.AttachShader(Handle, vertexShader);
        GL.AttachShader(Handle, fragmentShader);
        GL.LinkProgram(Handle);
        CheckLink(Handle);

        GL.DetachShader(Handle, vertexShader);
        GL.DetachShader(Handle, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        return this;
    }

    void CheckCompile(int shader, string type)
    {
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status == 0) {
            string info = GL.GetShaderInfoLog(shader);
            throw new Exception($"{type} SHADER COMPILATION ERROR:\n{info}");
        }
    }


    void CheckLink(int program)
    {
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int status);
        if (status == 0) {
            string info = GL.GetProgramInfoLog(program);
            throw new Exception($"SHADER LINKING ERROR:\n{info}");
        }
    }

    int GetUniformLocation(string name)
    {
        if (_uniformCache.TryGetValue(name, out int loc))
            return loc;

        int location = GL.GetUniformLocation(Handle, name);
        _uniformCache[name] = location;
        return location;
    }

    /// <summary>
    /// Sets a uniform value on the shader program.
    /// The shader must be bound with <see cref="Use"/> before calling this
    /// </summary>
    /// <param name="name">The uniform name in the GLSL shader.</param>
    /// <param name="value">The value to assign to the uniform.</param>
    /// <param name="silence">If true, missing uniforms won't throw.</param>
    /// <returns>True if the uniform was set successfully.</returns>
    public bool ApplyUniform(string name, object? value, bool silence = false)
    {
        int location = GetUniformLocation(name);

        if (location == -1 && !silence)
            throw new Exception($"ERR: Uniform '{name}' not found.");
        if (location == -1 && silence) return false;

        switch (value) {
            case float f:
                GL.Uniform1(location, f); break;

            case int i:
                GL.Uniform1(location, i); break;

            case bool b:
                GL.Uniform1(location, b ? 1 : 0); break;

            case Vector2 v2:
                GL.Uniform2(location, v2); break;

            case Vector3 v3:
                GL.Uniform3(location, v3); break;

            case Vector3[] v3Arr:
                if (v3Arr.Length == 0)
                    return silence ? false : throw new Exception($"ERR: Cannot set empty Vector3[] uniform '{name}'.");

                GL.Uniform3(location, v3Arr.Length, ref v3Arr[0].X);
                break;

            case Vector4 v4:
                GL.Uniform4(location, v4); break;

            case Matrix4 m4:
                GL.UniformMatrix4(location, false, ref m4); break;

            default:
                return silence
                    ? false
                    : throw new Exception($"ERR: Unsupported uniform type '{value?.GetType()}' for '{name}'.");
        }

        return true;
    }

    public bool AddAutoUniform(string name, Func<object?> getter, bool silence = false)
    {
        int location = GetUniformLocation(name);

        if (location == -1 && !silence)
            throw new Exception($"ERR: Uniform '{name}' not found.");
        if (location == -1 && silence) return false;
        
        _autoUniforms[name] = getter;
        return true;
    }

    public void ApplyAutoUniforms()
    {
        foreach (KeyValuePair<string, Func<object?>> kvp in _autoUniforms) {
            ApplyUniform(kvp.Key, kvp.Value?.Invoke());
        }
    }
}