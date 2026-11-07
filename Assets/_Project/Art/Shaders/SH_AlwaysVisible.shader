// For small indicators that must never be hidden, such as a hob's heat pips: drawn after the rest
// of the scene and ignoring depth, so they show through a chef standing between them and the
// camera. Unlit so their colour reads the same everywhere; top faces are slightly brighter, so a
// pip still looks like a solid object rather than a flat sticker.
Shader "Game/AlwaysVisible"
{
    Properties
    {
        [MainColor] _BaseColor("Colour (driven at runtime)", Color) = (0.12, 0.12, 0.13, 1)
        _SideShade("Brightness of side faces", Range(0, 1)) = 0.7
    }

    SubShader
    {
        // After transparent objects (3000), so nothing in the scene draws over them.
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Overlay" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            ZTest Always
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _SideShade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float top = saturate(normalize(input.normalWS).y);
                return half4(_BaseColor.rgb * lerp(_SideShade, 1.0, top), 1);
            }
            ENDHLSL
        }
    }
}
