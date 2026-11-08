using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// A debug switch for comparing the screen-space ink with the old inverted-hull outlines
    /// (decision 2026-11-08): the first press sets every toon material's _OutlineWidth to 0, the
    /// next puts each one's own width back (plates and food use thinner ones than the rest). The
    /// widths are kept in Library, outside version control. Don't commit materials while the hulls
    /// are off.
    /// </summary>
    public static class HullOutlineToggle
    {
        private const string MaterialsFolder = "Assets/_Project/Art/Materials";
        private static readonly string SavedWidths = Path.Combine("Library", "HullOutlineWidths.txt");

        [MenuItem("Tools/Game/Debug/Toggle Hull Outlines")]
        public static void Toggle()
        {
            if (File.Exists(SavedWidths))
                Restore();
            else
                TurnOff();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Game/Debug/Toggle Hull Outlines", true)]
        private static bool ToggleLabel()
        {
            Menu.SetChecked("Tools/Game/Debug/Toggle Hull Outlines", !File.Exists(SavedWidths));
            return true;
        }

        private static void TurnOff()
        {
            var lines = new List<string>();
            foreach (var (guid, material) in ToonMaterials())
            {
                lines.Add($"{guid} {material.GetFloat("_OutlineWidth").ToString("R", System.Globalization.CultureInfo.InvariantCulture)}");
                material.SetFloat("_OutlineWidth", 0f);
                EditorUtility.SetDirty(material);
            }
            File.WriteAllLines(SavedWidths, lines);
            Debug.Log($"[HullOutlines] Off on {lines.Count} materials; press again to restore.");
        }

        private static void Restore()
        {
            var restored = 0;
            foreach (var line in File.ReadAllLines(SavedWidths))
            {
                var parts = line.Split(' ');
                if (parts.Length != 2 || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var width))
                    continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(parts[0]));
                if (material == null || !material.HasProperty("_OutlineWidth"))
                    continue;
                material.SetFloat("_OutlineWidth", width);
                EditorUtility.SetDirty(material);
                restored++;
            }
            File.Delete(SavedWidths);
            Debug.Log($"[HullOutlines] Restored {restored} materials.");
        }

        private static IEnumerable<(string Guid, Material Material)> ToonMaterials() =>
            AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder })
                .Select(guid => (guid, AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid))))
                .Where(entry => entry.Item2 != null && entry.Item2.HasProperty("_OutlineWidth"));
    }
}
