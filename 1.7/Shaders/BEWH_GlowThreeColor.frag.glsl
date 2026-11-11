#version 450

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _DrawColor;      // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _DrawColorTwo;   // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _DrawColorThree; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _MainTex_ST;     // @default = vec4(0.0, 0.0, 1.0, 1.0)
} uMaterial;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _MaskTex; // @default = black

// Lit areas are brightened, whitened a little and get a soft halo, so the glow also shows where nothing
// is dark: in daylight and in portraits, which are drawn without the light overlay.
const float Brightness = 1.5;
const float HotCore = 0.15;
const float HaloStrength = 0.9;
const float HaloBrightness = 1.8;
const float HaloInnerRadius = 2.0; // texels
const float HaloOuterRadius = 5.0; // texels

// The lit colour at uv, with how much of the pixel is lit in its alpha. A colour that should not glow
// arrives with zero alpha, so only the lit mask channels add up here.
vec4 LitTint(vec2 uv)
{
    vec4 mask = texture(_MaskTex, uv);
    mask.rgb *= clamp(mask.a * 255.0, 0.0, 1.0);

    vec4 tint = mask.r * uMaterial._DrawColor
              + mask.g * uMaterial._DrawColorTwo
              + mask.b * uMaterial._DrawColorThree;
    tint.a *= texture(_MainTex, uv).a;
    return tint;
}

void main()
{
    vec2 uv = vUv * uMaterial._MainTex_ST.zw + uMaterial._MainTex_ST.xy;
    vec2 texel = 1.0 / vec2(textureSize(_MaskTex, 0));

    vec4 tint = LitTint(uv);
    vec4 f = texture(_MainTex, uv);

    // Mask coverage goes into the alpha, so it is divided back out of the colour.
    float coreAlpha = tint.a;
    vec3 coreColour = coreAlpha > 0.0 ? min(f.rgb * tint.rgb / coreAlpha * Brightness, vec3(1.0)) : vec3(0.0);
    coreColour += (1.0 - coreColour) * HotCore * f.rgb;

    // Average of the lit colour on two rings around the pixel.
    vec4 halo = vec4(0.0);
    for (int i = 0; i < 12; i++)
    {
        float angle = 6.2831853 * float(i) / 12.0;
        vec2 direction = vec2(cos(angle), sin(angle)) * texel;
        vec4 inner = LitTint(uv + direction * HaloInnerRadius);
        vec4 outer = LitTint(uv + direction * HaloOuterRadius);
        halo += vec4(inner.rgb * inner.a, inner.a) + vec4(outer.rgb * outer.a, outer.a) * 0.5;
    }
    halo /= 18.0;
    float haloAlpha = min(halo.a * HaloStrength, 1.0);
    vec3 haloColour = halo.a > 0.0 ? min(halo.rgb / halo.a * HaloBrightness, vec3(1.0)) : vec3(0.0);

    // The lit pixel over its halo.
    float alpha = coreAlpha + haloAlpha * (1.0 - coreAlpha);
    if (alpha <= 0.0)
        discard;

    vec3 colour = (coreColour * coreAlpha + haloColour * haloAlpha * (1.0 - coreAlpha)) / alpha;
    outColor = vec4(colour, alpha);
}
