namespace RERL.Shader_Engine;

public static class ShaderEngine
{
    static readonly Dictionary<string, ShaderProgram> _shaders = new();

    public static IReadOnlyDictionary<string, ShaderProgram> Shaders => _shaders;

    public static GraphicsShader RegisterGraphics(string name, string vertexPath, string fragmentPath, ShaderVariant? variant = null)
    {
        if (_shaders.ContainsKey(name))
            throw new InvalidOperationException($"Shader '{name}' is already registered.");

        var shader = new GraphicsShader(vertexPath, fragmentPath, variant);
        _shaders.Add(name, shader);
        return shader;
    }

    public static ComputeShader RegisterCompute(string name, string path, ShaderVariant? variant = null)
    {
        if (_shaders.ContainsKey(name))
            throw new InvalidOperationException($"Shader '{name}' is already registered.");

        var shader = new ComputeShader(path, variant);
        _shaders.Add(name, shader);
        return shader;
    }

    public static ShaderProgram Get(string name)
    {
        if (!_shaders.TryGetValue(name, out var shader))
            throw new KeyNotFoundException($"Shader '{name}' is not registered.");

        return shader;
    }

    public static T Get<T>(string name) where T : ShaderProgram
    {
        return (T)Get(name);
    }

    public static bool Unregister(string name)
    {
        if (!_shaders.Remove(name, out var shader))
            return false;

        shader.Dispose();
        return true;
    }

    public static void Clear()
    {
        foreach (var shader in _shaders.Values)
            shader.Dispose();

        _shaders.Clear();
    }
}