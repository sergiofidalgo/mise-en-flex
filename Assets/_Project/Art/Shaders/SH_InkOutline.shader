// Ink outline as a screen pass (art direction, 2026-11-08). Unity 6.3 / URP 17.3, Forward+.
// Replaces the inverted-hull Outline passes: at the kitchen shot (~9.6 m, FOV 45) a 0.0035 m hull
// is about half a pixel wide, so it barely shows. This draws a constant on-screen width instead,
// on every opaque (room surfaces included), from the DepthNormals passes the shaders already have.
// Use with a Full Screen Pass Renderer Feature:
//   Injection Point: Before Rendering Post Processing
//   Fetch Color Buffer: ON   Bind Depth-Stencil: OFF
//   Requirements: Depth + Normal  (this turns on the DepthNormals prepass, which SSAO shares)
// Perspective cameras only (LinearEyeDepth). Transparent queue and UI are never inked.
// Softened after the user played it (2026-11-08): a thinner line, capped in pixels so a Retina
// screen doesn't double it; drawn as a darker shade of what's underneath rather than one fixed
// brown, so wood edges read as darker wood and the white chef gets a soft brown-grey line; and a
// high normal threshold, so silhouettes carry the look and creases (doors, lips, grooves) mostly don't ink.
Shader "Game/Post/InkOutline"
{
    Properties
    {
        _InkColor        ("Ink Color", Color) = (0.2, 0.13, 0.09, 1)   // HouseStyle.OutlineColor, #332117
        _InkColorMix     ("Ink Color Mix (0 = shade of the scene, 1 = Ink Color)", Range(0, 1)) = 0.25
        _InkShade        ("Ink Shade (scene colour multiplier)", Range(0, 1)) = 0.45
        _InkOpacity      ("Ink Opacity", Range(0, 1)) = 0.5
        _Thickness       ("Thickness @1080p (px)", Range(0.5, 4)) = 1
        _MaxThickness    ("Max Thickness (px)", Range(1, 4)) = 2
        _DepthThreshold  ("Depth Threshold (relative)", Range(0.002, 0.2)) = 0.015
        _NormalThreshold ("Normal Threshold", Range(0.1, 2)) = 1.1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "InkOutline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            half4 _InkColor;
            float _InkColorMix;
            float _InkShade;
            float _InkOpacity;
            float _Thickness;
            float _MaxThickness;
            float _DepthThreshold;
            float _NormalThreshold;

            float EyeDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // Width scales with the screen, between 1 px and a cap, so a Retina screen doesn't
                // double the line (1 px at 1080p, 2 px at most).
                float px = clamp(_Thickness * _ScaledScreenParams.y / 1080.0, 1.0, _MaxThickness);
                float2 o = (px * 0.5) / _ScaledScreenParams.xy;

                // Roberts cross: 4 taps, cheap enough for Steam Deck / integrated Mac GPUs
                float2 uvA = uv + float2(-o.x, -o.y);
                float2 uvB = uv + float2( o.x,  o.y);
                float2 uvC = uv + float2( o.x, -o.y);
                float2 uvD = uv + float2(-o.x,  o.y);

                // Depth edges, relative to distance so thresholds hold anywhere in frame
                float dA = EyeDepth(uvA);
                float dB = EyeDepth(uvB);
                float dC = EyeDepth(uvC);
                float dD = EyeDepth(uvD);
                float dCenter = max(EyeDepth(uv), 0.001);
                float d1 = dB - dA;
                float d2 = dD - dC;
                float depthDelta = sqrt(d1 * d1 + d2 * d2) / dCenter;
                float depthEdge = smoothstep(_DepthThreshold, _DepthThreshold * 1.5, depthDelta);

                // Normal edges: creases, counter lips, plate rims (anything over ~25 degrees)
                float3 nA = SampleSceneNormals(uvA);
                float3 nB = SampleSceneNormals(uvB);
                float3 nC = SampleSceneNormals(uvC);
                float3 nD = SampleSceneNormals(uvD);
                float3 n1 = nB - nA;
                float3 n2 = nD - nC;
                float normalDelta = sqrt(dot(n1, n1) + dot(n2, n2));
                float normalEdge = smoothstep(_NormalThreshold, _NormalThreshold * 1.3, normalDelta);

                // The ink is a darker shade of what's underneath, with a little of the house brown.
                float edge = saturate(max(depthEdge, normalEdge)) * _InkOpacity;
                half3 ink = lerp(scene.rgb * _InkShade, _InkColor.rgb, _InkColorMix);
                return half4(lerp(scene.rgb, ink, edge), scene.a);
            }
            ENDHLSL
        }
    }
}
