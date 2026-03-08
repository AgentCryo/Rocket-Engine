#version 330 core

in vec3 vColor;
in vec3 Normal;
in vec3 FragPos;

out vec4 FragColor;

const float ambientLight = 0.2;
const vec3 lightPos = vec3(10, 8, 5);

void main()
{
    vec3 ambient = ambientLight * vec3(1,1,1);
    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(lightPos - FragPos);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * vec3(1,1,1);
    FragColor = vec4(vColor * (ambient + diffuse), 1.0);
}
