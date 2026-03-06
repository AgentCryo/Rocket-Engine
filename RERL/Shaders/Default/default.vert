#version 330 core

layout(location = 0) in vec3 aPos;

uniform vec3 uColors[3];

smooth out vec3 vColor;

void main()
{
    gl_Position = vec4(aPos, 1.0);

    vColor = uColors[gl_VertexID];
}
