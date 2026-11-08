using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// The look every model follows, from the user's vision image (decision 2026-11-08): warm
    /// afternoon light, soft shadows, warm shadow sides, and thin dark-brown outlines rather than
    /// thick black ones. The kitchen builder applies it to the scene's light and to every toon
    /// material, so a change here reaches everything on the next build.
    /// </summary>
    public static class HouseStyle
    {
        private const string MaterialsFolder = "Assets/_Project/Art/Materials";

        // Outlines: dark brown, and thin. Small things (plates, wires) set their own, thinner.
        public static readonly Color OutlineColor = new Color(0.2f, 0.13f, 0.09f);
        public const float OutlineWidth = 0.0035f;

        // The shadow side of a toon surface: its colour multiplied by this, a warm brown-rose.
        public static readonly Color ShadowTint = new Color(0.72f, 0.7f, 0.75f);

        // A sun from the top left, as in the vision image, only slightly warm (the user found the
        // scene too yellow, 2026-11-08), with soft, not-quite-black shadows.
        private static readonly Color SunColor = new Color(1f, 0.97f, 0.92f);
        private const float SunIntensity = 1.15f;
        private const float ShadowStrength = 0.6f;

        // Sky light: warm from above, a little darker round the sides and below.
        private static readonly Color AmbientSky = new Color(0.62f, 0.63f, 0.65f);
        private static readonly Color AmbientEquator = new Color(0.5f, 0.49f, 0.48f);
        private static readonly Color AmbientGround = new Color(0.3f, 0.28f, 0.27f);

        private const string RoomShader = "Game/Room/Surface";

        /// <summary>The kitchen floor: mid-tone warm grey tiles, a step lighter than the vision's slate.</summary>
        public static Material KitchenFloor() => RoomMaterial("KitchenFloor", "TILES", m =>
        {
            m.SetColor("_BaseColor", new Color(0.53f, 0.49f, 0.45f));
            m.SetColor("_SeamColor", new Color(0.38f, 0.34f, 0.3f));
            m.SetFloat("_Size", 0.5f);
            m.SetFloat("_SeamWidth", 0.014f);
            m.SetFloat("_Variation", 0.06f);
        });

        /// <summary>The dining floor: honey-coloured planks along x.</summary>
        public static Material DiningFloor() => RoomMaterial("DiningFloor", "PLANKS", m =>
        {
            m.SetColor("_BaseColor", new Color(0.68f, 0.46f, 0.28f));
            m.SetColor("_SeamColor", new Color(0.36f, 0.22f, 0.13f));
            m.SetFloat("_Size", 0.2f);
            m.SetFloat("_Length", 1.6f);
            m.SetFloat("_SeamWidth", 0.008f);
            m.SetFloat("_Variation", 0.1f);
            m.SetFloat("_Grain", 1f);
        });

        /// <summary>Walls: cream plaster over wood panelling, with a wooden rail and cap.</summary>
        public static Material Wall() => RoomMaterial("Wall", "WALL", m =>
        {
            m.SetColor("_BaseColor", new Color(0.92f, 0.9f, 0.85f));
            m.SetColor("_PanelColor", new Color(0.6f, 0.4f, 0.24f));
            m.SetColor("_CapColor", new Color(0.44f, 0.28f, 0.17f));
            m.SetColor("_SeamColor", new Color(0.33f, 0.21f, 0.13f));
            m.SetFloat("_PanelHeight", 0.55f);
            m.SetFloat("_PanelWidth", 0.3f);
            m.SetFloat("_SeamWidth", 0.01f);
            m.SetFloat("_Variation", 0.05f);
            m.SetFloat("_Grain", 1f);
        });

        // A room material (MAT_Room_<name>), created or brought up to date with the given pattern.
        private static Material RoomMaterial(string name, string pattern, System.Action<Material> set)
        {
            var path = $"{MaterialsFolder}/MAT_Room_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var isNew = material == null;
            if (isNew)
                material = new Material(Shader.Find(RoomShader));

            var patterns = new[] { "TILES", "PLANKS", "WALL" };
            foreach (var keyword in patterns)
                material.DisableKeyword($"_PATTERN_{keyword}");
            material.EnableKeyword($"_PATTERN_{pattern}");
            material.SetFloat("_Pattern", System.Array.IndexOf(patterns, pattern));
            set(material);

            if (isNew)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Sets the scene's sun and sky light. The scene must be the active one.</summary>
        public static void LightScene(Light sun)
        {
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = ShadowStrength;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>
        /// Gives every toon material the house outline colour and shadow tint, and caps outline
        /// widths at the house width, leaving thinner ones as they are.
        /// </summary>
        public static void ApplyToMaterials()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null || !material.HasProperty("_OutlineColor"))
                    continue;

                material.SetColor("_OutlineColor", OutlineColor);
                material.SetColor("_ShadowTint", ShadowTint);
                if (material.GetFloat("_OutlineWidth") > OutlineWidth)
                    material.SetFloat("_OutlineWidth", OutlineWidth);
                EditorUtility.SetDirty(material);
            }
        }
    }
}
