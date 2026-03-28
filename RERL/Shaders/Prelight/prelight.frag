#extension GL_EXT_nonuniform_qualifier : enable

in vec3 vColor;
in vec3 vNormal;
in vec3 FragPos;
smooth in vec2 texCoord0;

flat in int materialInstance;

struct Material {
    vec4 baseColor;
    uvec2 albedoHandle;
    vec2 _padding; 
};

layout(std430, binding = 0) buffer MaterialBuffer {
    Material materials[];
};

void main()
{
    gNormal = EncodeNormal(normalize(vNormal));

    Material material = materials[nonuniformEXT(materialInstance)];

    vec3 color;

    //Check if the handle is non-zero
    if (material.albedoHandle != uvec2(0u, 0u)) {
        sampler2D albedo = sampler2D(material.albedoHandle);
        vec4 tex = texture(albedo, texCoord0);
        if (tex.a <= 0.0)
            discard;
        color = tex.rgb * material.baseColor.rgb;
    } else {
        color = material.baseColor.rgb;
    }
    //color = material.baseColor.rgb;

    gAlbedo = vec4(color, 1.0);
}
