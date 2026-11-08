// A plate that can be dirty: sauce smeared over it, greasy patches and crumbs, all drawn in
// object space from _Seed so that no two dirty plates look alike. _Dirt goes from 0 (clean) to 1
// (as left by a diner), so a plate in the sink can come clean as it's washed. The plate's origin
// is the centre of its base and its radius is _Radius, so the grime stays on the plate.
Shader "Game/Kitchen/Plate"
{
    Properties
    {
        [MainColor] _BaseColor("Plate", Color) = (0.97, 0.97, 0.95, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.65
        _Radius("Plate radius (m)", Float) = 0.17

        [Header(Grime)]
        _Dirt("Dirt (runtime: 0 clean, 1 dirty)", Range(0, 1)) = 0
        _Seed("Variation (runtime)", Float) = 0.5
        _SauceRed("Sauce: tomato", Color) = (0.62, 0.12, 0.06, 1)
        _SauceBrown("Sauce: gravy", Color) = (0.36, 0.2, 0.09, 1)
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
                float wobble = ValueNoise(p * 5.0 + offset) - 0.5;

                // Sauce: a puddle somewhere in the well, and a smear dragged away from it, like a
                // fork's wipe. Both have ragged edges. Red or brown, by the seed.
                float2 puddleCentre = (float2(Pick(1.0), Pick(2.0)) - 0.5) * 0.9;
                float puddleSize = lerp(0.24, 0.4, Pick(5.0));
                float puddle = 1.0 - smoothstep(puddleSize - 0.06, puddleSize, length(p - puddleCentre) + wobble * 0.3);
                float smearAngle = Pick(3.0) * 6.2831853;
                float2 along = float2(cos(smearAngle), sin(smearAngle));
                float2 fromPuddle = p - puddleCentre;
                float run = dot(fromPuddle, along);
                float across = dot(fromPuddle, float2(-along.y, along.x)) + sin(run * 6.0 + offset) * 0.06;
                float smear = (1.0 - smoothstep(0.09, 0.16, abs(across))) * smoothstep(0.0, 0.05, run)
                    * (1.0 - smoothstep(0.45, 0.75, run + wobble * 0.3));
                float sauce = saturate(max(puddle, smear * 0.85)) * (1.0 - smoothstep(0.82, 0.9, r)) * _SauceAmount;
                half3 sauceColor = lerp(_SauceRed.rgb, _SauceBrown.rgb, Pick(4.0));

                // Grease: soft, see-through patches.
                float grease = smoothstep(0.45, 0.7, ValueNoise(p * 2.2 + offset + 7.0)) * _GreaseAmount;

                // Crumbs: a few clusters of specks, mostly in the well, rather than an even scatter.
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
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = PlateColor(input.positionOS);
                surface.smoothness = _Smoothness;
                surface.occlusion = 1;
                surface.alpha = 1;
                surface.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
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
