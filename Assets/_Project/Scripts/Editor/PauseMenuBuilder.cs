using Game.Data;
using Game.Gameplay;
using Game.Presentation.Hud;
using Game.Presentation.Menu;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Adds the pause menu (decision 2026-11-10) to the scene being built: the kitchen builder and
    /// the office builder both call it. The menu draws its own canvas in code, so it needs only its
    /// settings and, in the kitchen, the root and day loop.
    /// </summary>
    public static class PauseMenuBuilder
    {
        private const string SettingsFolder = "Assets/_Project/Data/Settings";

        public static PauseMenu Add(HudStyle style, CoopSettings coop, KitchenCompositionRoot root = null, DayLoop dayLoop = null)
        {
            var menu = new GameObject("PauseMenu").AddComponent<PauseMenu>();
            var so = new SerializedObject(menu);
            so.FindProperty("_style").objectReferenceValue = style;
            so.FindProperty("_realism").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RealismSettings>(SettingsFolder + "/RealismSettings.asset");
            so.FindProperty("_coop").objectReferenceValue = coop;
            so.FindProperty("_root").objectReferenceValue = root;
            so.FindProperty("_dayLoop").objectReferenceValue = dayLoop;
            so.ApplyModifiedPropertiesWithoutUndo();
            return menu;
        }
    }
}
