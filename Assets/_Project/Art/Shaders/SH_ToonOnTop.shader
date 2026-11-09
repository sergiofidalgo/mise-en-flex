// The house toon shading (ToonCommon.hlsl, as Game/Toon) for station interfaces built from 3D
// pieces that must stay readable over whatever stands in front of them: the chef using the
// station, another player, the sink's tap (decision 2026-11-09). It ignores depth and draws late,
// so the pieces cover everything; the pieces are simple solid shapes with back faces culled, so
// each still shades right on its own, and views order them with each renderer's sortingOrder
// (ring, then target, then the soap, then the stick hint's parts from the base up).
// _BaseColor is set per renderer, as with Game/Toon.
Shader "Game/ToonOnTop"
{
    Properties
    {
        [MainColor] _BaseColor("Colour", Color) = (0.86, 0.27, 0.2, 1)
        _RimStrength("Rim light", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+400" "IgnoreProjector" = "True" }

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
            ZTest Always
            ZWrite Off
            Cull Back

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
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
