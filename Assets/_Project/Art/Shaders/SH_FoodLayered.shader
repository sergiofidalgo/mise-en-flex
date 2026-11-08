// Layered food such as steak, as one mesh. The face looking up shows the crust; the edge shows
// three bands by height (bottom, core, top), so a cooked layer creeps in from the seared sides,
// as when a cook looks at a steak's side. IngredientView drives the colours from the simulation.
// The top also has fat, generated from _Seed so that no two pieces look the same. The mesh's UV x
// is how far a point is from the centre to the outline (0..1), which places fat round the edge.
Shader "Game/Food/Layered"
{
    Properties
    {
        [Header(Toon)]
        _ShadowTint("Shadow tint (multiplies the colour)", Color) = (0.76, 0.66, 0.64, 1)
        _ToonEdge("Light edge", Range(-1, 1)) = 0.1
        _ToonSoftness("Light edge softness", Range(0.001, 0.5)) = 0.04
        _RimStrength("Rim light", Range(0, 1)) = 0.15
        _OutlineColor("Outline", Color) = (0.2, 0.13, 0.09, 1)
        _OutlineWidth("Outline width (m)", Range(0, 0.05)) = 0.0035
        [MainColor] _BaseColor("Surface (driven at runtime; raw colour in the material)", Color) = (0.62, 0.12, 0.14, 1)
        _BandBottom("Band: bottom (runtime)", Color) = (0.62, 0.12, 0.14, 1)
        _BandCore("Band: core (runtime)", Color) = (0.62, 0.12, 0.14, 1)
        _BandTop("Band: top (runtime)", Color) = (0.62, 0.12, 0.14, 1)
        _Thickness("Mesh thickness (m)", Float) = 0.05
        _BandSoftness("Softness between bands", Range(0.01, 0.3)) = 0.08
        _MottleScale("Crust mottling scale (per metre)", Float) = 70
        _MottleStrength("Crust mottling strength", Range(0, 1)) = 0.3
        _Smoothness("Smoothness", Range(0, 1)) = 0.2

        [Header(Fat)]
        _FatRaw("Fat: raw", Color) = (0.89, 0.72, 0.65, 1)
        _FatCooked("Fat: cooked, holds this while the lean browns", Color) = (0.86, 0.72, 0.46, 1)
        _MarbleCookedStrength("Marbling: how much shows once cooked", Range(0, 1)) = 0.35
        _EdgeSwell("Edge fat: how far it swells onto the top", Range(0, 1)) = 0.4
        _EdgeCoverage("Edge fat: how much of the outline it covers", Range(0, 1)) = 0.75
        _SeamAmount("Seam: curve of fat through the meat", Range(0, 1)) = 0.9
        _MarbleAmount("Marbling strength", Range(0, 1)) = 0.6
        _MarbleScale("Marbling scale (per metre)", Float) = 85
        _Seed("Variation (runtime)", Float) = 0.37
        _Browning("Browning as a fraction of burnt (runtime)", Float) = 0
        _CrustBottom("Underside crust (runtime)", Color) = (0.62, 0.12, 0.14, 1)
        _BrowningBottom("Underside browning as a fraction of burnt (runtime)", Float) = 0
        _SearLine("Seared line along the edge, as a share of its height", Range(0, 0.5)) = 0.22
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
            half4 _FatRaw;
            half4 _FatCooked;
            float _MarbleCookedStrength;
            float _EdgeSwell;
            float _EdgeCoverage;
            float _SeamAmount;
            float _MarbleAmount;
            float _MarbleScale;
            float _Seed;
            float _Browning;
            half4 _CrustBottom;
            float _BrowningBottom;
            float _SearLine;
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
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 normalOS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                float edge : TEXCOORD5;
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
                output.edge = input.uv.x;
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

            // Fat round the outline: it covers the side of the steak and shows as a thin line on
            // top, swelling in places. Gaps let the lean reach the edge, as on a real cut, and show
            // the doneness bands there. An edge value of 1 is the wall. Noise is sampled by
            // direction from the centre, so it varies along the outline, not across the steak.
            float EdgeFat(float2 p, float edge)
            {
                float offset = _Seed * 37.0;
                float wobble = ValueNoise(p * 18.0 + offset) - 0.5;
                float2 direction = normalize(p + 0.00001);

                float gaps = ValueNoise(direction * 2.4 + offset + 21.0) + wobble * 0.25;
                // Noise rarely reaches its extremes, so this scale makes coverage roughly as set.
                float threshold = (1.0 - _EdgeCoverage) * 1.4;
                float covered = smoothstep(threshold, threshold + 0.09, gaps);

                float swell = ValueNoise(direction * 1.7 + offset + 31.0);
                float start = 0.93 - lerp(0.02, 0.3, _EdgeSwell * swell * swell);
                return covered * smoothstep(start, start + 0.03, edge + wobble * 0.05);
            }

            // The rest of the fat on the top face: x is what holds its colour (the seam), y is thin
            // marbling, which melts away as it cooks.
            float2 TopFat(float2 p)
            {
                float offset = _Seed * 37.0;
                float wobble = ValueNoise(p * 18.0 + offset) - 0.5;

                // A seam: a wavy line of fat running across the meat towards its edges, with its
                // position and direction from the seed. Its width changes along its length and it
                // breaks in places, so it reads as fat rather than a drawn line. Distances in metres.
                float2 centre = (float2(frac(_Seed * 7.3), frac(_Seed * 3.1)) - 0.5) * float2(0.09, 0.07);
                float seamAngle = frac(_Seed * 13.7) * 3.1415927;
                float2 seamDirection = float2(cos(seamAngle), sin(seamAngle));
                float2 fromCentre = p - centre;
                float run = dot(fromCentre, seamDirection);
                float bend = ValueNoise(p * 9.0 + offset + 2.0) - 0.5;
                float away = dot(fromCentre, float2(-seamDirection.y, seamDirection.x))
                    + sin(run * 16.0 + offset) * 0.018 + bend * 0.03 + wobble * 0.006;
                float seamWidth = lerp(0.0, 0.009, ValueNoise(p * 11.0 + offset + 11.0));
                float seam = (1.0 - smoothstep(seamWidth, seamWidth + 0.003, abs(away)))
                    * smoothstep(0.25, 0.45, ValueNoise(p * 12.0 + offset + 5.0)) * _SeamAmount;

                // Marbling, gathered in patches as in real meat: short veins where bent noise
                // crosses its midpoint, plus flecks at its peaks. Both fade once smaller than a
                // pixel, or they would shimmer from the game camera.
                float2 q = p * _MarbleScale + offset;
                float patches = smoothstep(0.42, 0.68, ValueNoise(p * 24.0 + offset + 9.0));
                float veins = (1.0 - smoothstep(0.0, 0.05, abs(ValueNoise(q + wobble * 3.0) - 0.5))) * patches;
                // The flecks' noise is bent by a coarser noise, which hides its square grid.
                float fleckBend = ValueNoise(q * 0.6 + 7.0) * 2.5;
                float flecks = smoothstep(0.76, 0.9, ValueNoise(q * 1.7 + fleckBend)) * patches;
                float marbling = max(veins, flecks) * saturate(1.0 - length(fwidth(q)) * 1.2) * _MarbleAmount;

                return float2(seam, marbling);
            }

            // Fat browns far more slowly than lean: it goes from white to a creamy yellow and holds
            // that while the lean around it sears, only colouring once the crust is nearly burnt.
            // It stays a little lighter even then, but still reads as burnt.
            half3 FatColor(float browning, half3 crust)
            {
                half3 fat = lerp(_FatRaw.rgb, _FatCooked.rgb, smoothstep(0.0, 0.5, browning));
                return lerp(fat, crust, smoothstep(0.7, 1.0, browning) * 0.85);
            }

            half3 FoodColor(Varyings input)
            {
                // Edge: the three bands by height, blended so no hard lines show from a distance.
                float height = saturate(input.positionOS.y / max(_Thickness, 0.0001));
                half3 edge = lerp(_BandBottom.rgb, _BandCore.rgb,
                    smoothstep(1.0 / 3.0 - _BandSoftness, 1.0 / 3.0 + _BandSoftness, height));
                edge = lerp(edge, _BandTop.rgb,
                    smoothstep(2.0 / 3.0 - _BandSoftness, 2.0 / 3.0 + _BandSoftness, height));

                // Each side's crust shows as a seared line along that edge, so searing the underside
                // shows before the first flip.
                float bottomLine = 1.0 - smoothstep(_SearLine * 0.5, _SearLine, height);
                float topLine = smoothstep(1.0 - _SearLine, 1.0 - _SearLine * 0.5, height);
                edge = lerp(edge, _CrustBottom.rgb, bottomLine);
                edge = lerp(edge, _BaseColor.rgb, topLine);

                // Two octaves of noise, centred on 1, so mottling doesn't shift the overall colour:
                // the crust still reads as the simulation's browning.
                float mottle = 0.65 * ValueNoise(input.positionOS.xz * _MottleScale)
                    + 0.35 * ValueNoise(input.positionOS.xz * _MottleScale * 2.7);
                half3 crust = _BaseColor.rgb * (1.0 + (mottle - 0.5) * 2.0 * _MottleStrength);

                // A little colour variation, so the fat doesn't look like smooth icing.
                float fatShade = 1.0 + (mottle - 0.5) * 0.25;
                half3 fat = FatColor(_Browning, _BaseColor.rgb) * fatShade;

                // Fat on the side renders from the pan up: the underside's browning at the bottom,
                // the top's at the top.
                half3 sideFat = lerp(FatColor(_BrowningBottom, _CrustBottom.rgb), FatColor(_Browning, _BaseColor.rgb),
                    smoothstep(0.15, 0.6, height)) * fatShade;

                float edgeFat = EdgeFat(input.positionOS.xz, input.edge);
                float2 topFat = TopFat(input.positionOS.xz);
                float marbling = topFat.y * lerp(1.0, _MarbleCookedStrength, smoothstep(0.0, 0.5, _Browning));
                crust = lerp(crust, fat, saturate(max(max(edgeFat, topFat.x), marbling)));

                // Fat covers the side too, hiding the doneness bands wherever it reaches.
                edge = lerp(edge, sideFat, edgeFat);

                // Faces looking up show the crust; the bevel blends into the edge.
                return lerp(edge, crust, smoothstep(0.55, 0.9, normalize(input.normalOS).y));
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                // Cartoony flat shading (ToonCommon.hlsl, decision 2026-11-08).
                half3 color = ToonShade(FoodColor(input), input.positionWS, input.normalWS, input.positionCS);
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
