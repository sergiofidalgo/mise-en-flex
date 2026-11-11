// The closing shutter (DayTransitions, decision 2026-11-11): wooden slats drawn across a UI
// rectangle, so a whole screen of them costs one quad and no texture. _Blur takes them out of
// focus behind the end-of-day dashboard: each pixel is the exact average of the slat pattern over a
// window that widens with it (summed analytically: no texture reads, no aliasing). _Dim darkens them.
Shader "Game/UI/Shutter"
{
    Properties
    {
        [PerRendererData] _MainTex("Unused", 2D) = "white" {}
        _WoodA("Slat", Color) = (0.36, 0.227, 0.129, 1)
        _WoodB("Slat, alternate", Color) = (0.396, 0.251, 0.145, 1)
        _Highlight("Highlight", Color) = (0.545, 0.353, 0.2, 1)
        _Ink("Ink", Color) = (0.227, 0.141, 0.078, 1)
        _Slats("Slats from top to bottom", Float) = 20
        _Blur("Blur (0 sharp, 1 soft)", Range(0, 1)) = 0
        _Dim("Dim", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            half4 _WoodA, _WoodB, _Highlight, _Ink;
            float _Slats, _Blur, _Dim;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            // One slat is, from its foot: an ink seam, wood, a light lip, wood again. Alternate slats
            // are a shade apart. These are the edges, as fractions of a slat (the mockup's look).
            static const float SeamTop = 0.075;
            static const float LipFrom = 0.84;
            static const float LipTo = 0.92;

            // The pattern summed from the foot of slat n up to f (0..1) within it.
            float3 Partial(float f, float3 wood)
            {
                return _Ink.rgb * min(f, SeamTop)
                    + wood * (clamp(f, SeamTop, LipFrom) - SeamTop + max(f, LipTo) - LipTo)
                    + _Highlight.rgb * (clamp(f, LipFrom, LipTo) - LipFrom);
            }

            // Floats, not halves: a pixel-wide window takes the difference of two sums near 10, which a
            // half (about three digits) can't resolve.
            // The pattern summed from the bottom edge (s = 0) up to s, in slats. Box-filtering is then
            // a difference of two of these divided by the width: an exact average, never aliased.
            float3 Summed(float s)
            {
                float n = floor(s);
                float evens = ceil(n * 0.5);   // slats 0, 2, 4... are _WoodA
                float odds = n - evens;
                float woodShare = 1.0 - SeamTop - (LipTo - LipFrom);
                float3 whole = n * (_Ink.rgb * SeamTop + _Highlight.rgb * (LipTo - LipFrom))
                    + woodShare * (evens * _WoodA.rgb + odds * _WoodB.rgb);
                float3 wood = fmod(n, 2.0) < 0.5 ? _WoodA.rgb : _WoodB.rgb;
                return whole + Partial(s - n, wood);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float s = input.uv.y * _Slats + 2.0; // kept positive under the window
                // The averaging window: about a pixel when sharp (antialiasing), a slat and a half
                // when blurred, which leaves only a soft hint of the slats.
                float w = max(fwidth(s), _Blur * 1.5);
                float3 c = (Summed(s + w * 0.5) - Summed(s - w * 0.5)) / w;
                c *= 1.0 - _Dim;
                return half4((half3)c, input.color.a);
            }
            ENDCG
        }
    }
}
