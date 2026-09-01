using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace RERL.Shader_Engine;

public sealed class ShaderParameter
{
    public string Name { get; }
    public int Location { get; }
    public ActiveUniformType Type { get; }
    public int Size { get; }
    public int ProgramHandle { get; }

    internal ShaderParameter(string name, int location, ActiveUniformType type, int size, int programHandle)
    {
        Name = name;
        Location = location;
        Type = type;
        Size = size;
        ProgramHandle = programHandle;
    }

    public bool Apply(object? value, bool transposeMatrix = false)
    {
        if (value is null || Location < 0)
            return false;

        GL.GetInteger(GetPName.CurrentProgram, out int currentProgram);

        if (currentProgram != ProgramHandle)
            throw new InvalidOperationException(
                $"Shader parameter '{Name}' belongs to program {ProgramHandle}, but program {currentProgram} is currently bound.");

        switch (value)
        {
            case float v:
                Require(ActiveUniformType.Float, nameof(v));
                GL.Uniform1(Location, v);
                return true;

            case double v:
                if (Type != ActiveUniformType.Double)
                    throw new InvalidOperationException(
                        $"Shader parameter '{Name}' is {Type}, but a double value was supplied.");

                GL.Uniform1(Location, v);
                return true;

            case int v:
                if (IsSampler(Type) || IsInteger(Type))
                {
                    GL.Uniform1(Location, v);
                    return true;
                }

                Require(ActiveUniformType.Int, nameof(v));
                GL.Uniform1(Location, v);
                return true;

            case uint v:
                Require(ActiveUniformType.UnsignedInt, nameof(v));
                GL.Uniform1(Location, v);
                return true;

            case bool v:
                Require(ActiveUniformType.Bool, nameof(v));
                GL.Uniform1(Location, v ? 1 : 0);
                return true;

            case Vector2 v:
                Require(ActiveUniformType.FloatVec2, nameof(v));
                GL.Uniform2(Location, v);
                return true;

            case Vector2i v:
                Require(ActiveUniformType.IntVec2, nameof(v));
                GL.Uniform2(Location, v.X, v.Y);
                return true;

            case Vector3 v:
                Require(ActiveUniformType.FloatVec3, nameof(v));
                GL.Uniform3(Location, v);
                return true;

            case Vector3i v:
                Require(ActiveUniformType.IntVec3, nameof(v));
                GL.Uniform3(Location, v.X, v.Y, v.Z);
                return true;

            case Vector4 v:
                Require(ActiveUniformType.FloatVec4, nameof(v));
                GL.Uniform4(Location, v);
                return true;

            case Vector4i v:
                Require(ActiveUniformType.IntVec4, nameof(v));
                GL.Uniform4(Location, v.X, v.Y, v.Z, v.W);
                return true;

            case Quaternion v:
                Require(ActiveUniformType.FloatVec4, nameof(v));
                GL.Uniform4(Location, new Vector4(v.X, v.Y, v.Z, v.W));
                return true;

            case Matrix4 v:
                Require(ActiveUniformType.FloatMat4, nameof(v));
                ApplyMatrix(v, transposeMatrix);
                return true;

            case float[] v when v.Length > 0:
                Require(ActiveUniformType.Float, nameof(v));
                GL.Uniform1(Location, v.Length, v);
                return true;

            case int[] v when v.Length > 0:
                if (IsSampler(Type) || IsInteger(Type))
                {
                    GL.Uniform1(Location, v.Length, v);
                    return true;
                }

                Require(ActiveUniformType.Int, nameof(v));
                GL.Uniform1(Location, v.Length, v);
                return true;

            case uint[] v when v.Length > 0:
                Require(ActiveUniformType.UnsignedInt, nameof(v));
                GL.Uniform1(Location, v.Length, v);
                return true;

            case Vector2[] v when v.Length > 0:
                Require(ActiveUniformType.FloatVec2, nameof(v));
                GL.Uniform2(Location, v.Length, ref v[0].X);
                return true;

            case Vector3[] v when v.Length > 0:
                Require(ActiveUniformType.FloatVec3, nameof(v));
                GL.Uniform3(Location, v.Length, ref v[0].X);
                return true;

            case Vector4[] v when v.Length > 0:
                Require(ActiveUniformType.FloatVec4, nameof(v));
                GL.Uniform4(Location, v.Length, ref v[0].X);
                return true;

            case Matrix4[] v when v.Length > 0:
                Require(ActiveUniformType.FloatMat4, nameof(v));
                ApplyMatrixArray(v, transposeMatrix);
                return true;

            default:
                throw new NotSupportedException(
                    $"Unsupported value type '{value.GetType().FullName}' for shader parameter '{Name}' ({Type}).");
        }
    }

