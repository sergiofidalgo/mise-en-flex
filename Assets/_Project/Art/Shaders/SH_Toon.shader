// A flat-coloured cartoony surface with an outline (see ToonCommon.hlsl). _BaseColor can be set
// per renderer by views (heat, highlights), as with URP Lit. With _WoodGrain above 0 it draws
// bands of wood grain along the object's x, in _GrainColor, for boards and handles.
Shader "Game/Toon"
{
    Properties
    {
        [MainColor] _BaseColor("Colour", Color) = (0.86, 0.27, 0.2, 1)
        _ShadowTint("Shadow tint (multiplies the colour)", Color) = (0.76, 0.66, 0.64, 1)
        _ToonEdge("Light edge", Range(-1, 1)) = 0.1
        _ToonSoftness("Light edge softness", Range(0.001, 0.5)) = 0.04
        _RimStrength("Rim light", Range(0, 1)) = 0.2
        _OutlineColor("Outline", Color) = (0.2, 0.13, 0.09, 1)
        _OutlineWidth("Outline width (m)", Range(0, 0.05)) = 0.012

        [Header(Wood grain)]
        _WoodGrain("Wood grain (0 off)", Range(0, 1)) = 0
        _GrainColor("Grain", Color) = (0.62, 0.4, 0.2, 1)
        _GrainScale("Grain bands per metre", Float) = 30
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadowTint;
            float _ToonEdge;
            float _ToonSoftness;
            float _RimStrength;
            half4 _OutlineColor;
            float _OutlineWidth;
            float _WoodGrain;
            half4 _GrainColor;
            float _GrainScale;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

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
                float3 positionOS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), f.x), f.y);
            }

            half3 Albedo(float3 positionOS)
            {
                if (_WoodGrain <= 0.0)
                    return _BaseColor.rgb;

                // Grain runs along x: wavy bands across z, bent by noise so they read as wood, with
                // the odd darker streak. Flat bands, to suit the cartoony shading.
                float2 p = positionOS.xz;
                float wave = ValueNoise(float2(p.x * 3.0, p.y * 6.0)) * 1.6 + ValueNoise(p * 12.0) * 0.3;
                float bands = frac(p.y * _GrainScale + wave);
                float grain = smoothstep(0.55, 0.62, bands) * (1.0 - smoothstep(0.85, 0.92, bands));
                return lerp(_BaseColor.rgb, _GrainColor.rgb, grain * _WoodGrain);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half3 color = ToonShade(Albedo(input.positionOS), input.positionWS, input.normalWS, input.positionCS);
                return half4(MixFog(color, input.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            float4 Vertex(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half Fragment() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

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
                return half4(NormalizeNormalPerPixel(input.normalWS), 0);
            }
            ENDHLSL
        }
    }
}
