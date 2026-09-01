namespace RERL.Shader_Engine;

public sealed class ShaderVariant
{
    readonly Dictionary<string, string?> _defines = new();

    public ShaderVariant Define(string name, string? value = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Define name cannot be empty.", nameof(name));

        _defines[name] = value;
        return this;
    }

    public ShaderVariant Remove(string name)
    {
        _defines.Remove(name);
        return this;
    }

    public bool Contains(string name) => _defines.ContainsKey(name);

    internal string Build()
    {
        if (_defines.Count == 0)
            return string.Empty;

        var result = new System.Text.StringBuilder();

        foreach (var define in _defines)
        {
            result.Append("#define ");
            result.Append(define.Key);

            if (define.Value != null)
            {
                result.Append(' ');
                result.Append(define.Value);
            }

            result.AppendLine();
        }

        return result.ToString();
    }
}