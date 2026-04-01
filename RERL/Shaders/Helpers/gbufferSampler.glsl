// ======================================================
// Rocket Engine - gbufferSampler.glsl
// ======================================================

in vec2 UV;

uniform sampler2D uColor;
uniform sampler2D uNormal;
uniform sampler2D uDepth;

vec3 GetColor() {
    return texture(uColor, UV).rgb;
}

vec4 GetNormal() {
    return texture(uNormal, UV).rgba;
}

float GetDepth() {
    return texture(uDepth, UV).r;
}

// gbufferSampler.glsl END
