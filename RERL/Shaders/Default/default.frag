#version 330 core

in vec3 vColor;
in vec3 Normal;
in vec3 FragPos;

layout(location = 0) out vec4 FragColor;
layout(location = 1) out vec4 NormalBuffer;

const float ambientLight = 0.2;
const vec3 lightPos = vec3(10, 8, 5);

void main()
{
    vec3 ambient = ambientLight * vec3(1,1,1);
    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(lightPos - FragPos);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * vec3(1,1,1);
    NormalBuffer = vec4(norm, 1.0);
    FragColor = vec4(vColor * (ambient + diffuse), 1.0);
}
