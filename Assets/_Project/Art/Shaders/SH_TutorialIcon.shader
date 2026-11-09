// Tutorial glyphs built from 3D pieces (the wash game's thumbstick hint, decision 2026-11-09):
// "scene agnostic", so they read the same in every room and at every hour. The scene's lights
// never touch them: a fixed light from the upper left, in view space, gives two flat tones and a
// soft rim, and each piece carries its own dark outline (a shell pushed out from the piece's
// centre, drawn first). Both passes ignore depth and draw late, so the glyph sits over the
// scene; views order the pieces with sortingOrder. _BaseColor is set per renderer.
Shader "Game/UI/TutorialIcon"
{
    Properties
    {
        [MainColor] _BaseColor("Colour", Color) = (0.36, 0.37, 0.42, 1)
        _ShadeStrength("Shade (share of the colour kept in shadow)", Range(0, 1)) = 0.72
        _LightEdge("Light edge", Range(-1, 1)) = 0.15
        _RimStrength("Rim light", Range(0, 1)) = 0.25
        _OutlineColor("Outline", Color) = (0.12, 0.11, 0.13, 1)
        _OutlineWidth("Outline width (m)", Range(0, 0.02)) = 0.006
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+450" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _ShadeStrength;
            float _LightEdge;
            float _RimStrength;
            half4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        // The outline: the piece pushed out from its own centre and drawn in the outline colour,
        // first, so the fill covers all but a rim of it. Works for the simple, roughly convex
        // pieces glyphs are made of.
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZTest Always
            ZWrite Off
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            float4 Vertex(float4 positionOS : POSITION) : SV_POSITION
            {
                float3 centre = TransformObjectToWorld(float3(0, 0, 0));
                float3 world = TransformObjectToWorld(positionOS.xyz);
                float3 away = world - centre;
                world += (dot(away, away) > 1e-8 ? normalize(away) : float3(0, 1, 0)) * _OutlineWidth;
                return TransformWorldToHClip(world);
            }

            half4 Fragment() : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Fill"
            Tags { "LightMode" = "UniversalForward" }
            ZTest Always
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalVS : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(input.normalOS));
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalVS);
                // View space looks down -z: this light comes from the upper left, in front.
                float3 light = normalize(float3(-0.45, 0.75, 0.5));
                float ndl = dot(n, light);
                float aa = max(fwidth(ndl), 1e-3);
                float lit = smoothstep(_LightEdge - aa, _LightEdge + aa, ndl);
                half3 color = _BaseColor.rgb * lerp(_ShadeStrength, 1.0, lit);
                float rim = pow(saturate(1.0 - n.z), 3.0) * _RimStrength * lit;
                return half4(color + rim, 1);
            }
            ENDHLSL
        }
    }
}
