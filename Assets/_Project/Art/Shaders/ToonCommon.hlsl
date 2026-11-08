// Shared cartoony shading (decision 2026-11-08): flat bands of light instead of smooth shading
// and a soft rim of light round the silhouette. Outlines are drawn over the screen by
// SH_InkOutline; the inverted-hull outline passes were removed (decision 2026-11-08, the user's
// call). Every toon shader includes this after its UnityPerMaterial buffer, which must declare
// _RimStrength. (_ShadowTint, _ToonEdge, _ToonSoftness, _OutlineColor and _OutlineWidth are no
// longer read; materials still carry them.)
#ifndef GAME_TOON_COMMON_INCLUDED
#define GAME_TOON_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Three-band ramp (proposed 2026-11-08, art direction): lit, mid and shadow, all in one place so
// objects, room surfaces, food and plates can't drift apart. The shadow band is cooler and a step
// deeper than the old _ShadowTint, for more separation between tops and fronts from the overhead
// camera. _ShadowTint on materials is no longer read.
static const half3 RampLit    = half3(1.00, 1.00, 1.00);
static const half3 RampMid    = half3(0.86, 0.84, 0.88);
static const half3 RampShadow = half3(0.66, 0.63, 0.72);
static const float RampShadowEdge = 0.42;   // half-Lambert value where shadow turns to mid
static const float RampLitEdge    = 0.62;   // and where mid turns to lit
static const float RampSoftness   = 0.015;  // half-width of each step; SMAA does the rest

half3 ToonRamp(float value)
{
    half toMid = smoothstep(RampShadowEdge - RampSoftness, RampShadowEdge + RampSoftness, value);
    half toLit = smoothstep(RampLitEdge - RampSoftness, RampLitEdge + RampSoftness, value);
    return lerp(lerp(RampShadow, RampMid, toMid), RampLit, toLit);
}

half3 ToonShade(half3 albedo, float3 positionWS, float3 normalWS, float4 positionCS)
{
    normalWS = normalize(normalWS);
    Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));

    // Half-Lambert, so faces turned slightly away from the sun land in the mid band instead of
    // dropping straight to shadow. Cast shadows push the value down into the shadow band; soft
    // shadow edges become a ring of mid band, which reads as an inked cartoon shadow.
    float ndotl = dot(normalWS, mainLight.direction);
    float facing = ndotl * 0.5 + 0.5;
    // Cast shadows only count on faces turned towards the sun. A face lit edge-on or from behind
    // is already in its darker band, and at grazing angles the shadow map aliases into jagged
    // teeth (seen on the fridge front, 2026-11-08), so its shadow fades out as it turns away.
    float shadow = lerp(1.0, mainLight.shadowAttenuation, saturate(ndotl * 5.0));
    float value = facing * shadow;
    half3 color = albedo * ToonRamp(value) * mainLight.color;

    // A little sky light, kept low so it doesn't smear gradients across the flat bands.
    color += albedo * SampleSH(normalWS) * 0.12;

    // Contact shadow from SSAO, banded to one flat step (needs _SCREEN_SPACE_OCCLUSION in the pass;
    // without the keyword this returns 1 and does nothing). A light step: at 0.78 the floor under
    // the counters went too dark (the user's note, 2026-11-08).
    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(positionCS));
    color *= lerp(0.87, 1.0, smoothstep(0.55, 0.7, ao.indirectAmbientOcclusion));

    // A soft rim on the lit side, which separates shapes from the floor behind them.
    float lit = smoothstep(RampLitEdge - RampSoftness, RampLitEdge + RampSoftness, value);
    float3 toCamera = GetWorldSpaceNormalizeViewDir(positionWS);
    float rim = pow(saturate(1.0 - dot(normalWS, toCamera)), 4.0) * _RimStrength * lit;
    return color + rim;
}

#endif
