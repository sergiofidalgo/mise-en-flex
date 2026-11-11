using Game.Gameplay;
using Game.Presentation;
using Game.Presentation.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Puts the button pictures on the appliances (<see cref="ApplianceHints"/>, the 2026-11-11
    /// playtest round) into a kitchen: the component on the HUD object, the control-glyph kit's
    /// button and trigger bodies, and the over-the-chef material. Part of Build Greybox Kitchen;
    /// Tools/Game/Add Appliance Hints and Review Gate adds it, and the HUD's review gate, to the open kitchen without a rebuild.
    /// </summary>
    public static class ApplianceHintsBuilder
    {
        private const string MaterialPath = "Assets/_Project/Art/Materials/MAT_FX_HintOnTop.mat";

        public static ApplianceHints Add(GameObject hud, HudStyle style, KitchenCompositionRoot root)
        {
            if (!hud.TryGetComponent(out ApplianceHints hints))
                hints = hud.AddComponent<ApplianceHints>();
            var so = new SerializedObject(hints);
            so.FindProperty("_root").objectReferenceValue = root;
            so.FindProperty("_style").objectReferenceValue = style;
            so.FindProperty("_button").objectReferenceValue = ControlGlyphArt.Sprite(ControlGlyphArt.Button);
            so.FindProperty("_trigger").objectReferenceValue = ControlGlyphArt.Sprite(ControlGlyphArt.Trigger);
            so.FindProperty("_material").objectReferenceValue = EnsureMaterial();
            so.ApplyModifiedPropertiesWithoutUndo();
            return hints;
        }

        /// <summary>Hides the kitchen's HUD while the dashboard is up (<see cref="ReviewHidesKitchenHud"/>).</summary>
        public static void AddReviewGate(GameObject hud, KitchenCompositionRoot root)
        {
            if (!hud.TryGetComponent(out ReviewHidesKitchenHud gate))
                gate = hud.AddComponent<ReviewHidesKitchenHud>();
            var so = new SerializedObject(gate);
            so.FindProperty("_root").objectReferenceValue = root;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static Material EnsureMaterial()
        {
            var shader = Shader.Find("Game/FX/HintOnTop");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            material.renderQueue = -1; // the shader's own: after the transparents
            EditorUtility.SetDirty(material);
            return material;
        }

        [MenuItem("Tools/Game/Add Appliance Hints and Review Gate")]
        private static void AddToOpenKitchen()
        {
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            var prompts = Object.FindFirstObjectByType<PromptHud>();
            if (root == null || prompts == null)
            {
                Debug.LogWarning("[ApplianceHints] Open a kitchen scene first.");
                return;
            }
            Add(prompts.gameObject, GreyboxHud.EnsureStyle(), root);
            AddReviewGate(prompts.gameObject, root);
            EditorSceneManager.MarkSceneDirty(prompts.gameObject.scene);
            Debug.Log("[ApplianceHints] Added to the open kitchen. Save the scene to keep it.");
        }
    }
}
