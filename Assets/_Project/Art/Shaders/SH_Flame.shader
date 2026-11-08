// A gas flame tongue (the user's note, 2026-11-08: more realistic, less solid): unlit and
// additive, so overlapping tongues brighten each other like real fire. Bright at its base in
// _BaseColor, turning to _TipColor towards its tip and fading out there, and fading at its
// silhouette too (where the surface turns side-on to the camera), so it reads as glowing gas
// rather than a solid shape. The mesh is _Height tall from its origin; views scale it and set
// both colours per tongue with the heat.
Shader "Game/FX/Flame"
{
    Properties
    {
        _BaseColor("Base", Color) = (0.25, 0.5, 1, 1)
        _TipColor("Tip", Color) = (1, 0.5, 0.12, 1)
        _Height("Mesh height (m, unscaled)", Float) = 0.1
        _Intensity("Brightness", Range(0, 4)) = 1.6
        _EdgeSoftness("Edge softness", Range(0.1, 4)) = 1.4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Flame"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                float _Height;
                float _Intensity;
                float _EdgeSoftness;
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
                float height : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.height = input.positionOS.y / _Height;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float h = saturate(input.height);
                float3 toCamera = GetWorldSpaceNormalizeViewDir(input.positionWS);
                // Facing the camera: full; side-on: none. Double-sided, so either side counts.
                float facing = pow(saturate(abs(dot(normalize(input.normalWS), toCamera))), _EdgeSoftness);
                half3 color = lerp(_BaseColor.rgb, _TipColor.rgb, smoothstep(0.3, 0.95, h));
                // Strongest just above the base, gone at the tip.
                float alpha = facing * smoothstep(0.0, 0.12, h) * (1.0 - smoothstep(0.5, 1.0, h));
                return half4(color * _Intensity, alpha);
            }
            ENDHLSL
        }
    }
}
