float random (vec2 st) {
    return fract(sin(dot(st.xy,
                         vec2(12.9898,78.233)))*
        43758.5453123);
}

void main() {
    vec4 norm = GetNormal();
    vec3 color = GetColor();
    
    gNormal = norm;
    float d = LinearizeDepth(gl_FragDepth, 0.1, 100.0);
    d = clamp(d / 100.0, 0.0, 1.0);
    //gAlbedo = vec4(vec3(d*5), 1.0);
    //gAlbedo =  vec4((DecodeNormal(norm) + 1) * 0.5, 1.0);
    gAlbedo = vec4(color, 1.0);
}