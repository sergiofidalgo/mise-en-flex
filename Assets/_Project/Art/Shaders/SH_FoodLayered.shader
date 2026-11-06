// Layered food such as steak, as one mesh. The face looking up shows the crust; the edge shows
// three bands by height (bottom, core, top), so a cooked layer creeps in from the seared sides,
// as when a cook looks at a steak's side. IngredientView drives the colours from the simulation.
Shader "Game/Food/Layered"
{
    Properties
    {
        [MainColor] _BaseColor("Surface (driven at runtime; raw colour in the material)", Color) = (0.62, 0.12, 0.14, 1)
        _BandBottom("Band: bottom (runtime)", Color) = (0.62, 0.12, 0.14, 1)
        _BandCore("Band: core (runtime)", Color) = (0.62, 0.12, 0.14, 1)
        _BandTop("Band: top (runtime)", Color) = (0.62, 0.12, 0.14, 1)
        _Thickness("Mesh thickness (m)", Float) = 0.05
        _BandSoftness("Softness between bands", Range(0.01, 0.3)) = 0.08
        _MottleScale("Crust mottling scale (per metre)", Float) = 70
        _MottleStrength("Crust mottling strength", Range(0, 1)) = 0.3
        _Smoothness("Smoothness", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _BandBottom;
            half4 _BandCore;
            half4 _BandTop;
            float _Thickness;
            float _BandSoftness;
            float _MottleScale;
            float _MottleStrength;
            float _Smoothness;
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
                float3 normalOS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // Cheap value noise for the crust, needing no texture.
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

            half3 FoodColor(Varyings input)
            {
                // Edge: the three bands by height, blended so no hard lines show from a distance.
                float height = saturate(input.positionOS.y / max(_Thickness, 0.0001));
                half3 edge = lerp(_BandBottom.rgb, _BandCore.rgb,
                    smoothstep(1.0 / 3.0 - _BandSoftness, 1.0 / 3.0 + _BandSoftness, height));
                edge = lerp(edge, _BandTop.rgb,
                    smoothstep(2.0 / 3.0 - _BandSoftness, 2.0 / 3.0 + _BandSoftness, height));

                // Two octaves of noise, centred on 1, so mottling doesn't shift the overall colour:
                // the crust still reads as the simulation's browning.
                float mottle = 0.65 * ValueNoise(input.positionOS.xz * _MottleScale)
                    + 0.35 * ValueNoise(input.positionOS.xz * _MottleScale * 2.7);
                half3 crust = _BaseColor.rgb * (1.0 + (mottle - 0.5) * 2.0 * _MottleStrength);

                // Faces looking up show the crust; the bevel blends into the edge.
                return lerp(edge, crust, smoothstep(0.55, 0.9, normalize(input.normalOS).y));
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
                surface.albedo = FoodColor(input);
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
