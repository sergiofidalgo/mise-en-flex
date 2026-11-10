// A lamp's glow (the user's note, 2026-11-10: "some ambient lights are solid circles with no glow
// effect"): a soft halo round a bulb, added to whatever is behind it. Put on a quad at the bulb;
// the quad is turned to the camera here, in the vertex stage, and sized by its own scale, so it
// needs no script and costs next to nothing however many there are. Not for static batching:
// the quad must keep its own transform.
Shader "Game/Halo"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 0.85, 0.55, 1)
        _Strength ("Strength", Range(0, 3)) = 0.9
        _Core ("Core size", Range(0.02, 0.6)) = 0.16
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Strength;
                half _Core;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // The quad's middle in the world, and its size (the length of its x axis).
                float3 centre = TransformObjectToWorld(float3(0, 0, 0));
                float size = length(TransformObjectToWorld(float3(1, 0, 0)) - centre);
                // Out from the middle along the camera's right and up: always facing it.
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 positionWS = centre + (right * input.positionOS.x + up * input.positionOS.y) * size;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.uv - 0.5) * 2.0;
                // A bright core, then a long soft fall to nothing at the quad's edge.
                float core = exp(-r * r / (_Core * _Core));
                float bloom = pow(saturate(1.0 - r), 2.2) * 0.45;
                half3 colour = _BaseColor.rgb * (core + bloom) * _Strength;
                // Haze dims a glow; it doesn't tint it.
                colour *= saturate(ComputeFogIntensity(input.fog));
                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }
}
