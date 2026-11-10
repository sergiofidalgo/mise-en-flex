using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Data;
using Game.Presentation.Hud;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Tools/Game/Update Language Tables (decision 2026-11-09): collects every text players can read
    /// (what the code passes to Tr, words from data assets, the review voices) and merges it into each
    /// language's CSV, keeping translations already there. Also creates the LanguageTables asset and
    /// the Spanish table the first time, and gives every HUD style the tables.
    /// </summary>
    public static class LanguageTablesTool
    {
        private const string TablesPath = "Assets/_Project/Data/Settings/LanguageTables.asset";
        private const string CsvFolder = "Assets/_Project/Data/Localization";
        private const string ScriptsFolder = "Assets/_Project/Scripts";

        // Developer-only screens players never see.
        private static readonly string[] SkippedFiles = { "CookingDebugOverlay.cs" };

        // Data fields players read: ingredient and kitchenware ids (prompts show them), display
        // names, doneness levels, staff descriptions, the restaurant's style.
        private static readonly HashSet<string> DataFields = new HashSet<string> { "_typeId", "_displayName", "_description", "_styleName" };

        [MenuItem("Tools/Game/Update Language Tables")]
        public static void Update()
        {
            var tables = EnsureTables();
            var keys = Collect().ToList();
            foreach (var language in tables.Languages)
            {
                if (language?.Table == null)
                    continue;
                var path = AssetDatabase.GetAssetPath(language.Table);
                var merged = TranslationKeys.Merge(File.ReadAllText(path), keys);
                File.WriteAllText(path, merged);
                AssetDatabase.ImportAsset(path);
                var missing = TranslationKeys.Missing(merged).Count();
                Debug.Log($"[Languages] {language.Name} ({language.Code}): {keys.Select(k => k.Key).Distinct().Count()} texts, {missing} still to translate. {path}");
            }
            tables.Reload();
        }

        /// <summary>Every key with where it's from, UI first, then data, then the review voices.</summary>
        public static IEnumerable<(string Key, string Source)> Collect()
        {
            foreach (var file in Directory.GetFiles(ScriptsFolder, "*.cs", SearchOption.AllDirectories).OrderBy(f => f))
            {
                var unix = file.Replace('\\', '/');
                if (unix.Contains("/Editor/") || SkippedFiles.Contains(Path.GetFileName(file)))
                    continue;
                foreach (var key in TranslationKeys.InCode(File.ReadAllText(file)))
                    yield return (key, "ui: " + Path.GetFileNameWithoutExtension(file));
            }

            foreach (var type in new[] { "IngredientDefinition", "ItemCatalog", "StaffConfig", "FurnitureCatalog", "ServiceConfig" })
            {
                foreach (var guid in AssetDatabase.FindAssets("t:" + type))
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                    foreach (var key in DataStrings(asset))
                    {
                        yield return (key, "data: " + asset.name);
                        // Reviews and order lines use dish and doneness names in lower case ("steak, medium").
                        if (asset is IngredientDefinition && key.ToLowerInvariant() != key)
                            yield return (key.ToLowerInvariant(), "data, lower case: " + asset.name);
                    }
                }
            }
            // Doneness tags name food that has no level yet, or has gone past them.
            foreach (var key in new[] { "Raw", "Burnt", "raw", "burnt" })
                yield return (key, "data: doneness tags");

            // What the boss says on the phone and in person.
            foreach (var key in Game.Simulation.Campaign.BossCalls.AllLines())
                yield return (key, "boss");

            // The voices as the code writes them, and as the service config holds them now.
            foreach (var voice in ReviewVoiceDefaults.All())
            {
                foreach (var key in VoiceStrings(voice))
                    yield return (key, "review voice");
            }
            foreach (var guid in AssetDatabase.FindAssets("t:ServiceConfig"))
            {
                var config = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)));
                var voices = config.FindProperty("_voices");
                if (voices == null)
                    continue;
                var end = voices.GetEndProperty();
                var property = voices.Copy();
                while (property.Next(true) && !SerializedProperty.EqualContents(property, end))
                {
                    if (property.propertyType == SerializedPropertyType.String && !property.propertyPath.Contains("_styles") && !string.IsNullOrWhiteSpace(property.stringValue))
                        yield return (property.stringValue, "review voice");
                }
            }
        }

        private static IEnumerable<string> DataStrings(Object asset)
        {
            var property = new SerializedObject(asset).GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.String || string.IsNullOrWhiteSpace(property.stringValue))
                    continue;
                // Doneness levels are "_name" inside "_donenessLevels".
                if (DataFields.Contains(property.name) || property.name == "_name" && property.propertyPath.StartsWith("_donenessLevels"))
                    yield return property.stringValue;
            }
        }

        // Every string field in the voice, except which styles it speaks for.
        private static IEnumerable<string> VoiceStrings(object value)
        {
            if (value == null)
                yield break;
            if (value is string text)
            {
                if (!string.IsNullOrWhiteSpace(text))
                    yield return text;
                yield break;
            }
            if (value is IEnumerable list)
            {
                foreach (var item in list)
                {
                    foreach (var inner in VoiceStrings(item))
                        yield return inner;
                }
                yield break;
            }
            if (value.GetType().IsPrimitive || value.GetType().IsEnum || !(value.GetType().Namespace ?? "").StartsWith("Game"))
                yield break;
            foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (field.Name == "_styles")
                    continue;
                foreach (var inner in VoiceStrings(field.GetValue(value)))
                    yield return inner;
            }
        }

        // The tables asset and the Spanish CSV, made once; every HUD style pointed at them.
        private static LanguageTables EnsureTables()
        {
            Directory.CreateDirectory(CsvFolder);
            var spanishPath = $"{CsvFolder}/es.csv";
            if (!File.Exists(spanishPath))
            {
                File.WriteAllText(spanishPath, "english,translation,note\n");
                AssetDatabase.ImportAsset(spanishPath);
            }

            var tables = AssetDatabase.LoadAssetAtPath<LanguageTables>(TablesPath);
            if (tables == null)
            {
                tables = ScriptableObject.CreateInstance<LanguageTables>();
                AssetDatabase.CreateAsset(tables, TablesPath);
            }
            if (tables.Languages.Count == 0)
            {
                var serialized = new SerializedObject(tables);
                var list = serialized.FindProperty("_languages");
                list.arraySize = 1;
                var spanish = list.GetArrayElementAtIndex(0);
                spanish.FindPropertyRelative("_code").stringValue = "es";
                spanish.FindPropertyRelative("_name").stringValue = "Español";
                spanish.FindPropertyRelative("_table").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(spanishPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(tables);
            }

            foreach (var guid in AssetDatabase.FindAssets("t:HudStyle"))
            {
                var style = new SerializedObject(AssetDatabase.LoadAssetAtPath<HudStyle>(AssetDatabase.GUIDToAssetPath(guid)));
                var field = style.FindProperty("_languages");
                if (field.objectReferenceValue == tables)
                    continue;
                field.objectReferenceValue = tables;
                style.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
            return tables;
        }
    }
}
