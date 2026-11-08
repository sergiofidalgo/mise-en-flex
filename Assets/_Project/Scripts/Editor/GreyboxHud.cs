using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Customers;
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
        // An ingredient's picture is a little bigger than a button badge, so it reads at a glance.
        private const float IconToBadge = 1.5f;
        private const float DiamondColumnGap = 28f;
        private const float RadialRingPadding = 18f;

        // Cutting dial (canvas pixels at the 1080p reference).
        private const float DialSize = 130f;
        private const float DialHole = 0.62f;     // inner fan as a fraction of the dial: arcs read as a band on the rim
        private const float NeedleWidth = 5f;
        private const float MarkerWidth = 7f;
        private const float MarkerOverhang = 6f;   // the hit marker pokes out past the rim
        private const float QualityBarWidth = 120f;
        private const float QualityBarHeight = 10f;
        private const int DialDots = 8;
        private const float DotSize = 12f;
        private static readonly Color DialRing = new Color(0.10f, 0.11f, 0.13f, 0.88f);
        private static readonly Color DialOk = new Color(0.95f, 0.72f, 0.25f);
        private static readonly Color DialGood = new Color(0.45f, 0.85f, 0.40f);

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

        /// <summary>A screen-space canvas with the prompt HUD, order tickets and food tags on it.</summary>
        public static void BuildCanvas(HudStyle style, CoopSettings settings, KitchenCompositionRoot root, ItemCatalog catalog)
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

            var dials = hud.AddComponent<ChopDialHud>();
            var dialSo = new SerializedObject(dials);
            dialSo.FindProperty("_canvas").objectReferenceValue = hud.GetComponent<RectTransform>();
            dialSo.FindProperty("_dialPrefab").objectReferenceValue = BuildChopDial(style).GetComponent<ChopDialView>();
            dialSo.ApplyModifiedPropertiesWithoutUndo();

            var tickets = new SerializedObject(hud.AddComponent<OrderTicketHud>());
            tickets.FindProperty("_canvas").objectReferenceValue = hud.GetComponent<RectTransform>();
            tickets.FindProperty("_style").objectReferenceValue = style;
            tickets.FindProperty("_director").objectReferenceValue = Object.FindFirstObjectByType<CustomerDirector>();
            tickets.FindProperty("_catalog").objectReferenceValue = catalog;
            tickets.ApplyModifiedPropertiesWithoutUndo();

            var tags = new SerializedObject(hud.AddComponent<DonenessTagHud>());
            tags.FindProperty("_canvas").objectReferenceValue = hud.GetComponent<RectTransform>();
            tags.FindProperty("_style").objectReferenceValue = style;
            tags.FindProperty("_root").objectReferenceValue = root;
            tags.ApplyModifiedPropertiesWithoutUndo();

            var layoutHud = new SerializedObject(hud.AddComponent<LayoutEditorHud>());
            layoutHud.FindProperty("_canvas").objectReferenceValue = hud.GetComponent<RectTransform>();
            layoutHud.FindProperty("_style").objectReferenceValue = style;
            layoutHud.FindProperty("_editor").objectReferenceValue = Object.FindFirstObjectByType<LayoutEditor>();
            layoutHud.FindProperty("_settings").objectReferenceValue = settings;
            layoutHud.FindProperty("_root").objectReferenceValue = root;
            layoutHud.FindProperty("_framePrefab").objectReferenceValue = GreyboxStaff.BuildCursorFrame();
            layoutHud.ApplyModifiedPropertiesWithoutUndo();

            var markers = new SerializedObject(hud.AddComponent<CustomerIndicatorHud>());
            markers.FindProperty("_canvas").objectReferenceValue = hud.GetComponent<RectTransform>();
            markers.FindProperty("_style").objectReferenceValue = style;
            markers.FindProperty("_root").objectReferenceValue = root;
            markers.FindProperty("_customers").objectReferenceValue = Object.FindFirstObjectByType<Game.Gameplay.Customers.CustomerDirector>();
            markers.ApplyModifiedPropertiesWithoutUndo();

            var bell = new SerializedObject(hud.AddComponent<BellHud>());
            bell.FindProperty("_canvas").objectReferenceValue = hud.GetComponent<RectTransform>();
            bell.FindProperty("_style").objectReferenceValue = style;
            bell.FindProperty("_root").objectReferenceValue = root;
            bell.ApplyModifiedPropertiesWithoutUndo();

            var flips = new SerializedObject(hud.AddComponent<FlipGaugeHud>());
            flips.FindProperty("_canvas").objectReferenceValue = hud.GetComponent<RectTransform>();
            flips.FindProperty("_root").objectReferenceValue = root;
            flips.ApplyModifiedPropertiesWithoutUndo();

            var wash = new SerializedObject(hud.AddComponent<WashGameHud>());
            wash.FindProperty("_canvas").objectReferenceValue = hud.GetComponent<RectTransform>();
            wash.FindProperty("_style").objectReferenceValue = style;
            wash.FindProperty("_sink").objectReferenceValue = Object.FindFirstObjectByType<SinkStation>();
            wash.ApplyModifiedPropertiesWithoutUndo();

            var status = new SerializedObject(hud.AddComponent<ServiceStatusHud>());
            status.FindProperty("_canvas").objectReferenceValue = hud.GetComponent<RectTransform>();
            status.FindProperty("_style").objectReferenceValue = style;
            status.FindProperty("_root").objectReferenceValue = root;
            status.FindProperty("_dayLoop").objectReferenceValue = root.GetComponent<DayLoop>();
            status.FindProperty("_settings").objectReferenceValue = settings;
            status.ApplyModifiedPropertiesWithoutUndo();

            var dashboard = new SerializedObject(hud.AddComponent<ReviewDashboard>());
            dashboard.FindProperty("_style").objectReferenceValue = style;
            dashboard.FindProperty("_root").objectReferenceValue = root;
            dashboard.FindProperty("_dayLoop").objectReferenceValue = root.GetComponent<DayLoop>();
            dashboard.FindProperty("_settings").objectReferenceValue = settings;
            dashboard.ApplyModifiedPropertiesWithoutUndo();

            var campaign = new SerializedObject(hud.AddComponent<CampaignCardHud>());
            campaign.FindProperty("_canvas").objectReferenceValue = hud.GetComponent<RectTransform>();
            campaign.FindProperty("_style").objectReferenceValue = style;
            campaign.FindProperty("_root").objectReferenceValue = root;
            campaign.ApplyModifiedPropertiesWithoutUndo();

            var money = new SerializedObject(hud.AddComponent<MoneyHud>());
            money.FindProperty("_canvas").objectReferenceValue = hud.GetComponent<RectTransform>();
            money.FindProperty("_style").objectReferenceValue = style;
            money.FindProperty("_root").objectReferenceValue = root;
            money.ApplyModifiedPropertiesWithoutUndo();

            var reviews = new SerializedObject(hud.AddComponent<ReviewPopupHud>());
            reviews.FindProperty("_canvas").objectReferenceValue = hud.GetComponent<RectTransform>();
            reviews.FindProperty("_style").objectReferenceValue = style;
            reviews.FindProperty("_root").objectReferenceValue = root;
            reviews.FindProperty("_director").objectReferenceValue = Object.FindFirstObjectByType<CustomerDirector>();
            reviews.ApplyModifiedPropertiesWithoutUndo();
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
            // A faint ring behind the diamond, so the open slot menu reads as radial.
            var ring = NewRect("Ring", diamond);
            ring.anchorMin = Vector2.zero;
            ring.anchorMax = Vector2.one;
            ring.offsetMin = -Vector2.one * RadialRingPadding;
            ring.offsetMax = Vector2.one * RadialRingPadding;
            var ringImage = AddImage(ring.gameObject, style.Circle, new Color(1f, 1f, 1f, 0.07f));
            ringImage.type = Image.Type.Simple;
            ring.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

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

        private static GameObject BuildChopDial(HudStyle style)
        {
            var root = NewRect("PF_Hud_ChopDial", null);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.one * DialSize;
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            // Everything lives under Content, which pops on a Good and shakes on a Miss.
            var content = NewRect("Content", root);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = Vector2.one * DialSize;

            // A half-arc fan: dark backing, the coloured sweet-spot arcs, then a smaller dark fan over
            // them so the colours read as bands along the rim, then the grade flash on top.
            // ChopDialView sizes all of them.
            var backing = Arc("Backing", content, style, DialRing, 1f);
            var ok = Arc("OkArc", content, style, DialOk, 1f);
            var good = Arc("GoodArc", content, style, DialGood, 1f);
            var inner = Arc("Inner", content, style, DialRing, DialHole);
            var flash = Arc("Flash", content, style, Color.clear, 1f);

            // Hit marker: an arm rotating about the centre with a tick at the rim.
            var arm = NewRect("MarkerArm", content);
            arm.anchorMin = arm.anchorMax = new Vector2(0.5f, 0.5f);
            arm.pivot = new Vector2(0.5f, 0f);
            arm.sizeDelta = new Vector2(MarkerWidth, DialSize / 2f);
            arm.anchoredPosition = Vector2.zero;
            var tick = NewRect("Marker", arm);
            tick.anchorMin = tick.anchorMax = new Vector2(0.5f, 1f);
            tick.pivot = new Vector2(0.5f, 1f);
            tick.sizeDelta = new Vector2(MarkerWidth, DialSize / 2f * (1f - DialHole) + MarkerOverhang);
            tick.anchoredPosition = new Vector2(0f, MarkerOverhang);
            var marker = AddImage(tick.gameObject, style.Rounded, Color.white);
            marker.type = Image.Type.Simple;

            // Needle from the centre to the rim; it rotates about the dial's centre.
            var needle = NewRect("Needle", content);
            needle.anchorMin = needle.anchorMax = new Vector2(0.5f, 0.5f);
            needle.pivot = new Vector2(0.5f, 0f);
            needle.sizeDelta = new Vector2(NeedleWidth, DialSize / 2f);
            needle.anchoredPosition = Vector2.zero;
            AddImage(needle.gameObject, style.Rounded, Color.white).type = Image.Type.Simple;

            // Under the fan's flat edge (the dial's centre line), where the lower half is empty:
            // dots per cut, the cut-quality bar, the grade and the speed hint.
            var dotsRow = NewRect("Dots", content);
            dotsRow.anchorMin = dotsRow.anchorMax = new Vector2(0.5f, 0.5f);
            dotsRow.pivot = new Vector2(0.5f, 1f);
            dotsRow.anchoredPosition = new Vector2(0f, -8f);
            var dotsLayout = dotsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            dotsLayout.spacing = 5f;
            dotsLayout.childControlWidth = dotsLayout.childControlHeight = false;
            dotsLayout.childAlignment = TextAnchor.MiddleCenter;
            var fitter = dotsRow.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var dots = new Image[DialDots];
            for (var i = 0; i < DialDots; i++)
            {
                var dot = NewRect($"Dot_{i + 1}", dotsRow);
                dot.sizeDelta = Vector2.one * DotSize;
                dots[i] = AddImage(dot.gameObject, style.Circle, Color.white);
                dots[i].type = Image.Type.Simple;
            }

            var bar = NewRect("QualityBar", content);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -8f - DotSize - 6f);
            bar.sizeDelta = new Vector2(QualityBarWidth, QualityBarHeight);
            AddImage(bar.gameObject, style.Rounded, DialRing).type = Image.Type.Simple;
            var fillRect = NewRect("Fill", bar);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.one * 2f;
            fillRect.offsetMax = -Vector2.one * 2f;
            var fill = AddImage(fillRect.gameObject, style.Rounded, Color.white);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;

            var grade = Label("Grade", content, style, -8f - DotSize - 6f - QualityBarHeight - 4f, 260f, 38f, FontStyles.Bold);
            var hint = Label("SpeedHint", content, style, -8f - DotSize - 6f - QualityBarHeight - 4f - 36f, 200f, 22f, FontStyles.Italic);
            hint.fontSize = style.FontSize * 0.7f;
            hint.color = new Color(0.85f, 0.87f, 0.9f);

            var view = root.gameObject.AddComponent<ChopDialView>();
            var so = new SerializedObject(view);
            so.FindProperty("_rect").objectReferenceValue = root;
            so.FindProperty("_content").objectReferenceValue = content;
            so.FindProperty("_group").objectReferenceValue = group;
            so.FindProperty("_backing").objectReferenceValue = backing;
            so.FindProperty("_inner").objectReferenceValue = inner;
            so.FindProperty("_okArc").objectReferenceValue = ok;
            so.FindProperty("_goodArc").objectReferenceValue = good;
            so.FindProperty("_flash").objectReferenceValue = flash;
            so.FindProperty("_needle").objectReferenceValue = needle;
            so.FindProperty("_markerArm").objectReferenceValue = arm;
            so.FindProperty("_marker").objectReferenceValue = marker;
            so.FindProperty("_qualityFill").objectReferenceValue = fill;
            so.FindProperty("_grade").objectReferenceValue = grade;
            so.FindProperty("_speedHint").objectReferenceValue = hint;
            SetArray(so.FindProperty("_dots"), dots);
            so.ApplyModifiedPropertiesWithoutUndo();

            GreyboxAssets.EnsureFolder(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, $"{PrefabFolder}/PF_Hud_ChopDial.prefab");
            Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        private static TextMeshProUGUI Label(string name, RectTransform parent, HudStyle style, float y, float width, float height, FontStyles fontStyle)
        {
            var rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(width, height);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = style.Font;
            text.fontSize = style.FontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static Image Disc(string name, RectTransform parent, HudStyle style, Color color, float scale)
        {
            var rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * DialSize * scale;
            var image = AddImage(rect.gameObject, style.Circle, color);
            image.type = Image.Type.Simple;
            return image;
        }

        // A disc filled radially from the top, clockwise; ChopDialView sets the amount and rotation.
        private static Image Arc(string name, RectTransform parent, HudStyle style, Color color, float scale)
        {
            var image = Disc(name, parent, style, color, scale);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = 0.1f;
            return image;
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

            // An ingredient's picture, between the button and its count; hidden for plain verbs.
            var iconRect = NewRect("Icon", row);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var iconSize = iconRect.gameObject.AddComponent<LayoutElement>();
            iconSize.preferredWidth = iconSize.preferredHeight = style.BadgeSize * IconToBadge;
            iconRect.gameObject.SetActive(false);

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
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_icons").objectReferenceValue = IngredientIconRenderer.Catalog();
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
