// The clear glass over the tower's real rooms (the user's note, 2026-11-10: the windows looked "way
// too transparent"). A pane that reads as glass without hiding the room: a cool tint that veils it
// a little, pale reflections in diagonal bands, and more of both at a glancing angle. The bands
// are laid out in world space, so they run on across the whole face from pane to pane, like the sky
// in a real glass wall, instead of repeating in every window; they slide as the camera moves, and
// drift slowly on their own.
Shader "Game/PaneGlass"
{
    Properties
    {
        _Tint ("Tint (alpha: how much it veils the room)", Color) = (0.6, 0.74, 0.9, 0.24)
        _Sheen ("Reflection colour", Color) = (0.86, 0.92, 1, 1)
        _SheenStrength ("Reflection strength", Range(0, 1)) = 0.2
        _Scale ("Metres from one reflection to the next", Float) = 9
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _Sheen;
                half _SheenStrength;
                float _Scale;
            CBUFFER_END

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
                float fog : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // A soft-edged band of `width` starting at `from`, in a coordinate that repeats every 1.
            float Band(float s, float from, float width)
            {
                float t = frac(s - from);
                return smoothstep(0.0, 0.015, t) * (1.0 - smoothstep(width, width + 0.015, t));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 view = normalize(_WorldSpaceCameraPos - input.positionWS);
                float facing = saturate(abs(dot(n, view)));
                // Along the face (whichever way it looks) and up it, on a slant.
                float along = input.positionWS.x * n.z - input.positionWS.z * n.x;
                float slide = view.x * n.z - view.z * n.x; // the reflections move against the camera
                float s = (along + input.positionWS.y * 0.55) / _Scale + slide * 0.35 + _Time.y * 0.004;
                float sheen = Band(s, 0.0, 0.17) + Band(s, 0.26, 0.045) * 0.7 + Band(s, 0.62, 0.09) * 0.5;
                float glance = pow(1.0 - facing, 3.0);

                float veil = _Tint.a + glance * 0.35;
                float shine = sheen * _SheenStrength * (0.6 + 0.4 * glance);
                float alpha = saturate(veil + shine);
                half3 colour = (_Tint.rgb * veil + _Sheen.rgb * shine) / max(alpha, 1e-3);
                colour = MixFog(colour, input.fog);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
