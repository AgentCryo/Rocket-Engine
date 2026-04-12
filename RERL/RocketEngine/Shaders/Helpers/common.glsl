// ======================================================
// Rocket Engine - Common.glsl
// ======================================================

// Encode a world-space normal into 0-1 range
vec4 EncodeNormal(vec3 n)
{
    return vec4(n * 0.5 + 0.5, 1.0);
}

// Decode a normal from 0-1 back to -1..1
vec3 DecodeNormal(vec4 packedNorm)
{
    return normalize(packedNorm.xyz * 2.0 - 1.0);
}

float LinearizeDepth(float depth, float near, float far)
{
    float z_ndc = depth * 2.0 - 1.0;
    float a = far / (far - near);
    float b = -near * far / (far - near);
    return b / (z_ndc - a);
}

float UnlinearizeDepth(float z, float near, float far)
{
    float a = far / (far - near);
    float b = -near * far / (far - near);
    float z_ndc = (a * z + b) / z;
    return z_ndc * 0.5 + 0.5;
}

// Reconstruct world position from depth
vec3 ReconstructWorldPos(vec2 uv, float depth, mat4 invProj, mat4 invView)
{
    vec4 clip = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 view = invProj * clip;
    view /= view.w;
    vec4 world = invView * vec4(view.xyz, 1.0);
    return world.xyz;
}

// Default GBuffer write (fallback)
//void DefaultGBufferWrite(vec3 albedo, vec3 normal)
//{
//    gAlbedo  = vec4(albedo, 1.0);
//    gNormal  = EncodeNormal(normal);
//}

bool IsBitSet(uint value, int bitIndex)
{
    return (value & (1u << bitIndex)) != 0;
}

// common.glsl END
