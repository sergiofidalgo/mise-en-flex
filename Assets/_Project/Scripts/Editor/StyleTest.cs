using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The cartoony style test (decision 2026-11-08): the gas hob, its pan and the steak get toon
    /// shading, outlines and a bold palette, so the direction can be judged on one station before
    /// every model gets it. Toon materials are created once; colours can then be tuned in the Editor.
    /// </summary>
    public static class StyleTest
    {
        private const string MaterialsFolder = "Assets/_Project/Art/Materials";
        private const string ToonShader = "Game/Toon";

        // Kitchen-real colours with clear contrast between parts: a stainless hob (decision 2026-11-08),
        // light enough that the darker pan on it stands out.
        private static readonly Color HobSteel = new Color(0.78f, 0.8f, 0.83f);
        // A steel top a shade darker than the body, with a black knob and the dark pan standing out
        // on it; the oven door below has a smoked window and a chrome bar handle.
        private static readonly Color HobTopSteel = new Color(0.76f, 0.77f, 0.8f);
        // A big dark-grey knob with a red pointer on top, so the control reads at a glance against
        // the steel and the black grate (the user's notes, 2026-11-08).
        private static readonly Color KnobGrey = new Color(0.3f, 0.31f, 0.34f);
        private static readonly Color KnobRed = new Color(0.82f, 0.18f, 0.12f);
        private const float KnobScale = 1.2f;
        private static readonly Color DoorSteel = new Color(0.64f, 0.66f, 0.7f);
        private static readonly Color WindowSmoked = new Color(0.15f, 0.16f, 0.19f);
        private static readonly Color Chrome = new Color(0.92f, 0.93f, 0.95f);
        private static readonly Color CastIron = new Color(0.07f, 0.07f, 0.08f);

        // Three greys on the top (the user's note, 2026-11-08): the rim lightest, the cooking
        // surface a step darker, the grate almost black.
        private static readonly Color TrayEnamel = new Color(0.42f, 0.43f, 0.46f);

        /// <summary>The spill tray's top, where the burner and grate stand. Must match TRAY_TOP in blender/build_pilot_models.py.</summary>
        public const float TrayTop = 0.865f;

        // The flames (the user's notes, 2026-11-08): cartoon tongues leaning out from the burner so
        // a high flame licks past the pan's edge (0.22 m), and between them shorter, brighter cores,
        // so the fire has a hot heart; soft flame puffs and embers rise from it too.
        private const int FlameCount = 16;
        private const float GlowLift = 0.003f;
        private const float FlameRadius = 0.2f;
        private const float FlameBase = TrayTop + 0.012f;
        private const float FlameLean = 35f;
        private const float CoreRadius = 0.18f;
        private const float CoreLean = 28f;
        private const float CoreScale = 0.6f;
        private const float FireRingRadius = 0.25f;
        private static readonly Color BurnerDark = new Color(0.08f, 0.08f, 0.09f);
        private static readonly Color PanSteel = new Color(0.32f, 0.36f, 0.44f);
        private static readonly Color HandleWood = new Color(0.55f, 0.33f, 0.18f);
        private static readonly Color HandleGrain = new Color(0.42f, 0.24f, 0.12f);
        private static readonly Color Bamboo = new Color(0.95f, 0.8f, 0.52f);
        private static readonly Color BambooGrain = new Color(0.82f, 0.62f, 0.35f);

        /// <summary>A Game/Toon material (MAT_Toon_<paramref name="name"/>), created once.</summary>
        public static Material EnsureToonMaterial(string name, Color color, Color? grain = null, float grainScale = 30f)
        {
            var path = $"{MaterialsFolder}/MAT_Toon_{name}.mat";
            // An existing material takes the colours given here too, so a change in code shows on rebuild.
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var isNew = material == null;
            if (isNew)
                material = new Material(Shader.Find(ToonShader));

            material.SetColor("_BaseColor", color);
            material.SetFloat("_WoodGrain", grain.HasValue ? 1f : 0f);
            if (grain.HasValue)
            {
                material.SetColor("_GrainColor", grain.Value);
                material.SetFloat("_GrainScale", grainScale);
            }

            if (isNew)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);
            return material;
        }

        public static Material BoardMaterial() => EnsureToonMaterial("Board", Bamboo, BambooGrain, grainScale: 18f);

        // The heat knob sits on the control strip along the top's front, to the right, clear of
        // the grate and the pan's handle (decision 2026-11-08).
        private static readonly Vector3 KnobPosition = new Vector3(0.32f, 0.9f, -0.4f);

        /// <summary>What the range's fire is made of, for its view to drive with the heat.</summary>
        public sealed class HobFire
        {
            public Renderer[] Flames = System.Array.Empty<Renderer>();
            public Renderer[] Cores = System.Array.Empty<Renderer>();
            public ParticleSystem[] Puffs = System.Array.Empty<ParticleSystem>();
            public ParticleSystem[] Embers = System.Array.Empty<ParticleSystem>();
            public Renderer[] Glow = System.Array.Empty<Renderer>();
        }

        /// <summary>
        /// The range's body, top, spill tray, heat knob, burner, grate and oven door, each in its
        /// own flat colour, and its fire for the hob's view to grow with the heat. The fire is
        /// empty without the model.
        /// </summary>
        public static HobFire DressHob(Transform hob, GameObject burner)
        {
            hob.Find("Body").GetComponent<Renderer>().sharedMaterial = EnsureToonMaterial("HobBody", HobSteel);
            burner.GetComponent<Renderer>().sharedMaterial = EnsureToonMaterial("Burner", BurnerDark);
            AddPart(hob, "Top", "HobTop", EnsureToonMaterial("HobTop", HobTopSteel));
            AddPart(hob, "Tray", "HobTray", EnsureToonMaterial("HobTray", TrayEnamel));
            AddPart(hob, "Door", "HobDoor", EnsureToonMaterial("HobDoor", DoorSteel));
            AddPart(hob, "Window", "HobWindow", EnsureToonMaterial("HobWindow", WindowSmoked));
            AddPart(hob, "Handle", "HobHandle", EnsureToonMaterial("HobHandle", Chrome));
            AddPart(hob, "Grate", "HobGrate", EnsureToonMaterial("HobGrate", CastIron));
            var knob = AddPart(hob, Game.Presentation.HobView.KnobName, "HobDial", EnsureToonMaterial("HobDial", KnobGrey));
            if (knob != null)
            {
                knob.localPosition = KnobPosition;
                knob.localScale = Vector3.one * KnobScale;
                AddPart(knob, "Mark", "HobDialMark", EnsureToonMaterial("HobDialMark", KnobRed)); // turns with it
            }

            var fire = new HobFire();
            var flameMaterial = PilotArt.EnsureFlameMaterial();
            var flames = Ring("Flame", FlameRadius, FlameLean, 0f, 1f);
            var cores = Ring("FlameCore", CoreRadius, CoreLean, 180f / FlameCount, CoreScale);
            if (flames == null || cores == null)
                return fire;
            fire.Flames = flames;
            fire.Cores = cores;

            // Soft flame puffs flickering up round the pan, and embers drifting off: realistic
            // touches on the cartoon fire. Their rates follow the heat (HobView).
            var puffs = GreyboxKitchenware.CreateParticles(hob, "FlamePuffs", FlameBase, FireRingRadius, lifetime: 0.35f,
                speed: 0.7f, minSize: 0.07f, maxSize: 0.15f, growth: 0.3f, maxParticles: 120);
            Glow(puffs, new Color(1f, 0.85f, 0.35f), new Color(1f, 0.38f, 0.05f), new Color(0.75f, 0.12f, 0.03f));
            var embers = GreyboxKitchenware.CreateParticles(hob, "Embers", FlameBase + 0.02f, FireRingRadius, lifetime: 1.3f,
                speed: 0.8f, minSize: 0.02f, maxSize: 0.035f, growth: 0.4f, maxParticles: 60);
            Glow(embers, new Color(1f, 0.8f, 0.3f), new Color(1f, 0.45f, 0.08f), new Color(0.9f, 0.2f, 0.04f));
            var noise = embers.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 1.5f;
            fire.Puffs = new[] { puffs };
            fire.Embers = new[] { embers };

            // The fire's light on the cooking surface round the burner: a soft additive disc
            // that grows and warms with the heat (the user's reference, 2026-11-08).
            var glow = GreyboxAssets.CreatePrimitive(PrimitiveType.Quad, "FireGlow", hob, PilotArt.EnsureFireMaterial(), keepCollider: false);
            glow.transform.localPosition = new Vector3(0f, TrayTop + GlowLift, 0f);
            glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var glowRenderer = glow.GetComponent<Renderer>();
            glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowRenderer.enabled = false; // lit by HobView once there's heat
            fire.Glow = new[] { glowRenderer };
            return fire;

            Renderer[] Ring(string name, float radius, float lean, float turn, float scale)
            {
                var ring = new Renderer[FlameCount];
                for (var i = 0; i < FlameCount; i++)
                {
                    var flame = AddPart(hob, $"{name}_{i + 1}", "HobFlame", flameMaterial);
                    if (flame == null)
                        return null;
                    var outward = Quaternion.Euler(0f, turn + 360f * i / FlameCount, 0f) * Vector3.forward;
                    flame.localPosition = new Vector3(0f, FlameBase, 0f) + outward * radius;
                    flame.localRotation = Quaternion.AngleAxis(lean, Vector3.Cross(Vector3.up, outward));
                    flame.localScale = Vector3.one * scale;
                    var renderer = flame.GetComponent<Renderer>();
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.enabled = false; // lit by HobView once there's heat
                    ring[i] = renderer;
                }
                return ring;
            }
        }

        // Fire colours over a particle's life, glowing additively and fading out; none at first.
        private static void Glow(ParticleSystem particles, Color start, Color middle, Color end)
        {
            var fade = particles.colorOverLifetime;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(middle, 0.45f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var main = particles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            // From a ring just outside the pan's edge, so a pan on the fire doesn't hide them.
            var shape = particles.shape;
            shape.radiusThickness = 0f;
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = PilotArt.EnsureFireMaterial();
        }

        /// <summary>The pan: a steel body (PanView still tints it with heat) and a wooden handle.</summary>
        public static void DressPan(GameObject body, GameObject handle)
        {
            body.GetComponent<Renderer>().sharedMaterial = EnsureToonMaterial("Pan", PanSteel);
            handle.GetComponent<Renderer>().sharedMaterial = EnsureToonMaterial("PanHandle", HandleWood, HandleGrain, grainScale: 60f);
        }

        private static Transform AddPart(Transform parent, string name, string mesh, Material material)
        {
            var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, parent, material, keepCollider: false);
            if (PilotArt.TryUseMesh(part, "SM_Hob", mesh))
                return part.transform;
            Object.DestroyImmediate(part);
            return null;
        }
    }
}
