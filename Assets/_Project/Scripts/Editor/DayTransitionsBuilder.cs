using Game.Gameplay;
using Game.Presentation.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Puts the transitions into and out of service (<see cref="DayTransitions"/>, the 2026-11-11
    /// playtest round) into a kitchen, on an object of their own beside the money card. Part of
    /// Build Greybox Kitchen; Tools/Game/Add Day Transitions adds them to the open kitchen without a rebuild.
    /// </summary>
    public static class DayTransitionsBuilder
    {
        private const string ShaderPath = "Assets/_Project/Art/Shaders/SH_UiShutter.shader";

        public static DayTransitions Add(HudStyle style, KitchenCompositionRoot root, ReviewDashboard dashboard)
        {
            var transitions = Object.FindFirstObjectByType<DayTransitions>(FindObjectsInactive.Include);
            if (transitions == null)
                transitions = new GameObject("DayTransitions").AddComponent<DayTransitions>();
            var so = new SerializedObject(transitions);
            so.FindProperty("_root").objectReferenceValue = root;
            so.FindProperty("_style").objectReferenceValue = style;
            so.FindProperty("_dashboard").objectReferenceValue = dashboard;
            so.FindProperty("_shutterShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            return transitions;
        }

        [MenuItem("Tools/Game/Add Day Transitions")]
        private static void AddToOpenKitchen()
        {
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            var dashboard = Object.FindFirstObjectByType<ReviewDashboard>(FindObjectsInactive.Include);
            if (root == null || dashboard == null)
            {
                Debug.LogWarning("[DayTransitions] Open a kitchen scene first.");
                return;
            }
            var transitions = Add(GreyboxHud.EnsureStyle(), root, dashboard);
            EditorSceneManager.MarkSceneDirty(transitions.gameObject.scene);
            Debug.Log("[DayTransitions] Added to the open kitchen. Save the scene to keep it.");
        }
    }
}
