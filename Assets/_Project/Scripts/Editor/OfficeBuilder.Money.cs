using Game.Gameplay.Office;
using Game.Presentation.Hud;
using Game.Presentation.Menu;
using Game.Presentation.Office;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static partial class OfficeBuilder
    {
        /// <summary>
        /// The campaign's money, top right (decision 2026-11-10), wired to the office's campaign and
        /// kept off with the rest of the floor's HUD until the opening's camera lands. Runs after
        /// AddOpening and AddMiniature, which make what it reads.
        /// </summary>
        private static void AddMoneyCard()
        {
            var card = new GameObject("MoneyCard");
            var source = new SerializedObject(card.AddComponent<OfficeMoneySource>());
            source.FindProperty("_office").objectReferenceValue = Object.FindFirstObjectByType<OfficeCampaign>();
            source.FindProperty("_table").objectReferenceValue = Object.FindFirstObjectByType<MiniatureTable>();
            source.ApplyModifiedPropertiesWithoutUndo();

            var hud = new SerializedObject(card.AddComponent<MoneyCardHud>());
            hud.FindProperty("_style").objectReferenceValue = AssetDatabase.LoadAssetAtPath<HudStyle>(SettingsFolder + "/HudStyle.asset");
            hud.FindProperty("_pause").objectReferenceValue = Object.FindFirstObjectByType<PauseMenu>();
            hud.ApplyModifiedPropertiesWithoutUndo();

            var sequence = Object.FindFirstObjectByType<OpeningSequence>();
            if (sequence == null)
                return;
            var opening = new SerializedObject(sequence);
            var list = opening.FindProperty("_hudAfterLanding");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = card;
            opening.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
