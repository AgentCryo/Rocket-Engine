#version 330 core

layout(location = 0) in vec3 aPos;

uniform vec3 uColors[3];
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

smooth out vec3 vColor;

void main()
{
    gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0);

    vColor = uColors[gl_VertexID];
}
