layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord0;
layout(location = 4) in int aMaterialIndexes;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

smooth out vec3 vColor;
smooth out vec3 vNormal;
smooth out vec3 FragPos;
smooth out vec2 texCoord0;

flat out int materialInstance;

void main()
{
    gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0);
    FragPos = vec3(uModel * vec4(aPos, 1.0));
    mat3 normalMatrix = mat3(transpose(inverse(uModel)));
    vNormal = normalize(normalMatrix * aNormal);
    vColor = vec3(1.0, 1.0, 1.0);
    materialInstance = aMaterialIndexes;
    texCoord0 = aTexCoord0;
}
