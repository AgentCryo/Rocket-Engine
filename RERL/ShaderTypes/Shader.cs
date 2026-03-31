using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using RCS;

namespace RERL.ShaderTypes;

/// <summary>
/// Represents an OpenGL shader program, supporting compilation, linking,
/// uniform uploads, and automatic uniforms.
/// </summary>
public class Shader
{
    public enum ShaderType
    {
        Shader,
        Prelight,
        PostProcess,
        Compute
    }
    
    public const string DefaultVert = "./Shaders/Templates/Default/default.vert";
    public const string DefaultFrag = "./Shaders/Templates/Default/default.frag";
    public const string DefaultPostProcessVert = "./Shaders/Templates/DefaultPostProcess/defaultPostProcess.vert";
    
    internal int Handle { get; set; }
    internal readonly Dictionary<string, int> _uniformCache = new();
    internal readonly Dictionary<string, Func<object?>> _autoUniforms = new();

    public void Use() => GL.UseProgram(Handle);
    public int GetHandle() => Handle;

    /// <summary>
    /// Loads, compiles, and links a vertex and fragment shader into this shader.
    /// Any previous program stored in this instance is replaced.
    /// </summary>
    /// <param name="vertexPath">Path to the vertex shader source file.</param>
    /// <param name="fragmentPath">Path to the fragment shader source file.</param>
    /// <returns>The current <see cref="Shader"/> instance for chaining.</returns>
    public virtual Shader AttachGraphicsShader(string vertexPath, string fragmentPath, ShaderType shaderType)
    {
        if (shaderType == ShaderType.Compute)
            Logger.Error($"Compute shaders must use the ComputeShader class, not Shader. Offending files: {vertexPath}, {fragmentPath}");
        
        string vertexSource = File.ReadAllText(vertexPath);
        string fragmentSource = File.ReadAllText(fragmentPath);

        int vertexShader = GL.CreateShader(OpenTK.Graphics.OpenGL4.ShaderType.VertexShader);
        if (vertexShader == -1) throw new Exception("ERR: Vertex Shader could not be created!");
        GL.ShaderSource(vertexShader, "#version 460 core\n" + vertexSource); //TODO: Upgrade vertex shader handling.
        GL.CompileShader(vertexShader);
        CheckCompile(vertexShader, "VERTEX", vertexPath);

        string finalFragmentSource = "#version 460 core\n" + fragmentSource;
        switch (shaderType) {
            case ShaderType.Shader:
                finalFragmentSource =
                    "#version 460 core\n" +
                    File.ReadAllText("./Shaders/Helpers/gbuffer.glsl") + "\n" +
                    File.ReadAllText("./Shaders/Helpers/common.glsl") + "\n" +
                    fragmentSource; break;
            case ShaderType.Prelight:
                finalFragmentSource =
                    "#version 460 core\n" +
                    "#extension GL_ARB_bindless_texture : require\n" +
                    File.ReadAllText("./Shaders/Helpers/gbuffer.glsl") + "\n" +
                    File.ReadAllText("./Shaders/Helpers/common.glsl") + "\n" +
                    fragmentSource; break;
            case ShaderType.PostProcess:
                finalFragmentSource =
                    "#version 460 core\n" +
                    File.ReadAllText("./Shaders/Helpers/gbuffer.glsl") + "\n" +
                    File.ReadAllText("./Shaders/Helpers/gbufferSampler.glsl") + "\n" +
                    File.ReadAllText("./Shaders/Helpers/common.glsl") + "\n" +
                    fragmentSource; break;
        }
        
        int fragmentShader = GL.CreateShader(OpenTK.Graphics.OpenGL4.ShaderType.FragmentShader);
        if (fragmentShader == -1) throw new Exception("ERR: Fragment Shader could not be created!");
        GL.ShaderSource(fragmentShader, finalFragmentSource);
        GL.CompileShader(fragmentShader);
        CheckCompile(fragmentShader, "FRAGMENT", fragmentPath);

        if (Handle != 0)
            GL.DeleteProgram(Handle);
        
        Handle = GL.CreateProgram();
        _uniformCache.Clear();
        _autoUniforms.Clear();
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

    internal void CheckCompile(int shader, string type, string shaderPath)
    {
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status == 0)
        {
            string info = GL.GetShaderInfoLog(shader);

            throw new Exception(
                $"{type} SHADER COMPILATION ERROR in {shaderPath}:\n{info}"
            );
        }
    }

