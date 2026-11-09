// The washing game drawn on the sink's water (decision 2026-11-09: the station is the interface).
// One flat quad, everything drawn from its UVs so it stays crisp at any size:
//  - a faint dark disc that separates the game from the water,
//  - a soap-suds patch in the middle where washing goes at full speed (_GoodRadius),
//    and a dashed circle where it stops (_OkRadius),
//  - a rim in the washing player's colour that fills clockwise with the plate's progress,
//  - the point the player balances: a soap bar that glows with suds when it's washing well
//    and gets a broken red ring when it isn't (shape as well as colour, for colour-blind players).
// The view turns the quad so UV up is screen up, which keeps the stick and the drawing in step
// whatever way the sink faces. Depth-tested like any object: a chef in front hides it.
Shader "Game/FX/WashRing"
{
    Properties
    {
        _RimColor("Rim (player colour)", Color) = (0.91, 0.53, 0.23, 1)
        _GoodColor("Washing well", Color) = (0.45, 0.85, 0.40, 1)
        _BadColor("Not washing", Color) = (0.95, 0.30, 0.26, 1)
        _SudsColor("Suds", Color) = (1, 1, 1, 1)
        _Point("Point (xy, 1 = rim)", Vector) = (0.4, 0.3, 0, 0)
        _Rate("Washing rate", Range(0, 1)) = 1
        _Progress("Plate progress", Range(0, 1)) = 0.4
        _GoodRadius("Full speed inside", Range(0, 1)) = 0.25
        _OkRadius("Nothing beyond", Range(0, 1)) = 0.55
        _Fade("Fade", Range(0, 1)) = 1
        _RimRadius("Rim radius (UV units)", Range(0.5, 1)) = 0.84
        _RimWidth("Rim width", Range(0.01, 0.3)) = 0.15
        _Segments("Rim segments", Range(4, 32)) = 16
        _PointRadius("Point radius", Range(0.02, 0.3)) = 0.2
        _Flash("Plate-done flash", Range(0, 1)) = 0
        _InkColor("Ink (outlines)", Color) = (0.227, 0.141, 0.078, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "WashRing"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                half4 _GoodColor;
                half4 _BadColor;
                half4 _SudsColor;
                float4 _Point;
                float _Rate;
                float _Progress;
                float _GoodRadius;
                float _OkRadius;
                float _Fade;
                float _RimRadius;
                float _RimWidth;
                float _PointRadius;
                half4 _InkColor;
                float _Segments;
                float _Flash;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 p : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.p = input.uv * 2.0 - 1.0; // -1..1, the quad's edge at 1
                return output;
            }

            // 1 inside a band of half-width w around distance d = 0, antialiased by screen-space size.
            float Band(float d, float w)
            {
                float aa = max(fwidth(d), 1e-4);
                return 1.0 - smoothstep(w - aa, w + aa, abs(d));
            }

            // 1 inside a disc of radius r around distance d from its centre.
            float Disc(float d, float r)
            {
                float aa = max(fwidth(d), 1e-4);
                return 1.0 - smoothstep(r - aa, r + aa, d);
            }

            // Straight alpha "over": paints colour c with coverage a onto what's there.
            void Over(inout half4 acc, half3 c, half a)
            {
                acc.rgb = lerp(acc.rgb, c, a);
                acc.a = a + acc.a * (1.0 - a);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 p = input.p;
                float r = length(p);
                // Angle clockwise from the top, 0..1, for the progress fill.
                float turn = frac(atan2(p.x, p.y) / (2.0 * PI) + 1.0);
                // Ring radii are in game units (1 = rim); scale them to the drawn rim.
                float scale = _RimRadius;
                float t = _Time.y;

                half4 acc = half4(0, 0, 0, 0);

                // Backing: a soft dark disc out to the rim, so the game reads on bright water.
                Over(acc, half3(0.05, 0.07, 0.10), 0.4 * Disc(r, scale));

                // Suds patch: a cluster of wobbling bubbles filling the full-speed zone, white with
                // an Ink silhouette so it reads on the water and on the white plate beneath it.
                float2 q = p / scale;
                float suds = 0.0;
                float sudsEdge = 0.0;
                [unroll] for (int i = 0; i < 7; i++)
                {
                    float a = i * 2.39996 + t * 0.6;             // golden-angle spread, slowly turning
                    float rr = _GoodRadius * (i == 0 ? 0.0 : 0.62);
                    float2 c = float2(sin(a), cos(a)) * rr;
                    float br = _GoodRadius * (i == 0 ? 0.55 : 0.42) * (1.0 + 0.06 * sin(t * 3.0 + i));
                    float d = length(q - c);
                    suds = max(suds, Disc(d, br));
                    sudsEdge = max(sudsEdge, Disc(d, br + 0.06));
                }
                Over(acc, _InkColor.rgb, sudsEdge);
                Over(acc, _SudsColor.rgb, 0.9 * suds);
                // A glint on each side of the patch, so it reads as soap rather than a white blob.
                Over(acc, _GoodColor.rgb, 0.35 * suds * smoothstep(0.0, 1.0, -q.y / max(_GoodRadius, 1e-3)));

                // Where washing stops: a dashed Ink circle.
                float dashes = step(0.45, frac(turn * 16.0));
                Over(acc, _InkColor.rgb, 0.85 * dashes * Band(r - _OkRadius * scale, 0.02));

                // Rim, arcade style: a thick Ink-outlined track cut into chunky segments that light
                // up in the washer's colour as the plate's progress reaches them. Lit segments are
                // two-tone (lighter on their outer half) so they read as raised, toy-like blocks.
                float rimHalf = _RimWidth * 0.5;
                Over(acc, _InkColor.rgb, Band(r - scale, rimHalf + 0.035));
                float segment = frac(turn * _Segments);
                float segAa = fwidth(turn * _Segments);
                float gap = 0.09; // share of each segment left as a gap
                float block = smoothstep(gap - segAa, gap + segAa, segment) * (1.0 - smoothstep(1.0 - segAa, 1.0, segment));
                float segmentIndex = floor(turn * _Segments);
                float lit = step(segmentIndex + 0.5, _Progress * _Segments);
                float rim = Band(r - scale, rimHalf);
                half3 track = _RimColor.rgb * 0.28;
                half3 lift = lerp(_RimColor.rgb, half3(1, 1, 1), 0.35 * step(scale, r));
                Over(acc, lit > 0.5 ? lift : track, rim * block);
                // The plate's done: the whole rim flashes white.
                Over(acc, half3(1, 1, 1), _Flash * rim);

                // Off target: chevrons flow from the point towards the suds, showing which way to
                // push the stick. They fade out as washing picks up, so a steady hand sees none.
                {
                    float2 toCentre = -_Point.xy * scale;
                    float len = length(toCentre);
                    float2 u = toCentre / max(len, 1e-4);          // along, towards the centre
                    float2 v = float2(-u.y, u.x);                  // across
                    float start = _PointRadius * 1.9;
                    float span = max(0.0, len - start - _GoodRadius * scale * 0.8);
                    float show = (1.0 - smoothstep(0.2, 0.7, _Rate)) * smoothstep(0.02, 0.08, span);
                    float half_ = 0.06;                            // chevron half-width
                    [unroll] for (int k = 0; k < 3; k++)
                    {
                        float s = frac(t * 1.1 + k / 3.0);         // 0..1 along the way, flowing in
                        float2 c = _Point.xy * scale + u * (start + s * span);
                        float2 d2 = p - c;
                        float a = dot(d2, u);
                        float b = dot(d2, v);
                        // A V pointing towards the centre: its arms are a = -|b|, cut at |b| = half.
                        float arm = abs(a + abs(b));
                        float within = 1.0 - smoothstep(half_, half_ + 0.01, abs(b));
                        float ends = smoothstep(0.0, 0.2, s) * (1.0 - smoothstep(0.75, 1.0, s));
                        float alpha = show * within * ends;
                        Over(acc, _InkColor.rgb, alpha * Band(arm, 0.032));
                        Over(acc, half3(1, 1, 1), alpha * Band(arm, 0.016));
                    }
                }

                // The point: a soap bar in the rate's colour, white-edged.
                float2 pp = _Point.xy * scale;
                float dp = length(p - pp);
                half3 pointColor = lerp(_BadColor.rgb, _GoodColor.rgb, _Rate);
                // Washing well: a pulsing halo of suds round it.
                float halo = Band(dp - _PointRadius * (1.6 + 0.15 * sin(t * 10.0)), 0.02) * smoothstep(0.4, 0.9, _Rate);
                Over(acc, _SudsColor.rgb, 0.9 * halo);
                // Not washing: a broken red ring round it, turning, so it reads without colour.
                float2 dpp = p - pp;
                float spin = frac(atan2(dpp.x, dpp.y) / (2.0 * PI) + t * 0.5);
                float broken = Band(dp - _PointRadius * 1.65, 0.022) * step(0.5, frac(spin * 4.0)) * (1.0 - smoothstep(0.05, 0.3, _Rate));
                Over(acc, _BadColor.rgb, broken);
                Over(acc, _InkColor.rgb, Disc(dp, _PointRadius + 0.06));
                Over(acc, half3(1, 1, 1), Disc(dp, _PointRadius + 0.035));
                Over(acc, pointColor, Disc(dp, _PointRadius));
                // A shine at its top left, so it reads as a glossy soap bar rather than a dot.
                Over(acc, half3(1, 1, 1), 0.8 * Disc(length(dpp - float2(-0.35, 0.35) * _PointRadius), _PointRadius * 0.3));

                acc.a *= _Fade;
                return acc;
            }
            ENDHLSL
        }
    }
}
