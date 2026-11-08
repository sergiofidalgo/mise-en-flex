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
        private static readonly Color HobTopSteel = new Color(0.68f, 0.7f, 0.74f);
        private static readonly Color KnobBlack = new Color(0.13f, 0.13f, 0.14f);
        private static readonly Color DoorSteel = new Color(0.64f, 0.66f, 0.7f);
        private static readonly Color WindowSmoked = new Color(0.15f, 0.16f, 0.19f);
        private static readonly Color Chrome = new Color(0.92f, 0.93f, 0.95f);
        private static readonly Color CastIron = new Color(0.12f, 0.12f, 0.13f);

        // The flame ring: tongues just inside the pan's edge (0.22 m), leaning out, so they show
        // past it at high heat. Their bases sit on the burner, under the grate.
        private const int FlameCount = 12;
        private const float FlameRadius = 0.2f;
        private const float FlameBase = 0.903f;
        private const float FlameLean = 50f;
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

        // The heat knob sits on the top's front-right corner, clear of the burner and the pan's
        // handle, next to the pips along the back (decision 2026-11-08).
        private static readonly Vector3 KnobPosition = new Vector3(0.37f, 0.9f, -0.37f);

        /// <summary>
        /// The range's body, top, heat knob, burner, grate and oven door, each in its own flat
        /// colour, and a ring of flames round the burner for the hob's view to grow with the heat.
        /// Returns the flames; empty without the model.
        /// </summary>
        public static Renderer[] DressHob(Transform hob, GameObject burner)
        {
            hob.Find("Body").GetComponent<Renderer>().sharedMaterial = EnsureToonMaterial("HobBody", HobSteel);
            burner.GetComponent<Renderer>().sharedMaterial = EnsureToonMaterial("Burner", BurnerDark);
            AddPart(hob, "Top", "HobTop", EnsureToonMaterial("HobTop", HobTopSteel));
            AddPart(hob, "Door", "HobDoor", EnsureToonMaterial("HobDoor", DoorSteel));
            AddPart(hob, "Window", "HobWindow", EnsureToonMaterial("HobWindow", WindowSmoked));
            AddPart(hob, "Handle", "HobHandle", EnsureToonMaterial("HobHandle", Chrome));
            AddPart(hob, "Grate", "HobGrate", EnsureToonMaterial("HobGrate", CastIron));
            var knob = AddPart(hob, Game.Presentation.HobView.KnobName, "HobDial", EnsureToonMaterial("HobDial", KnobBlack));
            if (knob != null)
                knob.localPosition = KnobPosition;

            // Flame tongues round the burner, leaning out so a high flame licks past the pan's edge.
            var flames = new Renderer[FlameCount];
            var flameMaterial = PilotArt.EnsureUnlitMaterial("Flame", Color.white);
            for (var i = 0; i < FlameCount; i++)
            {
                var flame = AddPart(hob, $"Flame_{i + 1}", "HobFlame", flameMaterial);
                if (flame == null)
                    return System.Array.Empty<Renderer>();
                var angle = 360f * i / FlameCount;
                var outward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                flame.localPosition = new Vector3(0f, FlameBase, 0f) + outward * FlameRadius;
                flame.localRotation = Quaternion.AngleAxis(FlameLean, Vector3.Cross(Vector3.up, outward));
                var renderer = flame.GetComponent<Renderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                flames[i] = renderer;
            }
            return flames;
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
