// A plate that can be dirty: streaky smears, splatter, a tide mark, grease and crumbs, all drawn in
// object space from _Seed so that no two dirty plates look alike. _Dirt goes from 0 (clean) to 1
// (as left by a diner), so a plate in the sink can come clean as it's washed. The plate's origin
// is the centre of its base and its radius is _Radius, so the grime stays on the plate.
Shader "Game/Kitchen/Plate"
{
    Properties
    {
        [Header(Toon)]
        _ShadowTint("Shadow tint (multiplies the colour)", Color) = (0.62, 0.6, 0.78, 1)
        _ToonEdge("Light edge", Range(-1, 1)) = 0.1
        _ToonSoftness("Light edge softness", Range(0.001, 0.5)) = 0.04
        _RimStrength("Rim light", Range(0, 1)) = 0.15
        _OutlineColor("Outline", Color) = (0.12, 0.09, 0.1, 1)
        _OutlineWidth("Outline width (m)", Range(0, 0.05)) = 0.005
        [MainColor] _BaseColor("Plate", Color) = (0.97, 0.97, 0.95, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.65
        _Radius("Plate radius (m)", Float) = 0.17

        [Header(Grime)]
        _Dirt("Dirt (runtime: 0 clean, 1 dirty)", Range(0, 1)) = 0
        _Seed("Variation (runtime)", Float) = 0.5
        _SauceRed("Sauce: orange", Color) = (0.7, 0.4, 0.16, 1)
        _SauceBrown("Sauce: brown", Color) = (0.46, 0.34, 0.22, 1)
        _GreaseColor("Grease", Color) = (0.86, 0.72, 0.38, 1)
        _CrumbColor("Crumbs", Color) = (0.3, 0.19, 0.1, 1)
        _SauceAmount("Sauce", Range(0, 1)) = 1
        _GreaseAmount("Grease", Range(0, 1)) = 0.7
        _CrumbAmount("Crumbs", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Smoothness;
            float _Radius;
            float _Dirt;
            float _Seed;
            half4 _SauceRed;
            half4 _SauceBrown;
            half4 _GreaseColor;
            half4 _CrumbColor;
            float _SauceAmount;
            float _GreaseAmount;
            float _CrumbAmount;
            half4 _ShadowTint;
            float _ToonEdge;
            float _ToonSoftness;
            float _RimStrength;
            half4 _OutlineColor;
            float _OutlineWidth;
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
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

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), f.x),
                    f.y);
            }

            // A random 0..1 for this plate and a channel, from the seed.
            float Pick(float channel)
            {
                return frac(sin((_Seed + channel) * 91.345) * 47453.5453);
            }

            half3 PlateColor(float3 positionOS)
            {
                // Across the plate, -1..1, whichever way it's turned or stood.
                float2 p = positionOS.xz / _Radius;
                float r = length(p);
                float offset = _Seed * 41.0;

                // Smears: streaky wipes, as a fork or bread leaves them, in a few patches. Thin
                // broken lines rather than solid shapes, so a dirty plate never reads as food.
                float angle = Pick(3.0) * 6.2831853;
                float2 along = float2(cos(angle), sin(angle));
                float across = dot(p, float2(-along.y, along.x)) + (ValueNoise(p * 3.0 + offset) - 0.5) * 0.5;
                float lines = abs(frac(across * 3.0 + ValueNoise(p * 2.0 + offset) * 0.4) - 0.5);
                // Broken into short dabs along their length, so they read as wipes, not contour lines.
                float dabs = smoothstep(0.45, 0.6, ValueNoise(float2(dot(p, along) * 6.0, across * 3.0) + offset + 17.0));
                float streaks = (1.0 - smoothstep(0.05, 0.16, lines)) * dabs
                    * smoothstep(0.4, 0.65, ValueNoise(p * 2.5 + offset + 5.0))
                    * (1.0 - smoothstep(0.75, 0.88, r));

                // Splatter: small drops, gathered in clusters.
                float drops = smoothstep(0.82, 0.88, ValueNoise(p * 14.0 + offset + 9.0))
                    * smoothstep(0.45, 0.7, ValueNoise(p * 3.0 + offset + 13.0)) * (1.0 - smoothstep(0.8, 0.9, r));

                // A faint tide mark round the well.
                float ringAt = lerp(0.55, 0.7, Pick(6.0));
                float ring = (1.0 - smoothstep(0.0, 0.05, abs(r - ringAt + (ValueNoise(p * 4.0 + offset) - 0.5) * 0.06)))
                    * smoothstep(0.3, 0.6, ValueNoise(p * 2.0 + offset + 21.0));

                float sauce = saturate(max(max(streaks * 0.65, drops), ring * 0.45)) * _SauceAmount;
                half3 sauceColor = lerp(_SauceRed.rgb, _SauceBrown.rgb, Pick(4.0));

                // Grease: soft, see-through patches.
                float grease = smoothstep(0.45, 0.7, ValueNoise(p * 2.2 + offset + 7.0)) * _GreaseAmount;

                // Crumbs: a few clusters of specks, mostly in the well.
                float clusters = smoothstep(0.6, 0.8, ValueNoise(p * 3.0 + offset + 11.0));
                float specks = smoothstep(0.86, 0.9, ValueNoise(p * 22.0 + offset + 3.0));
                float crumbs = specks * clusters * (1.0 - smoothstep(0.6, 0.8, r)) * _CrumbAmount;

                half3 color = _BaseColor.rgb;
                color = lerp(color, _GreaseColor.rgb, grease * 0.5 * _Dirt);
                color = lerp(color, sauceColor, sauce * _Dirt);
                color = lerp(color, _CrumbColor.rgb, crumbs * _Dirt);
                return color;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                // Cartoony flat shading (ToonCommon.hlsl, decision 2026-11-08).
                half3 color = ToonShade(PlateColor(input.positionOS), input.positionWS, input.normalWS, input.positionCS);
                return half4(MixFog(color, input.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex ToonOutlineVertex
            #pragma fragment ToonOutlineFragment
            #include "ToonCommon.hlsl"
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
