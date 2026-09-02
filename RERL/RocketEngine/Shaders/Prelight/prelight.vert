layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;
layout(location = 3) in vec4 aTangent; // xyz = tangent, w = handedness
layout(location = 4) in int aMaterialIndex;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

smooth out vec3 vColor;
smooth out vec3 vNormal;
smooth out vec3 FragPos;
smooth out mat3 vTBN;
smooth out vec2 texCoord0;
flat out int materialInstance;

void main()
{
    gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0);
    FragPos = vec3(uModel * vec4(aPos, 1.0));

    mat3 normalMatrix = mat3(transpose(inverse(uModel)));

    vec3 N = normalize(normalMatrix * aNormal);
    vec3 T = normalize(normalMatrix * aTangent.xyz);
    T = normalize(T - dot(T, N) * N);
    vec3 B = cross(N, T) * aTangent.w;

    vTBN = mat3(T, B, N);
    vNormal = N;
    vColor = vec3(1.0, 1.0, 1.0);

    texCoord0 = aTexCoord;
    materialInstance = aMaterialIndex;
}