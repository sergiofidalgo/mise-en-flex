using Game.Gameplay;
using Game.Presentation;
using Game.Presentation.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Adds the timing gauges to the kitchen (decision 2026-11-11): the flip's crust strip under the
    /// pans and the cutting dial over the boards, as toon pieces in the scene. They replace the flat
    /// HUD gauges (<see cref="FlipGaugeHud"/>, <see cref="ChopDialHud"/>), which the menu item removes
    /// from an open kitchen and the HUD builder no longer adds. Called by the kitchen builder; safe
    /// to run again.
    /// </summary>
    public static class TimingGaugeArt
    {
        private const string ObjectName = "TimingGauges";

        /// <summary>Adds the gauges to the open kitchen scene without rebuilding it.</summary>
        [MenuItem("Tools/Game/Apply Timing Gauges")]
        public static void ApplyToOpenScene()
        {
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            if (root == null)
            {
                Debug.LogWarning("Open the kitchen scene first.");
                return;
            }
            AddTo(root);
            CleaningArt.AddTo(root); // the stains' translucent material, without a rebuild
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log("[TimingGauges] Added the crust strip and the cutting dial; the flat HUD gauges are removed. Save the scene to keep it.");
        }

        /// <summary>Batch: opens a kitchen scene (-scenePath, default the greybox), applies and saves it.</summary>
        public static void ApplyAndSave()
        {
            var path = "Assets/_Project/Scenes/Kitchen_Greybox.unity";
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == "-scenePath")
                    path = args[i + 1];
            var scene = EditorSceneManager.OpenScene(path);
            ApplyToOpenScene();
            EditorSceneManager.SaveScene(scene);
        }

        public static void AddTo(KitchenCompositionRoot root)
        {
            foreach (var old in Object.FindObjectsByType<FlipGaugeHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(old);
            foreach (var old in Object.FindObjectsByType<ChopDialHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(old);

            var holder = GameObject.Find(ObjectName);
            if (holder == null)
                holder = new GameObject(ObjectName);
            var material = WashRing.EnsureToonMaterial();

            if (!holder.TryGetComponent(out FlipCrustGaugeView flip))
                flip = holder.AddComponent<FlipCrustGaugeView>();
            var flipSo = new SerializedObject(flip);
            flipSo.FindProperty("_root").objectReferenceValue = root;
            flipSo.FindProperty("_onTop").objectReferenceValue = material;
            flipSo.ApplyModifiedPropertiesWithoutUndo();

            if (!holder.TryGetComponent(out CleanHintView clean))
                clean = holder.AddComponent<CleanHintView>();
            var cleanSo = new SerializedObject(clean);
            cleanSo.FindProperty("_root").objectReferenceValue = root;
            cleanSo.FindProperty("_onTop").objectReferenceValue = material;
            cleanSo.FindProperty("_hints").objectReferenceValue = Object.FindFirstObjectByType<ApplianceHints>(FindObjectsInactive.Include);
            cleanSo.ApplyModifiedPropertiesWithoutUndo();

            // The appliance hints' LT RT as toon keys, the gauges' family.
            foreach (var hints in Object.FindObjectsByType<ApplianceHints>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var hintsSo = new SerializedObject(hints);
                hintsSo.FindProperty("_onTop").objectReferenceValue = material;
                hintsSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // The ingredient wheel, over storage, stalls and trays, in place of the flat card.
            if (!holder.TryGetComponent(out SlotWheelView wheel))
                wheel = holder.AddComponent<SlotWheelView>();
            var wheelSo = new SerializedObject(wheel);
            wheelSo.FindProperty("_onTop").objectReferenceValue = material;
            wheelSo.FindProperty("_iconMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/MAT_FX_HintOnTop.mat");
            wheelSo.FindProperty("_icons").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Game.Data.IngredientIcons>("Assets/_Project/Data/UI/IngredientIcons.asset");
            wheelSo.FindProperty("_style").objectReferenceValue = GreyboxHud.EnsureStyle();
            wheelSo.FindProperty("_settings").objectReferenceValue = WashRing.FindSettings();
            wheelSo.ApplyModifiedPropertiesWithoutUndo();

            if (!holder.TryGetComponent(out ChopGaugeView chop))
                chop = holder.AddComponent<ChopGaugeView>();
            var chopSo = new SerializedObject(chop);
            chopSo.FindProperty("_onTop").objectReferenceValue = material;
            chopSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
