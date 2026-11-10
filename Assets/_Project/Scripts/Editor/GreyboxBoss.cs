using Game.Gameplay;
using Game.Gameplay.Debt;
using Game.Gameplay.Stations;
using Game.Presentation.Hud;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The boss in the kitchen (Phase 6, milestone 4): his morning visits (BossVisitDirector, with
    /// the boss and bodyguard prefabs the office builder dresses) and the personal mobile in the
    /// bottom right corner (BossPhoneHud). Run by Build Greybox Kitchen.
    /// </summary>
    public static class GreyboxBoss
    {
        public static void Build(KitchenCompositionRoot root, Camera camera, HudStyle style)
        {
            var (bossPrefab, guardPrefab) = OfficeBuilder.BuildVisitorPrefabs();
            var door = Object.FindFirstObjectByType<StreetDoor>();

            var holder = new GameObject("Boss");
            var director = holder.AddComponent<BossVisitDirector>();
            var visit = new SerializedObject(director);
            visit.FindProperty("_root").objectReferenceValue = root;
            visit.FindProperty("_layoutEditor").objectReferenceValue = Object.FindFirstObjectByType<LayoutEditor>();
            visit.FindProperty("_door").objectReferenceValue = door != null ? door.transform : null;
            visit.FindProperty("_bossPrefab").objectReferenceValue = bossPrefab;
            visit.FindProperty("_guardPrefab").objectReferenceValue = guardPrefab;
            visit.ApplyModifiedPropertiesWithoutUndo();

            var loop = root.GetComponent<DayLoop>();
            if (loop != null)
            {
                var loopSo = new SerializedObject(loop);
                loopSo.FindProperty("_boss").objectReferenceValue = director;
                loopSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var phone = new SerializedObject(holder.AddComponent<BossPhoneHud>());
            phone.FindProperty("_style").objectReferenceValue = style;
            phone.FindProperty("_root").objectReferenceValue = root;
            phone.FindProperty("_visit").objectReferenceValue = director;
            phone.FindProperty("_camera").objectReferenceValue = camera;
            phone.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
