// Shared cartoony shading (decision 2026-11-08): flat bands of light instead of smooth shading, a
// warm tinted shadow side, a soft rim of light round the silhouette, and a thin outline drawn by a
// second, inflated pass with its front faces culled ("inverted hull"). Every toon shader includes
// this after its UnityPerMaterial buffer, which must declare the toon properties used below:
// _ShadowTint, _ToonEdge, _ToonSoftness, _RimStrength, _OutlineColor and _OutlineWidth.
#ifndef GAME_TOON_COMMON_INCLUDED
#define GAME_TOON_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Two bands: lit and shadow, with a narrow soft step between, so shapes read as flat cartoon
// colour. Shadows from other objects fall into the same shadow band.
half3 ToonShade(half3 albedo, float3 positionWS, float3 normalWS, float4 positionCS)
{
    normalWS = normalize(normalWS);
    Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
    float facing = dot(normalWS, mainLight.direction);
    float lit = min(smoothstep(_ToonEdge - _ToonSoftness, _ToonEdge + _ToonSoftness, facing),
        mainLight.shadowAttenuation);

    half3 shadowColor = albedo * _ShadowTint.rgb;
    half3 color = lerp(shadowColor, albedo, lit) * mainLight.color;

    // A little sky light so the shadow side is never black.
    color += albedo * SampleSH(normalWS) * 0.25;

    // A soft rim on the lit side, which separates shapes from the floor behind them.
    float3 toCamera = GetWorldSpaceNormalizeViewDir(positionWS);
    float rim = pow(saturate(1.0 - dot(normalWS, toCamera)), 4.0) * _RimStrength * lit;
    return color + rim;
}

// The outline pass: the mesh pushed out along its normals, in the outline colour, with its front
// faces culled so only a rim shows behind the real surface. Width is in metres.
struct ToonOutlineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
};

float4 ToonOutlineVertex(ToonOutlineAttributes input) : SV_POSITION
{
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
    return TransformWorldToHClip(positionWS + normalWS * _OutlineWidth);
}

half4 ToonOutlineFragment() : SV_Target
{
    return half4(_OutlineColor.rgb, 1);
}

#endif
