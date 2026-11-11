using System.Collections.Generic;
using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// What each piece in the shop is for, under its picture on the shop card (the user's call,
    /// 2026-11-11). Written from what the game actually does with it, so the card never promises
    /// more than the kitchen delivers. The builder writes them with the catalog; Tools/Game/Update
    /// Shop Descriptions writes them into the catalog as it stands, without rebuilding anything.
    /// </summary>
    public static class ShopDescriptions
    {
        private const string CatalogPath = "Assets/_Project/Data/Settings/FurnitureCatalog.asset";

        public static readonly IReadOnlyDictionary<string, string> ById = new Dictionary<string, string>
        {
            ["counter"] = "A plain worktop for pans, plates and food.",
            ["hob"] = "A flame on a dial. A pan left too hot can catch fire.",
            ["induction hob"] = "Steps hold the pan at a set heat. No flame, no fires.",
            ["grill"] = "Always lit, with a hot and a cool side. Move food across.",
            ["board"] = "Cuts onions, tomatoes and steaks. A helper needs one.",
            ["knife rack"] = "Keeps the kitchen's knives to hand beside the boards.",
            ["knife"] = "One more knife, so two cooks can cut at once.",
            ["pan"] = "One more pan, so more can cook at once.",
            ["plate"] = "One more clean plate for the pile.",
            ["shelf"] = "Dry storage for what doesn't need the cold.",
            ["fridge"] = "Cold storage: meat and fish stay fresh longer.",
            ["table"] = "Seats up to four. Each extra chair costs a little more.",
            ["bar"] = "A narrow table and stool for guests eating alone.",
        };

        public static string For(string id) => id != null && ById.TryGetValue(id, out var text) ? text : "";

        [MenuItem("Tools/Game/Update Shop Descriptions")]
        public static void Update()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FurnitureCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"[ShopDescriptions] Missing {CatalogPath}.");
                return;
            }
            Write(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[ShopDescriptions] Wrote the shop's descriptions.");
        }

        public static void Write(FurnitureCatalog catalog)
        {
            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("_entries");
            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_description").stringValue = For(entry.FindPropertyRelative("_id").stringValue);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }
    }
}
