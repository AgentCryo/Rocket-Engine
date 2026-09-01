using System.Text.RegularExpressions;

namespace RERL.Shader_Engine;

public static class ShaderSource
{
    static readonly Regex IncludeRegex = new(
        @"^\s*#include\s*[<""](?<path>[^>""]+)[>""]\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public static string Load(string path, ShaderVariant? variant = null)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Shader source not found.", path);

        string source = File.ReadAllText(path);

        return Build(source, variant, Path.GetFullPath(path));
    }

    public static string Build(string source, ShaderVariant? variant = null)
    {
        return Build(source, variant, null);
    }

    static string Build(string source, ShaderVariant? variant, string? sourcePath)
    {
        string resolved = ResolveIncludes(source, sourcePath, []);

        return "#version 460 core\n" +
               (variant?.Build() ?? string.Empty) +
               resolved;
    }

    static string ResolveIncludes(string source, string? sourcePath, HashSet<string> includeStack)
    {
        string sourceDirectory = sourcePath != null
            ? Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory()
            : Directory.GetCurrentDirectory();

        return IncludeRegex.Replace(source, match =>
        {
            string includePath = match.Groups["path"].Value;

            string fullPath;

            if (Path.IsPathRooted(includePath))
            {
                fullPath = Path.GetFullPath(includePath);
            }
            else if (includePath.StartsWith("./RocketEngine/", StringComparison.OrdinalIgnoreCase))
            {
                fullPath = Path.GetFullPath(
                    Path.Combine(Directory.GetCurrentDirectory(), includePath));
            }
            else
            {
                fullPath = Path.GetFullPath(
                    Path.Combine(sourceDirectory, includePath));
            }

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"Shader include not found: '{includePath}'\n" +
                    $"Resolved path: '{fullPath}'",
                    fullPath);
            }

            if (!includeStack.Add(fullPath))
            {
                string chain = string.Join(
                    " -> ",
                    includeStack.Append(fullPath));

                throw new InvalidOperationException(
                    $"Circular shader include detected:\n{chain}");
            }

            string includedSource = File.ReadAllText(fullPath);

            string resolved = ResolveIncludes(
                includedSource,
                fullPath,
                includeStack);

            includeStack.Remove(fullPath);

            return "\n" + resolved + "\n";
        });
    }

    public static string Combine(params string[] sources)
    {
        return string.Join("\n", sources);
    }
}
