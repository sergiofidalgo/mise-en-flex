// Rooms behind the tower's glass without building them (the user's note, 2026-11-10: "through the
// window you can see each floor's layout"; decision: real rooms only where the camera climbs,
// these everywhere else). Interior mapping: each pane is the window of a box-shaped room behind it;
// the view ray from the camera is traced into that box and the fragment shows whatever wall, floor
// or ceiling it hits, so the room has depth and parallax as the camera moves. Everything is worked
// out in world space from the pane's normal, tangent and UV, so it survives static batching.
// Each room picks from a hash of its position: lit (warm or cool) or dark, wall and carpet colours,
// desks with screens, someone at the back, a picture or a door, blinds, a television's flicker.
Shader "Game/InteriorCard"
{
    Properties
    {
        _PaneSize ("Pane width and height (m)", Vector) = (1.9, 2.1, 0, 0)
        _RoomDepth ("Room depth (m)", Float) = 5.5
        _LitChance ("Share of rooms lit", Range(0, 1)) = 0.45
        _Warm ("Warm light", Color) = (1, 0.82, 0.58, 1)
        _Cool ("Cool light", Color) = (0.82, 0.9, 1, 1)
        _Glass ("Glass tint", Color) = (0.1, 0.13, 0.22, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _PaneSize;
                float _RoomDepth;
                half _LitChance;
                half4 _Warm;
                half4 _Cool;
                half4 _Glass;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float fog : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.17);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            // Inside a rectangle centred at c with half-size e, softened by s.
            float Box(float2 p, float2 c, float2 e, float s)
            {
                float2 d = abs(p - c) - e;
                return saturate(1.0 - max(d.x, d.y) / s);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 t = normalize(input.tangentWS - n * dot(input.tangentWS, n));
                float3 up = float3(0, 1, 0);
                float3 view = normalize(input.positionWS - _WorldSpaceCameraPos);
                // The ray in the room's frame: x along the pane, y up, z into the building.
                float3 d = float3(dot(view, t), dot(view, up), max(dot(view, -n), 1e-3));
                float w = _PaneSize.x;
                float h = _PaneSize.y;
                float3 p = float3((input.uv.x - 0.5) * w, (input.uv.y - 0.5) * h, 0.0);

                // The room: a little wider than the pane, its floor under the sill and its ceiling
                // above the head of the window.
                float floorY = -h * 0.5 - 0.35;
                float ceilingY = h * 0.5 + 0.55;
                float3 low = float3(-w * 0.5 - 0.15, floorY, 0.0);
                float3 high = float3(w * 0.5 + 0.15, ceilingY, _RoomDepth);
                float3 ta = (low - p) / d;
                float3 tb = (high - p) / d;
                float3 far = max(ta, tb);
                float hitT = min(min(far.x, far.y), far.z);
                float3 hit = p + d * hitT;

                // This room's own dice, from where its pane is.
                float3 centre = input.positionWS - t * p.x - up * p.y;
                float3 cell = floor(centre * 2.0 + 0.5);
                float h1 = Hash(cell);
                float h2 = Hash(cell + 11.3);
                float h3 = Hash(cell + 27.1);
                float h4 = Hash(cell + 41.7);
                float h5 = Hash(cell + 59.9);
                bool lit = h1 < _LitChance;
                half3 light = h2 < 0.6 ? _Warm.rgb : _Cool.rgb;

                half3 wallColour = lerp(half3(0.82, 0.78, 0.7), half3(0.62, 0.7, 0.72), h3);
                wallColour = lerp(wallColour, half3(0.72, 0.6, 0.5), step(0.75, h4));
                half3 carpet = lerp(half3(0.3, 0.33, 0.4), half3(0.42, 0.32, 0.26), h4);
                half3 colour;
                float depthShade = 1.0 - saturate(hit.z / _RoomDepth) * 0.35;

                if (far.z <= hitT + 1e-4)
                {
                    // The back wall: its colour, desks along it, screens on them, maybe someone there,
                    // a picture or a door.
                    colour = wallColour * 0.85;
                    float desk = step(hit.y, floorY + 0.78) * step(floorY + 0.68, hit.y) * step(0.3, frac(hit.x * 0.45 + h3));
                    float deskLegs = step(hit.y, floorY + 0.7) * step(0.85, frac(hit.x * 1.3 + h3));
                    colour = lerp(colour, half3(0.35, 0.26, 0.2), saturate(desk + deskLegs));
                    float screen = Box(hit.xy, float2(-0.4 + h5 * 0.8, floorY + 1.0), float2(0.22, 0.15), 0.02);
                    colour = lerp(colour, lit ? half3(0.6, 0.85, 1.0) * 1.6 : half3(0.25, 0.4, 0.7), screen * step(0.35, h3));
                    float picture = Box(hit.xy, float2(0.3 - h2 * 0.6, floorY + 1.75), float2(0.32, 0.22), 0.02);
                    colour = lerp(colour, lerp(half3(0.75, 0.45, 0.3), half3(0.3, 0.5, 0.45), h5), picture * step(0.5, h4));
                    float door = Box(hit.xy, float2(w * 0.3, floorY + 1.0), float2(0.42, 1.0), 0.02);
                    colour = lerp(colour, half3(0.4, 0.3, 0.24), door * step(h4, 0.25));
                    // Someone at the back: a head over shoulders, standing or at a desk.
                    float2 person = float2(-0.5 + h3, floorY + (h2 < 0.5 ? 1.05 : 1.35));
                    float body = Box(hit.xy, person, float2(0.2, 0.3), 0.04);
                    float head = saturate(1.0 - (length(hit.xy - person - float2(0, 0.45)) - 0.13) / 0.03);
                    colour = lerp(colour, half3(0.18, 0.16, 0.2), saturate(body + head) * step(0.55, h5));
                }
                else if (far.y <= hitT + 1e-4)
                {
                    if (d.y < 0.0)
                    {
                        // The floor: carpet, a little lighter where the room's light falls.
                        colour = carpet * (0.9 + 0.1 * sin(hit.x * 9.0) * sin(hit.z * 9.0));
                    }
                    else
                    {
                        // The ceiling, with a light panel down the middle of the room.
                        colour = half3(0.86, 0.85, 0.82);
                        float panel = Box(float2(hit.x, hit.z), float2(0.0, _RoomDepth * 0.45), float2(0.35, _RoomDepth * 0.3), 0.03);
                        colour = lerp(colour, lit ? half3(1.6, 1.6, 1.5) : half3(0.5, 0.5, 0.5), panel);
                    }
                }
                else
                {
                    // A side wall, a shade darker.
                    colour = wallColour * 0.7;
                }

                // The room's light: lit rooms glow (they're seen at night), dark ones keep a little of
                // the city's light, and now and then a television flickers in one.
                if (lit)
                {
                    colour *= light * depthShade * 1.05;
                }
                else
                {
                    colour = colour * 0.1 + half3(0.02, 0.03, 0.06);
                    if (h5 > 0.88)
                    {
                        float flicker = 0.5 + 0.5 * frac(sin(floor(_Time.y * 4.0 + h2 * 13.0)) * 43758.5);
                        colour += half3(0.12, 0.2, 0.4) * flicker * (1.0 - saturate(hit.z / _RoomDepth) * 0.6);
                    }
                }

                // Blinds down over the top of some windows.
                if (h4 > 0.72)
                {
                    float level = 0.55 + 0.35 * h2;
                    if (input.uv.y > level)
                    {
                        float slat = step(0.35, frac(input.uv.y * 26.0));
                        half3 blind = lit ? half3(0.95, 0.88, 0.72) * light * 0.9 : half3(0.18, 0.18, 0.22);
                        colour = lerp(colour, blind * (0.8 + 0.2 * slat), 0.92);
                    }
                }

                // The glass itself: a tint, a diagonal reflection, more of the sky at a glancing angle.
                float streak = saturate(1.0 - abs((input.uv.x + input.uv.y * 0.6) - 0.75) * 9.0) * 0.12;
                float glance = 1.0 - d.z;
                colour = colour * (1.0 - 0.25 * glance) + _Glass.rgb * (0.35 + glance * 0.6) + streak;

                colour = MixFog(colour, input.fog);
                return half4(colour, 1.0);
            }
            ENDHLSL
        }

        // Depth, so depth priming and the depth texture see the panes as the glass wall they are.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
    }
}
