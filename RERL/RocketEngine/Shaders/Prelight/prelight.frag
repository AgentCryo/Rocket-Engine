#include "./RocketEngine/Shaders/Helpers/common.glsl"
#include "./RocketEngine/Shaders/Helpers/gbuffer.glsl"

in vec3 vColor;
in vec3 vNormal;
in vec3 FragPos;
in mat3 vTBN;
smooth in vec2 texCoord0;

flat in int materialInstance;

struct Material {
    vec4 baseColor;
    uvec2 albedoHandle;
    uvec2 normalHandle;
};

layout(std430, binding = 0) buffer MaterialBuffer {
    Material materials[];
};

void main()
{
    Material material = materials[nonuniformEXT(materialInstance)];

    // Computed once, shared by both texture fetches below - avoids paying
    // for derivative computation twice for the same UV coordinate.
    vec2 dUVdx = dFdx(texCoord0);
    vec2 dUVdy = dFdy(texCoord0);

    vec3 worldNormal = normalize(vNormal);

    if (material.normalHandle != uvec2(0u, 0u)) {
        sampler2D normalMap = sampler2D(nonuniformEXT(material.normalHandle));
        vec3 tangentNormal = textureGrad(normalMap, texCoord0, dUVdx, dUVdy).rgb * 2.0 - 1.0;
        worldNormal = normalize(vTBN * tangentNormal);
    }

    gNormal = EncodeNormal(worldNormal);

    vec3 color;

    if (material.albedoHandle != uvec2(0u, 0u)) {
        sampler2D albedo = sampler2D(nonuniformEXT(material.albedoHandle));
        vec4 tex = textureGrad(albedo, texCoord0, dUVdx, dUVdy);
        if (tex.a <= 0.0)
            discard;
        color = tex.rgb * material.baseColor.rgb;
    } else {
        color = material.baseColor.rgb;
    }

    gAlbedo = vec4(color, 1.0);
    gPosition = vec4(FragPos, 1.0);
}