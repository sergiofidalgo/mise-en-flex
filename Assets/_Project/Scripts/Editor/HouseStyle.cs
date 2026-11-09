using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// The look every model follows, from the user's vision image (decision 2026-11-08): warm
    /// afternoon light, soft shadows, and dark-brown ink outlines rather than black ones. The ink
    /// and the light bands are set by <see cref="RenderLook"/> and ToonCommon.hlsl. The kitchen builder applies it to the scene's light and to every toon
    /// material, so a change here reaches everything on the next build.
    /// </summary>
    public static class HouseStyle
    {
        private const string MaterialsFolder = "Assets/_Project/Art/Materials";

        // The ink's house brown (RenderLook mixes a little of it into the screen-space outlines).
        public static readonly Color OutlineColor = new Color(0.2f, 0.13f, 0.09f);

        // A sun from the top left, as in the vision image, only slightly warm (the user found the
        // scene too yellow, 2026-11-08), with soft, not-quite-black shadows.
        private static readonly Color SunColor = new Color(1f, 0.97f, 0.92f);
        // High and from the left, almost square to the camera (the user found things flat,
        // 2026-11-08): tops land in the toon ramp's lit band, fronts in its mid band and right-hand
        // sides in its shadow band, so every box reads as a box. Behind the camera, as before, tops
        // and fronts were both fully lit and only the outlines told them apart.
        private static readonly Vector3 SunAngles = new Vector3(58f, 96f, 0f);
        private const float SunIntensity = 1.15f;
        // Soft enough that the floor under the counters doesn't go dark (the user's note, 2026-11-08).
        private const float ShadowStrength = 0.45f;

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

        /// <summary>
        /// A room material (MAT_Room_<paramref name="name"/>), created or brought up to date with the given
        /// pattern: TILES, PLANKS, WALL, or the city's BRICK and STONE. Patterns are in world space, so any
        /// floor or wall shows them at true size.
        /// </summary>
        public static Material RoomMaterial(string name, string pattern, System.Action<Material> set)
        {
            var path = $"{MaterialsFolder}/MAT_Room_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var isNew = material == null;
            if (isNew)
                material = new Material(Shader.Find(RoomShader));

            var patterns = new[] { "TILES", "PLANKS", "WALL", "BRICK", "STONE" };
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

        /// <summary>
        /// Smooth edges (the user's note, 2026-11-08): on top of the pipeline's 4x MSAA, which
        /// smooths geometry edges, SMAA smooths the steps inside surfaces too (toon light bands,
        /// thin lines). SMAA runs with post-processing, which adds nothing else: the PC pipeline
        /// carries no sample effects profile.
        /// </summary>
        public static void SmoothEdges(Camera camera)
        {
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(camera);
            data.renderPostProcessing = true;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = UnityEngine.Rendering.Universal.AntialiasingQuality.High;
            camera.allowMSAA = true;
        }

        /// <summary>Sets the scene's sun and sky light. The scene must be the active one.</summary>
        public static void LightScene(Light sun)
        {
            sun.transform.rotation = Quaternion.Euler(SunAngles);
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
        /// Gives every toon material the house outline colour. Nothing draws it since the hull
        /// outlines were removed (decision 2026-11-08), but materials keep the property; the shadow
        /// side's colour is the toon ramp's in ToonCommon.hlsl.
        /// </summary>
        public static void ApplyToMaterials()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null || !material.HasProperty("_OutlineColor"))
                    continue;

                material.SetColor("_OutlineColor", OutlineColor);
                EditorUtility.SetDirty(material);
            }
        }
    }
}
