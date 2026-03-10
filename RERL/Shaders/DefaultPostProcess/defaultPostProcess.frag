#version 330 core

out vec4 FragColor;

in vec2 TexCoords;

uniform sampler2D uColor;
uniform sampler2D uNormal;
uniform sampler2D uDepth;

float random (vec2 st) {
    return fract(sin(dot(st.xy,
                         vec2(12.9898,78.233)))*
        43758.5453123);
}

float smin(float a, float b, float k) {
	float h = clamp(0.5 + 0.5*(b-a)/k, 0.0, 1.0 );
	return mix(b, a, h) - k*h*(1.0 - h);
	//return -log(exp(-a/k) + exp(-b/k))*k;
}

float MakeBox( vec3 p, vec3 b )
{
    vec3 q = abs(p) - b;
    return length(max(q,0.0)) + min(max(q.x,max(q.y,q.z)),0.0);
}

float MakeSphere( vec3 rayPoint, vec3 spherePos, float sphereR )
{
    return length(rayPoint - spherePos) - sphereR;
} 

float FloorSDF(vec3 rayPoint, float floorHeight) {
    return rayPoint.y - floorHeight;
}

float mergerSponge(in vec3 p) {
    float d = MakeBox(p, vec3(2));

    float s = 3.0;
    for (int m = 0; m < 3; m++)
    {
        vec3 a = mod(p*s, 2.0)-1.0;
        s *=3.0;
        vec3 r = abs(2.0 - 3.0*abs(a));
        float da = max(r.x,r.y);
        float db = max(r.y,r.z);
        float dc = max(r.z,r.x);
        float c = (min(da,min(db,dc))-1.0)/s;
        if( c>d )
        {
            d = c;
            //res = vec3( d, 0.2*da*db*dc, (1.0+float(m))/4.0, 0.0 );
        }
    }
    return d;
}

float sphereR = 0.1;
vec3 spherePos;

float sphere2R = 0.25;
vec3 sphere2Pos = vec3(0.0, 0.0, 3.0);

vec3 objectPos = vec3(1.0, 0.0, 1.5);

float map(vec3 rayPoint) {
    return mergerSponge(rayPoint);
}

vec3 calcNormal(vec3 rayPoint) {
    vec2 e = vec2(1.0, -1.0) * 0.0005;
    return normalize(
        e.xyy * map(rayPoint + e.xyy) +
        e.yyx * map(rayPoint + e.yyx) +
        e.yxy * map(rayPoint + e.yxy) +
        e.xxx * map(rayPoint + e.xxx));
}

float RayMarch(vec3 cameraPos, vec3 rayDir, int numberOfSteps) {
    float distanceTraveled = 0.0;
    vec3 rayPoint;
    for(int i = 0; i < numberOfSteps; i++) {
        rayPoint = cameraPos + distanceTraveled * rayDir;
        
        //if(map(rayPoint) < 0.001) {
        //    rayColor = vec3(calcNormal(rayPoint) * 0.5+0.5);
        //}
        
        if(distanceTraveled > 1000.0) {
            break;
        }
        
        distanceTraveled += map(rayPoint);
    }
    return distanceTraveled;
}

float GetLight(vec3 rayPoint, vec3 lightPos) {
    vec3 light = normalize(lightPos - rayPoint);
    vec3 normal = calcNormal(rayPoint);
    
    float dif = dot(normal, light);
    dif = clamp(dif, 0.0, 1.0);
    
    float shadows = RayMarch(rayPoint+normal*0.01*2.0, light, 300);
    
    if(shadows < length(lightPos - rayPoint)) {
        dif *= 0.1;
    }
    
    return dif;
}

vec3 sphereNormal(vec3 spherePos, vec3 hitPoint) {
    return normalize(spherePos - hitPoint);
}

void main()
{
    vec2 uv  = gl_FragCoord.xy / vec2(1920.0, 1080.0);
    vec2 cuv = uv * 2.0 - vec2(1.0);
    cuv.y   *= 1080.0 / 1920.0;
    
    vec3 cameraPos = vec3(0.0, 0.0, -7.0);
    
    vec3 rayPoint;
    vec3 rayColor;
    vec3 rayDir = normalize(vec3(cuv.x, cuv.y, 1.0));
    
    float distanceTraveled = 0.0;

    vec3 lightPos = vec3(1, 0.0, -5.0);
    
    int numberOfSteps = 300;
    
    float raymarch = RayMarch(cameraPos, rayDir, numberOfSteps);
    
    rayPoint = cameraPos + rayDir * raymarch;
    float dif = GetLight(rayPoint, lightPos);
    raymarch *= 0.2;
    rayColor = vec3(dif);

    vec3 norm = texture(uNormal, TexCoords).rgb;
    float depth = texture(uDepth, TexCoords).r;
    vec3 color = texture(uColor, TexCoords).rgb;
    
    FragColor = vec4( map(rayPoint) < 0.01 ? rayColor : color, 1.0);
}
