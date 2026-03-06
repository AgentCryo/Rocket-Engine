#version 330 core

layout(location = 0) in vec3 aPos;

smooth out vec3 vColor;

void main()
{
    gl_Position = vec4(aPos, 1.0);

    if (gl_VertexID == 0)
        vColor = vec3(1.0, 0.0, 0.0); // Red
    else if (gl_VertexID == 1)
        vColor = vec3(0.0, 1.0, 0.0); // Green
    else
        vColor = vec3(0.0, 0.0, 1.0); // Blue
}
