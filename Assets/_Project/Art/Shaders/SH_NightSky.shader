// The night sky over the boss's city (user's note, 2026-11-09: "on the intro scene the sky is
// super important too... stars, the moon"). A skybox, all procedural, no textures:
// - a gradient from the zenith's deep blue down to the haze at the horizon, which is the fog's own
//   colour, so the city's farthest buildings melt into it with no seam, plus a warm glow of city
//   light along the horizon;
// - stars in two sizes, twinkling, thinning towards the horizon where the city light drowns them;
// - the moon: a disc with soft craters, a rim of light, and a wide halo;
// - slow clouds, dark against the sky and silvered where they pass near the moon.
Shader "Game/NightSky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.03, 0.04, 0.12, 1)
        _CityGlow ("City glow at the horizon", Color) = (0.32, 0.2, 0.3, 1)
        _GlowHeight ("City glow height", Range(0.02, 0.6)) = 0.22
        _MoonDirection ("Moon direction", Vector) = (0.35, 0.45, 0.82, 0)
        _MoonColor ("Moon", Color) = (1, 0.96, 0.86, 1)
        _MoonSize ("Moon size (radians)", Range(0.01, 0.2)) = 0.06
        _HaloColor ("Moon halo", Color) = (0.45, 0.5, 0.8, 1)
        _StarDensity ("Stars", Range(0, 1)) = 0.55
        _StarBrightness ("Star brightness", Range(0, 3)) = 1.4
        _Clouds ("Cloud cover", Range(0, 1)) = 0.35
        _CloudSpeed ("Cloud drift", Range(0, 0.05)) = 0.006
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith;
                half4 _CityGlow;
                half _GlowHeight;
                float4 _MoonDirection;
                half4 _MoonColor;
                half _MoonSize;
                half4 _HaloColor;
                half _StarDensity;
                half _StarBrightness;
                half _Clouds;
                half _CloudSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(float3(i, 1.0));
                float b = Hash(float3(i + float2(1, 0), 1.0));
                float c = Hash(float3(i + float2(0, 1), 1.0));
                float d = Hash(float3(i + float2(1, 1), 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    sum += Noise(p) * amplitude;
                    p *= 2.03;
                    amplitude *= 0.5;
                }
                return sum;
            }

            // Stars on a grid of cells round the sky: a few cells hold one, jittered inside the cell,
            // sized by `scale`; each twinkles at its own rate.
            float Stars(float3 direction, float scale, float threshold)
            {
                float3 p = direction * scale;
                float3 cell = floor(p);
                float pick = Hash(cell);
                if (pick < threshold)
                    return 0.0;
                float3 centre = cell + 0.5 + (float3(Hash(cell + 7.1), Hash(cell + 3.7), Hash(cell + 1.3)) - 0.5) * 0.6;
                float d = length(p - centre);
                float twinkle = 0.65 + 0.35 * sin(_Time.y * (1.5 + pick * 4.0) + pick * 40.0);
                return saturate(1.0 - d * 3.2) * twinkle * (0.5 + 0.5 * Hash(cell + 9.9));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float up = direction.y;
                half3 horizon = unity_FogColor.rgb;

                // The sky: haze at the horizon (the fog's colour), deep blue overhead, the city's
                // warm light glowing up from the horizon.
                float height = saturate(up);
                half3 sky = lerp(horizon, _Zenith.rgb, pow(height, 0.55));
                sky += _CityGlow.rgb * exp(-max(up, 0.0) / _GlowHeight) * 0.6;
                if (up < 0.0)
                    sky = horizon; // below the horizon: the haze

                // Stars: small ones everywhere, a few brighter, fewer near the horizon's city light.
                float visible = saturate((up - 0.04) * 4.0);
                float threshold = 1.0 - 0.03 * _StarDensity;
                float stars = Stars(direction, 260.0, threshold) + Stars(direction, 110.0, 1.0 - 0.012 * _StarDensity) * 1.6;
                sky += stars * visible * _StarBrightness * half3(0.9, 0.93, 1.0);

                // The moon: a disc with soft craters and a lit rim, inside a wide halo.
                float3 moonDirection = normalize(_MoonDirection.xyz);
                float angle = acos(saturate(dot(direction, moonDirection)));
                float halo = exp(-angle / (_MoonSize * 2.5)) * 0.5 + exp(-angle / (_MoonSize * 9.0)) * 0.18;
                sky += _HaloColor.rgb * halo;
                float disc = saturate((_MoonSize - angle) / (_MoonSize * 0.08));
                if (disc > 0.0)
                {
                    // Craters: noise on the disc's face, in its own plane.
                    float3 side = normalize(cross(moonDirection, float3(0, 1, 0)));
                    float3 top = cross(side, moonDirection);
                    float2 face = float2(dot(direction - moonDirection, side), dot(direction - moonDirection, top)) / _MoonSize;
                    float craters = Fbm(face * 2.6 + 3.0);
                    half3 moon = _MoonColor.rgb * lerp(0.78, 1.08, smoothstep(0.35, 0.65, craters));
                    moon *= lerp(1.0, 0.85, saturate(length(face))); // darker towards the edge
                    sky = lerp(sky, moon * 1.3, disc);
                }

                // Clouds: drifting noise projected on a dome, thin near the horizon, silver near the moon.
                if (up > 0.0 && _Clouds > 0.0)
                {
                    float2 plane = direction.xz / (up + 0.18) * 1.2;
                    plane += float2(_Time.y, _Time.y * 0.4) * _CloudSpeed * 10.0;
                    float cover = Fbm(plane);
                    float cloud = smoothstep(1.0 - _Clouds, 1.0 - _Clouds + 0.25, cover) * saturate(up * 3.0);
                    half3 shade = lerp(horizon * 0.8, _Zenith.rgb * 1.6, 0.5);
                    half3 silver = _MoonColor.rgb * 0.55 * exp(-angle / 0.35);
                    sky = lerp(sky, shade + silver, cloud * 0.85);
                }
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
}
