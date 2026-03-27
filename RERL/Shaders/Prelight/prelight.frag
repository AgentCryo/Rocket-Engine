in vec3 vColor;
in vec3 vNormal;
in vec3 FragPos;
smooth in vec2 texCoord0;

flat in int materialInstance;

struct Material {
    vec4 baseColor;
    int albedo;
    //int normal;
    //int orm;
};

layout(std430, binding = 0) buffer MaterialBuffer {
    Material materials[];
};

uniform sampler2DArray uAlbedoTextures;

void main()
{
    gNormal = EncodeNormal(normalize(vNormal));
    //sampler2D albedo = sampler2D(materials[materialInstance].albedoHandle);
    //vec3 color = texture(albedo, texCoord0).rgb * materials[materialInstance].baseColor;
    
    Material material = materials[materialInstance];
    vec3 color;

    if(texture(uAlbedoTextures, vec3(texCoord0, material.albedo)).a <= 0) discard;

    if (material.albedo >= 0)
        color = texture(uAlbedoTextures, vec3(texCoord0, material.albedo)).rgb * material.baseColor.rgb;
    else
        color = material.baseColor.rgb;
    //color = materialInstance == 0 ? vec3(1,0,0) : vec3(0,1,0);
            
    gAlbedo = vec4(color, 1.0);
}
