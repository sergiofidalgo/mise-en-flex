using Game.Data;
using Game.Gameplay.Office;
using Game.Presentation.Hud;
using Game.Presentation.Office;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The tailor's working parts (decision 2026-11-09, plan in docs/plans/tailor.md): each colour
    /// plinth becomes the dressing spot for its player slot, and one HUD draws their cards.
    /// </summary>
    public static partial class OfficeBuilder
    {
        // Where BuildTailor puts the plinths, slot by slot (Player 1's first).
        private static readonly Vector3[] TailorPlinths =
        {
            new Vector3(-15.4f, 0f, 9.8f), new Vector3(-13.25f, 0f, 10.6f), new Vector3(-11.1f, 0f, 9.8f),
        };

        /// <summary>The players' colours in slot order, for the plinths (decision 2026-11-09: colour is fixed by slot).</summary>
        private static Color[] PlinthColours()
        {
            var coop = GreyboxAssets.EnsureAsset<CoopSettings>(SettingsFolder + "/CoopSettings.asset");
            var colours = new Color[TailorPlinths.Length];
            for (var i = 0; i < colours.Length; i++)
                colours[i] = coop.GetPlayerColor(i);
            return colours;
        }

        private static void AddTailor(Transform rooms)
        {
            var room = rooms.Find("Tailor");
            var plinths = new TailorPlinth[TailorPlinths.Length];
            for (var i = 0; i < plinths.Length; i++)
            {
                var spot = new GameObject($"TailorPlinth_P{i + 1}").transform;
                spot.SetParent(room != null ? room : rooms, false);
                spot.position = TailorPlinths[i];
                var plinth = new SerializedObject(spot.gameObject.AddComponent<TailorPlinth>());
                plinth.FindProperty("_slot").intValue = i;
                plinth.ApplyModifiedPropertiesWithoutUndo();
                plinths[i] = spot.GetComponent<TailorPlinth>();
            }

            var hud = new SerializedObject(new GameObject("TailorHud").AddComponent<TailorCardHud>());
            hud.FindProperty("_style").objectReferenceValue = AssetDatabase.LoadAssetAtPath<HudStyle>(SettingsFolder + "/HudStyle.asset");
            hud.FindProperty("_coop").objectReferenceValue = GreyboxAssets.EnsureAsset<CoopSettings>(SettingsFolder + "/CoopSettings.asset");
            var list = hud.FindProperty("_plinths");
            list.arraySize = plinths.Length;
            for (var i = 0; i < plinths.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = plinths[i];
            hud.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
