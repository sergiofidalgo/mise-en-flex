using Game.Data;
using Game.Gameplay.Stations;
using Game.Presentation.Hud;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Builds the HUD: the style asset, the prompt card prefab (badge rows and the face-button
    /// diamond) and the screen canvas in the scene. uGUI + TextMeshPro (decision 2026-11-02).
    /// </summary>
    public static class GreyboxHud
    {
        private const string StylePath = "Assets/_Project/Data/Settings/HudStyle.asset";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Hud";
        private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        // Canvas scales from a 1080p reference so sizes in HudStyle mean the same on any screen.
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private const int HudSortingOrder = 5;

        private const int CardPadding = 10;
        private const float RowSpacing = 6f;
        private const float BadgeToLabel = 8f;
        private const float DiamondColumnGap = 28f;

        public static HudStyle EnsureStyle()
        {
            var style = GreyboxAssets.EnsureAsset<HudStyle>(StylePath);
            var so = new SerializedObject(style);
            SetIfEmpty(so, "_font", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath));
            SetIfEmpty(so, "_circle", AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"));
            SetIfEmpty(so, "_rounded", AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"));
            so.ApplyModifiedPropertiesWithoutUndo();
            return style;
        }

        /// <summary>A screen-space canvas with the prompt HUD on it.</summary>
        public static void BuildCanvas(HudStyle style, CoopSettings settings)
        {
            var card = BuildPromptCard(style);

            var hud = new GameObject("HUD", typeof(RectTransform));
            var canvas = hud.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = HudSortingOrder;
            var scaler = hud.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            var prompts = hud.AddComponent<PromptHud>();
            var so = new SerializedObject(prompts);
            so.FindProperty("_canvas").objectReferenceValue = hud.GetComponent<RectTransform>();
            so.FindProperty("_cardPrefab").objectReferenceValue = card.GetComponent<PromptCard>();
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject BuildPromptCard(HudStyle style)
        {
            // Root: player-coloured edge, sized to its content; pivot bottom-left (PromptHud centres it).
            var root = NewRect("PF_Hud_PromptCard", null);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = Vector2.zero;
            var edge = AddImage(root.gameObject, style.Rounded, Color.white);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            var pad = CardPadding + Mathf.RoundToInt(style.CardEdge);
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Dark fill inset by the edge width, behind the content.
            var fill = NewRect("Fill", root);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.one * style.CardEdge;
            fill.offsetMax = -Vector2.one * style.CardEdge;
            AddImage(fill.gameObject, style.Rounded, style.CardBackground);
            fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var list = NewRect("List", root);
            var listLayout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = RowSpacing;
            listLayout.childControlWidth = listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = listLayout.childForceExpandHeight = false;
            var rows = new PromptRow[4];
            for (var i = 0; i < rows.Length; i++)
                rows[i] = BuildRow($"Row_{i + 1}", list, style);

            // Diamond: Y on top, X and B in the middle, A at the bottom, as on the controller.
            var diamond = NewRect("Diamond", root);
            var diamondLayout = diamond.gameObject.AddComponent<VerticalLayoutGroup>();
            diamondLayout.spacing = RowSpacing;
            diamondLayout.childAlignment = TextAnchor.MiddleCenter;
            diamondLayout.childControlWidth = diamondLayout.childControlHeight = true;
            diamondLayout.childForceExpandWidth = diamondLayout.childForceExpandHeight = false;
            var top = DiamondLine("Top", diamond);
            var middle = DiamondLine("Middle", diamond);
            var bottom = DiamondLine("Bottom", diamond);
            middle.GetComponent<HorizontalLayoutGroup>().spacing = DiamondColumnGap;
            var cells = new PromptRow[4];
            cells[(int)PromptControl.North] = BuildRow("Cell_North", top, style);
            cells[(int)PromptControl.West] = BuildRow("Cell_West", middle, style);
            cells[(int)PromptControl.East] = BuildRow("Cell_East", middle, style);
            cells[(int)PromptControl.South] = BuildRow("Cell_South", bottom, style);

            var card = root.gameObject.AddComponent<PromptCard>();
            var so = new SerializedObject(card);
            so.FindProperty("_style").objectReferenceValue = style;
            so.FindProperty("_rect").objectReferenceValue = root;
            so.FindProperty("_group").objectReferenceValue = group;
            so.FindProperty("_edge").objectReferenceValue = edge;
            so.FindProperty("_list").objectReferenceValue = list.gameObject;
            so.FindProperty("_diamond").objectReferenceValue = diamond.gameObject;
            SetArray(so.FindProperty("_rows"), rows);
            SetArray(so.FindProperty("_cells"), cells);
            so.ApplyModifiedPropertiesWithoutUndo();

            GreyboxAssets.EnsureFolder(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, $"{PrefabFolder}/PF_Hud_PromptCard.prefab");
            Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        private static RectTransform DiamondLine(string name, RectTransform parent)
        {
            var line = NewRect(name, parent);
            var layout = line.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return line;
        }

        private static PromptRow BuildRow(string name, RectTransform parent, HudStyle style)
        {
            var row = NewRect(name, parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = BadgeToLabel;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var group = row.gameObject.AddComponent<CanvasGroup>();

            var badge = BuildBadge(row, style);

            var labelRect = NewRect("Label", row);
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = style.Font;
            label.fontSize = style.FontSize;
            label.color = style.TextColor;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.text = "action";

            var prompt = row.gameObject.AddComponent<PromptRow>();
            var so = new SerializedObject(prompt);
            so.FindProperty("_style").objectReferenceValue = style;
            so.FindProperty("_badge").objectReferenceValue = badge;
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_group").objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();
            return prompt;
        }

        private static ButtonBadge BuildBadge(RectTransform parent, HudStyle style)
        {
            var rect = NewRect("Badge", parent);
            var image = AddImage(rect.gameObject, style.Circle, Color.white);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.preferredHeight = style.BadgeSize;

            var glyphRect = NewRect("Glyph", rect);
            glyphRect.anchorMin = Vector2.zero;
            glyphRect.anchorMax = Vector2.one;
            glyphRect.offsetMin = glyphRect.offsetMax = Vector2.zero;
            var glyph = glyphRect.gameObject.AddComponent<TextMeshProUGUI>();
            glyph.font = style.Font;
            glyph.fontStyle = FontStyles.Bold;
            glyph.alignment = TextAlignmentOptions.Center;
            glyph.textWrappingMode = TextWrappingModes.NoWrap;
            glyph.raycastTarget = false;
            glyph.text = "A";

            var badge = rect.gameObject.AddComponent<ButtonBadge>();
            var so = new SerializedObject(badge);
            so.FindProperty("_style").objectReferenceValue = style;
            so.FindProperty("_rect").objectReferenceValue = rect;
            so.FindProperty("_layout").objectReferenceValue = element;
            so.FindProperty("_background").objectReferenceValue = image;
            so.FindProperty("_glyph").objectReferenceValue = glyph;
            so.ApplyModifiedPropertiesWithoutUndo();
            return badge;
        }

        private static RectTransform NewRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            if (parent != null)
                rect.SetParent(parent, false);
            return rect;
        }

        private static Image AddImage(GameObject go, Sprite sprite, Color color)
        {
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void SetArray<T>(SerializedProperty property, T[] values) where T : Object
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void SetIfEmpty(SerializedObject so, string field, Object value)
        {
            var property = so.FindProperty(field);
            if (property.objectReferenceValue == null)
                property.objectReferenceValue = value;
        }
    }
}
