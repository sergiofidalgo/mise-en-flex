// The house toon shading (ToonCommon.hlsl) for dirt lying on the floor, tables and counters
// (decision 2026-11-11: stains a little translucent, so the floor shows through like a wet film).
// Blended by _BaseColor's alpha, depth-tested but not written, drawn just after the opaque scene so
// it lies on what's under it; the pieces of one stain are ordered by each renderer's sortingOrder
// (rim, body, sheen, highlight). Shadows still darken it, as they do the floor.
Shader "Game/ToonStain"
{
    Properties
    {
        [MainColor] _BaseColor("Colour", Color) = (0.62, 0.38, 0.18, 0.7)
        _RimStrength("Rim light", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-50" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _RimStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZTest LEqual
            ZWrite Off
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "ToonCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half3 color = ToonShade(_BaseColor.rgb, input.positionWS, input.normalWS, input.positionCS);
                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