    internal void CheckLink(int program)
    {
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int status);
        if (status == 0) {
            string info = GL.GetProgramInfoLog(program);
            throw new Exception($"SHADER LINKING ERROR:\n{info}");
        }
    }

    internal int GetUniformLocation(string name)
    {
        if (_uniformCache.TryGetValue(name, out int loc))
            return loc;

        int location = GL.GetUniformLocation(Handle, name);
        _uniformCache[name] = location;
        return location;
    }

    /// <summary>
    /// Assigns a value to a uniform variable in the shader program.
    /// The shader must be bound using <see cref="Use"/> before calling this.
    /// </summary>
    /// <param name="name">The uniform name in the GLSL shader.</param>
    /// <param name="value">The value to upload to the GPU.</param>
    /// <param name="silence">If true, missing uniforms are ignored instead of throwing.</param>
    /// <returns>True if the uniform was successfully applied.</returns>
    public bool ApplyUniform(string name, object? value, bool transposeMatrix = false, bool silence = false)
    {
        int location = GetUniformLocation(name);

        if (location == -1 && !silence)
            throw new Exception($"ERR: Uniform '{name}' not found.");
        if (location == -1 && silence) return false;

        switch (value) {
            case float f: GL.Uniform1(location, f); break;
            case int i: GL.Uniform1(location, i); break;
            case bool b: GL.Uniform1(location, b ? 1 : 0); break;
            case Vector2 v2: GL.Uniform2(location, v2); break;
            case Vector2i v2: GL.Uniform2(location, v2.X, v2.Y); break;
            case Vector3 v3: GL.Uniform3(location, v3); break;
            case Vector3i v3:  GL.Uniform3(location, v3.X, v3.Y, v3.Z);  break;
            case Vector2[] v2Arr:
                if (v2Arr.Length == 0)
                    return silence ? false : throw new Exception($"ERR: Cannot set empty Vector3[] uniform '{name}'.");
                GL.Uniform2(location, v2Arr.Length, ref v2Arr[0].X);
                break;
            case Vector3[] v3Arr:
                if (v3Arr.Length == 0)
                    return silence ? false : throw new Exception($"ERR: Cannot set empty Vector3[] uniform '{name}'.");
                GL.Uniform3(location, v3Arr.Length, ref v3Arr[0].X);
                break;
            case Vector4 v4: GL.Uniform4(location, v4); break;
            case Matrix4 m4: GL.UniformMatrix4(location, transposeMatrix, ref m4); break;
            case Quaternion q: GL.Uniform4(location, new Vector4(q.X, q.Y, q.Z, q.W)); break;
            default:
                return silence
                    ? false
                    : throw new Exception($"ERR: Unsupported uniform type '{value?.GetType()}' for '{name}'.");
        }

        return true;
    }

    /// <summary>
    /// Registers a uniform whose value is supplied automatically each frame.
    /// </summary>
    /// <param name="name">The uniform name in the GLSL shader.</param>
    /// <param name="getter">A function returning the value to assign.</param>
    /// <param name="silence">If true, missing uniforms are ignored.</param>
    /// <returns>True if the uniform was registered successfully.</returns>
    public bool RegisterAutoUniform(string name, Func<object?> getter, bool silence = false)
    {
        int location = GetUniformLocation(name);

        if (location == -1 && !silence)
            throw new Exception($"ERR: Uniform '{name}' not found.");
        if (location == -1 && silence) return false;
        
        _autoUniforms[name] = getter;
        return true;
    }

    /// <summary>
    /// Applies all automatically registered uniforms.
    /// </summary>
    public void ApplyAutoUniforms()
    {
        foreach (var kvp in _autoUniforms)
            ApplyUniform(kvp.Key, kvp.Value?.Invoke());
    }
}
