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

// Point and spot lights (decision 2026-11-09, the office at night: lamps give each room its
// own light). Off unless a scene turns them on: _GameLocalLights is a global the scene sets
// (LampLighting); unset, it is 0. The office leans on its lamps; the kitchen only warms with
// a few gentle ones (decision 2026-11-09). Lamps light the
// shapes naturally, a smooth falloff rather than toon bands (the user's call, 2026-11-09), and
// wrap a little round the sides facing away, as a room's bounce light would.
float _GameLocalLights;
// A trial (2026-11-11, the user's note: the city and the tower look "too 3D" beside the kitchen):
// above 0, the lamps' light falls in that many flat steps instead of a smooth falloff, as the
// sun's does in the three-band ramp. A global the scene sets (LampLighting); unset, it is 0 and
// nothing changes.
float _GameLampBands;

half3 LocalLights(half3 albedo, float3 positionWS, float3 normalWS, float4 positionCS)
{
#if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
    if (_GameLocalLights <= 0.0)
        return half3(0, 0, 0);
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    half3 sum = half3(0, 0, 0);
    uint lightsCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightsCount)
        Light light = GetAdditionalLight(lightIndex, positionWS);
        half wrap = saturate(dot(normalWS, light.direction) * 0.7 + 0.3);
        sum += light.color * (light.distanceAttenuation * wrap);
    LIGHT_LOOP_END
    sum *= _GameLocalLights;
    if (_GameLampBands > 0.5)
    {
        // Step the brightness, keep the colour. Steps are taken on b / (1 + b), which squeezes
        // any brightness into 0–1, so a lamp's bright core gets as many steps as its dim edge
        // instead of all of them crowding round the bulb. Each step's edge is a fixed sliver of a
        // step wide (no screen derivatives: this sits behind a branch).
        half brightness = max(dot(sum, half3(0.299, 0.587, 0.114)), 1e-4);
        half squeezed = brightness / (1.0 + brightness) * _GameLampBands;
        half stepped = (floor(squeezed) + smoothstep(0.44, 0.56, frac(squeezed))) / _GameLampBands;
        stepped = min(stepped, 0.88);
        sum *= stepped / (1.0 - stepped) / brightness;
    }
    return albedo * sum;
#else
    return half3(0, 0, 0);
#endif
}

// receiveShadows scales how much cast shadow lands on the surface: 1 for the set, 0 for the
// people. Small round bodies self-shadow into jagged acne teeth at the sun's shadow-map
// resolution (jackets, aprons, faces; the user's note, 2026-11-10), so characters cast shadows
// on the floor but take none themselves, as cartoon characters usually do.
half3 ToonShade(half3 albedo, float3 positionWS, float3 normalWS, float4 positionCS, float receiveShadows)
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
    float shadow = lerp(1.0, mainLight.shadowAttenuation, saturate(ndotl * 5.0) * receiveShadows);
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
    return color + rim + LocalLights(albedo, positionWS, normalWS, positionCS);
}

half3 ToonShade(half3 albedo, float3 positionWS, float3 normalWS, float4 positionCS)
{
    return ToonShade(albedo, positionWS, normalWS, positionCS, 1.0);
}

#endif