    static bool IsSampler(ActiveUniformType type)
    {
        return type switch
        {
            ActiveUniformType.Sampler1D => true,
            ActiveUniformType.Sampler2D => true,
            ActiveUniformType.Sampler3D => true,
            ActiveUniformType.SamplerCube => true,
            ActiveUniformType.Sampler1DShadow => true,
            ActiveUniformType.Sampler2DShadow => true,
            ActiveUniformType.Sampler1DArray => true,
            ActiveUniformType.Sampler2DArray => true,
            ActiveUniformType.Sampler1DArrayShadow => true,
            ActiveUniformType.Sampler2DMultisample => true,
            ActiveUniformType.Sampler2DMultisampleArray => true,
            ActiveUniformType.SamplerCubeShadow => true,
            ActiveUniformType.SamplerCubeMapArray => true,
            ActiveUniformType.SamplerCubeMapArrayShadow => true,
            ActiveUniformType.IntSampler1D => true,
            ActiveUniformType.IntSampler2D => true,
            ActiveUniformType.IntSampler3D => true,
            ActiveUniformType.IntSamplerCube => true,
            ActiveUniformType.IntSampler1DArray => true,
            ActiveUniformType.IntSampler2DArray => true,
            ActiveUniformType.IntSampler2DMultisample => true,
            ActiveUniformType.IntSampler2DMultisampleArray => true,
            ActiveUniformType.IntSamplerCubeMapArray => true,
            ActiveUniformType.UnsignedIntSampler1D => true,
            ActiveUniformType.UnsignedIntSampler2D => true,
            ActiveUniformType.UnsignedIntSampler3D => true,
            ActiveUniformType.UnsignedIntSamplerCube => true,
            ActiveUniformType.UnsignedIntSampler1DArray => true,
            ActiveUniformType.UnsignedIntSampler2DArray => true,
            ActiveUniformType.UnsignedIntSampler2DMultisample => true,
            ActiveUniformType.UnsignedIntSampler2DMultisampleArray => true,
            _ => false
        };
    }

    static bool IsInteger(ActiveUniformType type)
    {
        return type switch
        {
            ActiveUniformType.Int => true,
            ActiveUniformType.IntVec2 => true,
            ActiveUniformType.IntVec3 => true,
            ActiveUniformType.IntVec4 => true,
            ActiveUniformType.UnsignedInt => true,
            ActiveUniformType.UnsignedIntVec2 => true,
            ActiveUniformType.UnsignedIntVec3 => true,
            ActiveUniformType.UnsignedIntVec4 => true,
            ActiveUniformType.Bool => true,
            ActiveUniformType.BoolVec2 => true,
            ActiveUniformType.BoolVec3 => true,
            ActiveUniformType.BoolVec4 => true,
            _ => false
        };
    }

    void Require(ActiveUniformType expected, string supplied)
    {
        if (Type != expected)
            throw new InvalidOperationException(
                $"Shader parameter '{Name}' is {Type}, but a {supplied} value was supplied.");
    }

    void ApplyMatrix(Matrix4 matrix, bool transpose)
    {
        float[] values =
        [
            matrix.M11, matrix.M12, matrix.M13, matrix.M14,
            matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34,
            matrix.M41, matrix.M42, matrix.M43, matrix.M44
        ];

        GL.UniformMatrix4(Location, 1, transpose, values);
    }

    void ApplyMatrixArray(Matrix4[] matrices, bool transpose)
    {
        float[] values = new float[matrices.Length * 16];

        for (int i = 0; i < matrices.Length; i++)
        {
            Matrix4 m = matrices[i];
            int o = i * 16;

            values[o] = m.M11;
            values[o + 1] = m.M12;
            values[o + 2] = m.M13;
            values[o + 3] = m.M14;
            values[o + 4] = m.M21;
            values[o + 5] = m.M22;
            values[o + 6] = m.M23;
            values[o + 7] = m.M24;
            values[o + 8] = m.M31;
            values[o + 9] = m.M32;
            values[o + 10] = m.M33;
            values[o + 11] = m.M34;
            values[o + 12] = m.M41;
            values[o + 13] = m.M42;
            values[o + 14] = m.M43;
            values[o + 15] = m.M44;
        }

        GL.UniformMatrix4(Location, matrices.Length, transpose, values);
    }

    public override string ToString() =>
        $"{Name} ({Type}, Location={Location}, Size={Size}, Program={ProgramHandle})";
}