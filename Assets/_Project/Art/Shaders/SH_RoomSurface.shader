// The room's floors and walls (decision 2026-11-08, the art overhaul): patterns drawn from world
// position, so any size of floor or wall tiles seamlessly and nothing needs painting. Toon shaded
// like everything else, without outlines (the room is the backdrop).
//   Tiles:  square floor tiles with grout and a slight colour change per tile (the kitchen).
//   Planks: wooden boards along x with staggered ends, seams and grain (the dining room).
//   Wall:   cream plaster over a wood-panelled lower band with a rail, and a wood cap on top.
Shader "Game/Room/Surface"
{
    Properties
    {
        [KeywordEnum(Tiles, Planks, Wall)] _Pattern("Pattern", Float) = 0

        [Header(Toon)]
        _ShadowTint("Shadow tint (multiplies the colour)", Color) = (0.76, 0.66, 0.64, 1)
        _ToonEdge("Light edge", Range(-1, 1)) = 0.1
        _ToonSoftness("Light edge softness", Range(0.001, 0.5)) = 0.04
        _RimStrength("Rim light", Range(0, 1)) = 0
        _OutlineColor("Outline (unused)", Color) = (0.2, 0.13, 0.09, 1)
        _OutlineWidth("Outline width (unused)", Float) = 0

        [Header(Main)]
        _BaseColor("Tile, plank or plaster", Color) = (0.5, 0.47, 0.44, 1)
        _SeamColor("Grout or seams", Color) = (0.36, 0.33, 0.3, 1)
        _Size("Tile size, or plank width (m)", Float) = 0.5
        _Length("Plank length (m)", Float) = 1.6
        _SeamWidth("Seam width (m)", Float) = 0.012
        _Variation("Colour change per tile or plank", Range(0, 0.3)) = 0.06
        _Grain("Wood grain", Range(0, 1)) = 0

        [Header(Wall)]
        _PanelColor("Panelling", Color) = (0.55, 0.36, 0.22, 1)
        _CapColor("Top of the wall", Color) = (0.42, 0.27, 0.17, 1)
        _PanelHeight("Panelling height (m)", Float) = 0.5
        _PanelWidth("Panel width (m)", Float) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _SeamColor;
            float _Size;
            float _Length;
            float _SeamWidth;
            float _Variation;
            float _Grain;
            half4 _PanelColor;
            half4 _CapColor;
            float _PanelHeight;
            float _PanelWidth;
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
            #pragma shader_feature_local _PATTERN_TILES _PATTERN_PLANKS _PATTERN_WALL
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
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
                float fogFactor : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
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
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                            lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), f.x), f.y);
            }

            // 1 inside a seam of the given width round each cell of a grid, 0 elsewhere, with a
            // pixel of softening so the lines don't shimmer. p is in cells; width is in cells.
            float Seam(float2 p, float2 width)
            {
                float2 fromEdge = min(frac(p), 1.0 - frac(p));
                float2 soft = fwidth(p);
                float2 edges = 1.0 - smoothstep(width * 0.5, width * 0.5 + soft, fromEdge);
                return max(edges.x, edges.y);
            }

            // A colour nudged lighter or darker per cell, so a floor isn't one flat sheet.
            half3 Vary(half3 color, float2 cell)
            {
                return color * (1.0 + (Hash(cell) - 0.5) * 2.0 * _Variation);
            }

            // Fine streaks along a board, for wood.
            float GrainAt(float along, float across, float2 cell)
            {
                float wobble = ValueNoise(float2(along * 1.5, across * 30.0) + cell * 7.0);
                return smoothstep(0.55, 0.8, ValueNoise(float2(along * 3.0, across * 60.0 + wobble * 3.0) + cell * 13.0));
            }

            half3 Tiles(float2 floorPos)
            {
                float2 p = floorPos / _Size;
                half3 tile = Vary(_BaseColor.rgb, floor(p));
                return lerp(tile, _SeamColor.rgb, Seam(p, _SeamWidth / _Size));
            }

            half3 Planks(float2 floorPos)
            {
                // Boards run along x, _Size wide; each row's ends are offset by its own amount.
                float row = floor(floorPos.y / _Size);
                float along = floorPos.x / _Length + Hash(float2(row, 3.0));
                float2 cell = float2(floor(along), row);
                half3 board = Vary(_BaseColor.rgb, cell);
                board = lerp(board, _SeamColor.rgb, GrainAt(floorPos.x, floorPos.y, cell) * _Grain * 0.35);
                float2 p = float2(along, floorPos.y / _Size);
                float2 width = float2(_SeamWidth / _Length, _SeamWidth / _Size);
                return lerp(board, _SeamColor.rgb, Seam(p, width));
            }

            half3 Wall(float3 positionWS, float3 normalWS)
            {
                // The top of the wall: a wooden cap.
                if (normalWS.y > 0.5)
                    return _CapColor.rgb;

                // Along the wall, whichever way it runs: walls are square to the room.
                float along = abs(normalWS.x) > 0.5 ? positionWS.z : positionWS.x;
                float height = positionWS.y;
                if (height > _PanelHeight + 0.04)
                    return _BaseColor.rgb; // plaster

                // A rail along the top of the panelling, then upright panels below it.
                if (height > _PanelHeight)
                    return _CapColor.rgb;
                float2 p = float2(along / _PanelWidth, 0.5);
                half3 panel = Vary(_PanelColor.rgb, float2(floor(p.x), 0.0));
                panel = lerp(panel, _SeamColor.rgb, GrainAt(height, along, floor(p.x)) * _Grain * 0.3);
                float seam = Seam(p, float2(_SeamWidth / _PanelWidth, 0.0));
                return lerp(panel, _SeamColor.rgb, seam);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                #if defined(_PATTERN_WALL)
                    half3 albedo = Wall(input.positionWS, normalWS);
                #elif defined(_PATTERN_PLANKS)
                    half3 albedo = Planks(input.positionWS.xz);
                #else
                    half3 albedo = Tiles(input.positionWS.xz);
                #endif
                half3 color = ToonShade(albedo, input.positionWS, normalWS, input.positionCS);
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
