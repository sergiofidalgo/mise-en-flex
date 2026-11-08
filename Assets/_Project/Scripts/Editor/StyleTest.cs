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
        private static readonly Color HobTopCharcoal = new Color(0.2f, 0.2f, 0.23f);
        private static readonly Color DialBlack = new Color(0.12f, 0.12f, 0.13f);
        private static readonly Color WindowSmoked = new Color(0.14f, 0.15f, 0.18f);
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

        /// <summary>The hob's body, top, dial, oven window and burner, each in its own flat colour.</summary>
        public static void DressHob(Transform hob, GameObject burner)
        {
            hob.Find("Body").GetComponent<Renderer>().sharedMaterial = EnsureToonMaterial("HobBody", HobSteel);
            burner.GetComponent<Renderer>().sharedMaterial = EnsureToonMaterial("Burner", BurnerDark);
            AddPart(hob, "Top", "HobTop", EnsureToonMaterial("HobTop", HobTopCharcoal));
            AddPart(hob, "Dial", "HobDial", EnsureToonMaterial("HobDial", DialBlack));
            AddPart(hob, "Window", "HobWindow", EnsureToonMaterial("HobWindow", WindowSmoked));
        }

        /// <summary>The pan: a steel body (PanView still tints it with heat) and a wooden handle.</summary>
        public static void DressPan(GameObject body, GameObject handle)
        {
            body.GetComponent<Renderer>().sharedMaterial = EnsureToonMaterial("Pan", PanSteel);
            handle.GetComponent<Renderer>().sharedMaterial = EnsureToonMaterial("PanHandle", HandleWood, HandleGrain, grainScale: 60f);
        }

        private static void AddPart(Transform parent, string name, string mesh, Material material)
        {
            var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, parent, material, keepCollider: false);
            if (!PilotArt.TryUseMesh(part, "SM_Hob", mesh))
                Object.DestroyImmediate(part);
        }
    }
}
