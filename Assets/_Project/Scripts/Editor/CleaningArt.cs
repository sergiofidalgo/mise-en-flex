using Game.Data;
using Game.Gameplay;
using Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Adds the dirt and cleaning game's view to the kitchen (Phase 5, decision 2026-11-10). Called
    /// by the kitchen builder; safe to run again.
    /// </summary>
    public static class CleaningArt
    {
        private const string CoopSettingsPath = "Assets/_Project/Data/Settings/CoopSettings.asset";

        /// <summary>Adds the view to the open kitchen scene without rebuilding it.</summary>
        [MenuItem("Tools/Game/Apply Cleaning View")]
        public static void ApplyToOpenScene()
        {
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            if (root == null)
            {
                Debug.LogWarning("Open the kitchen scene first.");
                return;
            }
            AddTo(root);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        public static void AddTo(KitchenCompositionRoot root)
        {
            var view = Object.FindFirstObjectByType<CleaningView>();
            if (view == null)
                view = new GameObject("CleaningView").AddComponent<CleaningView>();
            var so = new SerializedObject(view);
            so.FindProperty("_root").objectReferenceValue = root;
            so.FindProperty("_settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CoopSettings>(CoopSettingsPath);
            so.FindProperty("_onTop").objectReferenceValue = WashRing.EnsureToonMaterial();
            // White, so each patch and tool tints it with its own colour.
            so.FindProperty("_toon").objectReferenceValue = StyleTest.EnsureToonMaterial("Cleaning", Color.white);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
