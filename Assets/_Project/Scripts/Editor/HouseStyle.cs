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
        public static readonly Color ShadowTint = new Color(0.76f, 0.66f, 0.64f);

        // A warm sun from the top left, as in the vision image, with soft, not-quite-black shadows.
        private static readonly Color SunColor = new Color(1f, 0.93f, 0.82f);
        private const float SunIntensity = 1.15f;
        private const float ShadowStrength = 0.6f;

        // Sky light: warm from above, a little darker round the sides and below.
        private static readonly Color AmbientSky = new Color(0.66f, 0.62f, 0.58f);
        private static readonly Color AmbientEquator = new Color(0.52f, 0.46f, 0.4f);
        private static readonly Color AmbientGround = new Color(0.3f, 0.25f, 0.21f);

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
