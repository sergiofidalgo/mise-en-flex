using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Editor
{
    /// <summary>
    /// The PC pipeline's render settings for the look (decision 2026-11-08, "Screen-space ink,
    /// three-band ramp, banded SSAO"): ink outlines drawn over the screen from depth and normals at
    /// a constant on-screen width, ambient occlusion that the toon ramp bands into one flat contact
    /// shadow, and tighter shadows for the kitchen's short view. The kitchen builder applies them,
    /// so a change here reaches the project on the next build.
    /// </summary>
    public static class RenderLook
    {
        private const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        private const string PipelinePath = "Assets/Settings/PC_RPAsset.asset";
        private const string InkMaterialPath = "Assets/_Project/Art/Materials/MAT_Post_InkOutline.mat";
        private const string InkShader = "Game/Post/InkOutline";
        private const string InkFeatureName = "InkOutline";

        /// <summary>What the camera clears to, where nothing is drawn: a near-black warm brown, #120D0A.</summary>
        public static readonly Color ClearColor = new Color32(0x12, 0x0D, 0x0A, 0xFF);

        [MenuItem("Tools/Game/Apply Render Look")]
        public static void Apply()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                Debug.LogWarning($"[RenderLook] No renderer at {RendererPath}.");
                return;
            }
            SetAmbientOcclusion(renderer);
            SetInkPass(renderer);
            SetShadows();
            AssetDatabase.SaveAssets();
        }

        /// <summary>The camera clears to the dark backdrop rather than the sky.</summary>
        public static void Backdrop(Camera camera)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ClearColor;
        }

        // The renderer already has an SSAO feature: set it rather than add a second. Its settings
        // are internal to URP, so they're set through serialization. Values are the art brief's.
        private static void SetAmbientOcclusion(UniversalRendererData renderer)
        {
            var ssao = renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if (ssao == null)
            {
                Debug.LogWarning("[RenderLook] The renderer has no SSAO feature.");
                return;
            }
            var so = new SerializedObject(ssao);
            so.FindProperty("m_Settings.AOMethod").enumValueIndex = 0;              // Blue Noise
            so.FindProperty("m_Settings.Intensity").floatValue = 1f;
            so.FindProperty("m_Settings.Radius").floatValue = 0.25f;
            so.FindProperty("m_Settings.Falloff").floatValue = 30f;
            so.FindProperty("m_Settings.Source").enumValueIndex = 1;                // Depth Normals
            so.FindProperty("m_Settings.NormalSamples").enumValueIndex = 1;         // Medium
            so.FindProperty("m_Settings.Downsample").boolValue = false;
            so.FindProperty("m_Settings.BlurQuality").enumValueIndex = 0;           // High
            so.FindProperty("m_Settings.Samples").enumValueIndex = 1;               // Medium
            so.FindProperty("m_Settings.AfterOpaque").boolValue = false;
            so.FindProperty("m_Settings.DirectLightingStrength").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            ssao.SetActive(true);
            EditorUtility.SetDirty(ssao);
        }

        // The ink: a full-screen pass before post-processing (so SMAA smooths it too), reading the
        // depth and normals that SSAO's prepass already makes.
        private static void SetInkPass(UniversalRendererData renderer)
        {
            var feature = renderer.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f.name == InkFeatureName);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = InkFeatureName;
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
                LinkFeatures(renderer);
            }
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.bindDepthStencilAttachment = false;
            feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            feature.passMaterial = EnsureInkMaterial();
            feature.passIndex = 0;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
        }

        // The renderer keeps a map from each feature to its sub-asset id; URP's own editor fills it
        // when a feature is added by hand, so this does the same.
        private static void LinkFeatures(UniversalRendererData renderer)
        {
            var so = new SerializedObject(renderer);
            var map = so.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (var i = 0; i < renderer.rendererFeatures.Count; i++)
            {
                if (renderer.rendererFeatures[i] != null
                    && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out _, out long localId))
                    map.GetArrayElementAtIndex(i).longValue = localId;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The ink's settings, here so the build keeps the material true to them. Its brown is the
        // house outline colour, so that has one source. Softened after the user played it
        // (2026-11-08): thin, a shade of what's underneath, and mostly silhouettes.
        private static Material EnsureInkMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(InkMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find(InkShader));
                AssetDatabase.CreateAsset(material, InkMaterialPath);
            }
            material.SetColor("_InkColor", HouseStyle.OutlineColor);
            material.SetFloat("_InkColorMix", 0.25f);
            material.SetFloat("_InkShade", 0.45f);
            material.SetFloat("_InkOpacity", 0.5f);
            material.SetFloat("_Thickness", 1f);
            material.SetFloat("_MaxThickness", 2f);
            material.SetFloat("_DepthThreshold", 0.015f);
            material.SetFloat("_NormalThreshold", 1.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Shadows for a kitchen seen from ~10 m: a short distance in two cascades keeps them crisp,
        // and the larger biases keep counter tops free of shadow acne.
        private static void SetShadows()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                Debug.LogWarning($"[RenderLook] No pipeline asset at {PipelinePath}.");
                return;
            }
            pipeline.shadowDistance = 25f;
            pipeline.shadowCascadeCount = 2;
            pipeline.cascade2Split = 0.4f;
            pipeline.shadowDepthBias = 0.6f;
            pipeline.shadowNormalBias = 0.6f;
            var so = new SerializedObject(pipeline);
            so.FindProperty("m_SoftShadowQuality").intValue = (int)SoftShadowQuality.Low;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }
    }
}
