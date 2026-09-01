#include "./RocketEngine/Shaders/Helpers/common.glsl"
#include "./RocketEngine/Shaders/Helpers/gbuffer.glsl"

uniform vec3 cameraPos;
uniform vec4 cameraRot;   // quaternion

const float cameraFovX = 1.5708; // in radians
uniform vec2 screenSize;

uniform vec3 objectPos;
uniform vec4 objectRot;   // quaternion
uniform vec3 objectScale;

float MakeBox(vec3 p, vec3 b)
{
   vec3 q = abs(p) - b;
   return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
}

float mergerSponge(vec3 p)
{
   float d = MakeBox(p, vec3(1));

   float s = 3.0;
   for (int m = 0; m < 3; m++)
   {
       vec3 a = mod(p * s, 2.0) - 1.0;
       s *= 3.0;
       vec3 r = abs(1.0 - 3.0 * abs(a));
       float da = max(r.x, r.y);
       float db = max(r.y, r.z);
       float dc = max(r.z, r.x);
       float c = (min(da, min(db, dc)) - 1.0) / s;
       if (c > d)
           d = c;
   }
   return d;
}

float mandelbulb(vec3 p)
{
    vec3 z = p;
    float dr = 1.0;
    float r = 0.0;

    const int Iterations = 8;
    const float Power = 8.0;

    for (int i = 0; i < Iterations; i++)
    {
        r = length(z);
        if (r > 2.0) break;

        // Spherical coordinates
        float theta = acos(z.z / r);
        float phi   = atan(z.y, z.x);

        // Scale derivative
        float rPow = pow(r, Power - 1.0);
        dr = dr * Power * rPow + 1.0;

        // Scale and rotate the point
        float zr = pow(r, Power);
        theta *= Power;
        phi   *= Power;

        z = zr * vec3(
            sin(theta) * cos(phi),
            sin(theta) * sin(phi),
            cos(theta)
        ) + p;
    }

    return 0.5 * log(r) * r / dr;
}

float julia(vec3 p)
{
    vec4 z = vec4(p, 0.0);
    vec4 c = vec4(0.2, 0.7, 0.3, 0.0);

    float dr = 1.0;
    float r = 0.0;

    for (int i = 0; i < 10; i++)
    {
        r = length(z.xyz);
        if (r > 2.0) break;

        // Quaternion square
        z = vec4(
            z.x*z.x - dot(z.yzw, z.yzw),
            2.0*z.x*z.yzw
        ) + c;

        dr = 2.0 * r * dr + 1.0;
    }

    return 0.5 * log(r) * r / dr;
}

vec3 rotateVectorByQuaternion(vec3 v, vec4 q)
{
   vec3 q_xyz = q.xyz;
   float q_w = q.w;
   vec3 t = 2.0 * cross(q_xyz, v);
   return v + q_w * t + cross(q_xyz, t);
}

vec3 objectWorldToLocal(vec3 p)
{
   // translate
   vec3 q = p - vec3(objectPos.x, -objectPos.y, objectPos.z);

   // inverse rotate
   vec4 correctedObjectRot = vec4(-objectRot.x, objectRot.y, -objectRot.z, objectRot.w);
   vec4 invRot = vec4(-correctedObjectRot.xyz, objectRot.w);
   q = rotateVectorByQuaternion(q, invRot);

   // inverse scale
   q /= objectScale * 0.5;

   return q;
}

float map(vec3 pWorld)
{
   return mergerSponge(objectWorldToLocal(pWorld));
}

vec3 calcNormal(vec3 p)
{
   vec2 e = vec2(1.0, -1.0) * 0.0005;
   return normalize(
       e.xyy * map(p + e.xyy) +
       e.yyx * map(p + e.yyx) +
       e.yxy * map(p + e.yxy) +
       e.xxx * map(p + e.xxx));
}

float RayMarch(vec3 origin, vec3 dir, int steps)
{
   float t = 0.0;
   for (int i = 0; i < steps; i++)
   {
       vec3 p = origin + dir * t;
       float d = map(p);
       if (d < 0.001) break;
       t += d;
       if (t > 100.0) break;
   }
   return t;
}

float GetLight(vec3 p, vec3 lightPos)
{
   //vec3 lightDir = normalize(lightPos - p);
   //vec3 n = calcNormal(p);
//
   //float dif = clamp(dot(n, lightDir), 0.0, 1.0);
//
   //float shadowT = RayMarch(p + n * 0.02, lightDir, 128);
   //if (shadowT < length(lightPos - p))
   //    dif *= 0.1;
//
   //return dif;
   return 0.6;
}

mat4 makeProj(float fov, float aspect, float near, float far)
{
    float f = 1.0 / tan(fov * 0.5);

    return mat4(
        f / aspect, 0, 0, 0,
        0, f, 0, 0,
        0, 0, far / (far - near), 1,
        0, 0, (-near * far) / (far - near), 0
    );
}

float fovY_from_fovX(float fovX, float aspect)
{
    return 2.0 * atan(tan(fovX * 0.5) / aspect);
}

void main()
{
    vec2 ndc = (gl_FragCoord.xy / screenSize) * 2.0 - 1.0;
    ndc.y *= -1.0;
    
    float aspect = screenSize.x / screenSize.y;
    float tanHalfFovX = tan(cameraFovX * 0.5);
    float tanHalfFovY = tanHalfFovX / aspect;
    
    vec3 rayDirView = normalize(vec3(
       ndc.x * tanHalfFovX,
       ndc.y * tanHalfFovY,
       1.0
    ));
    
    vec4 correctedCameraRot = vec4(-cameraRot.x, cameraRot.y, -cameraRot.z, cameraRot.w);
    vec3 origin = vec3(cameraPos.x, -cameraPos.y, cameraPos.z);
    vec3 rayDir = rotateVectorByQuaternion(rayDirView, correctedCameraRot);

    float hitDist = RayMarch(origin, rayDir, 300);
    vec3 hitPoint = origin + rayDir * hitDist;

    if (map(hitPoint) > 0.01 || hitDist > 100.0)
        discard;

    vec3 lightPos = vec3(7.0, -7.0, -7.0);
    float dif = GetLight(hitPoint, lightPos);
    vec3 color = vec3(dif);
 
    vec3 worldNormal = calcNormal(hitPoint);
    gNormal = EncodeNormal(vec3(worldNormal.x, -worldNormal.y, -worldNormal.z));
    
    vec4 invCamRot = vec4(-correctedCameraRot.xyz, correctedCameraRot.w);
    vec3 cam = rotateVectorByQuaternion(hitPoint - origin, invCamRot);
    float depth = UnlinearizeDepth(cam.z, 0.1, 100);
    
    gl_FragDepth = depth;
         
    gAlbedo = vec4(color, 1.0);
    gPosition = vec4(hitPoint, 1.0);
}