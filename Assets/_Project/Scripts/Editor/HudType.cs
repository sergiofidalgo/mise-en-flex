using Game.Presentation.Hud;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Gives the in-game HUD the art bible's type: Nunito ExtraBold for words, Fredoka SemiBold for
    /// numbers and button letters, both with a thin Ink outline so text reads over the scene, then
    /// rebuilds the HUD prefabs that bake the font in, and raises the text sizes stored on the HUD in
    /// any open scene (save the scene afterwards). Safe to run again.
    /// Batch: -executeMethod Game.Editor.HudType.Apply
    /// </summary>
    public static class HudType
    {
        public const string BodyFont = "Nunito-ExtraBold";
        public const string DisplayFont = "Fredoka-SemiBold";
        private const string HudStylePath = "Assets/_Project/Data/Settings/HudStyle.asset";

        [MenuItem("Tools/Game/Apply HUD Type")]
        public static void Apply()
        {
            var style = AssetDatabase.LoadAssetAtPath<HudStyle>(HudStylePath);
            if (style == null)
            {
                Debug.LogError($"[HudType] Missing {HudStylePath}.");
                return;
            }

            var body = MenuArt.Font(BodyFont);
            var display = MenuArt.Font(DisplayFont);
            if (body == null || display == null)
                return;
            Outline(body, style);
            Outline(display, style);

            var so = new SerializedObject(style);
            so.FindProperty("_font").objectReferenceValue = body;
            so.FindProperty("_displayFont").objectReferenceValue = display;
            // The art bible's minimum for anything read during play; older assets kept smaller sizes.
            Raise(so, "_fontSize", 28f);
            Raise(so, "_labelFontSize", 28f);
            Raise(so, "_badgeSize", 40f);
            var background = so.FindProperty("_cardBackground");
            var color = background.colorValue;
            color.a = Mathf.Max(color.a, 0.92f);
            background.colorValue = color;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(style);

            GreyboxHud.RebuildPrefabs(style);
            AssetDatabase.SaveAssets();
            var updated = UpdateOpenScenes();
            Debug.Log($"[HudType] Applied Nunito and Fredoka to the HUD, rebuilt its prefabs and updated {updated} HUD component(s) in the open scene(s). Save the scene to keep them.");
        }

        /// <summary>For batch mode, where no scene is open: applies the type, then the kitchen scene's sizes, and saves it.</summary>
        public static void ApplyWithKitchen()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Kitchen_Greybox.unity");
            Apply();
            EditorSceneManager.SaveScene(scene);
        }

        private static void Raise(SerializedObject so, string field, float minimum)
        {
            var property = so.FindProperty(field);
            property.floatValue = Mathf.Max(property.floatValue, minimum);
        }

        // Sizes the kitchen scene stores on its HUD components, made before the 28 px minimum.
        private static int UpdateOpenScenes()
        {
            var count = 0;
            foreach (var money in Object.FindObjectsByType<MoneyHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                count += Update(money, so => Raise(so, "_fontSize", 32f));
            foreach (var status in Object.FindObjectsByType<ServiceStatusHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                count += Update(status, so =>
                {
                    Raise(so, "_stripFontSize", 28f);
                    Raise(so, "_clockFontSize", 32f);
                    Raise(so, "_cardFontSize", 32f);
                });
            // The doneness tag above the gauge grew with the bigger text: move the gauge down to clear it.
            foreach (var gauge in Object.FindObjectsByType<FlipGaugeHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                count += Update(gauge, so =>
                {
                    var offset = so.FindProperty("_offsetY");
                    offset.floatValue = Mathf.Min(offset.floatValue, -76f);
                });
            return count;
        }

        private static int Update(Component component, System.Action<SerializedObject> change)
        {
            var so = new SerializedObject(component);
            change(so);
            if (!so.ApplyModifiedProperties())
                return 0;
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            return 1;
        }

        // The fonts' own materials carry the outline, so every HUD text using them gets it. The
        // font assets are made with TextMeshPro's mobile SDF shader, which needs the keyword on.
        private static void Outline(TMP_FontAsset font, HudStyle style)
        {
            var material = font.material;
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, style.TextOutlineWidth);
            material.SetColor(ShaderUtilities.ID_OutlineColor, style.TextOutlineColor);
            if (style.TextOutlineWidth > 0f)
                material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            else
                material.DisableKeyword(ShaderUtilities.Keyword_Outline);
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(font);
        }
    }
}
