using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// Small helpers for screens built in code (the main menu): rectangles laid out from their parent's
    /// top-left corner in canvas pixels, rounded boxes, outlines, text and drawn stars, in the house style.
    /// </summary>
    public static class UiKit
    {
        public static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public static RectTransform Box(HudStyle style, Transform parent, string name, Color color, float x, float y, float width, float height, bool rounded = true, bool circle = false)
        {
            var rect = Rect(parent, name, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (circle && style.Circle != null)
            {
                image.sprite = style.Circle;
            }
            else if (rounded && style.Rounded != null)
            {
                image.sprite = style.Rounded;
                image.type = Image.Type.Sliced;
            }
            return rect;
        }

        /// <summary>A frame of <paramref name="thickness"/> just outside <paramref name="target"/>: four thin bars.</summary>
        public static void Outline(HudStyle style, RectTransform target, Color color, float thickness, bool circle = false)
        {
            var x = target.anchoredPosition.x;
            var y = -target.anchoredPosition.y;
            var size = target.sizeDelta;
            if (circle)
            {
                var ring = Box(style, target.parent, target.name + "Ring", color, x - thickness, y - thickness, size.x + 2f * thickness, size.y + 2f * thickness, circle: true);
                ring.SetSiblingIndex(target.GetSiblingIndex());
                return;
            }
            Box(style, target.parent, "Edge", color, x - thickness, y - thickness, size.x + 2f * thickness, thickness, rounded: false);
            Box(style, target.parent, "Edge", color, x - thickness, y + size.y, size.x + 2f * thickness, thickness, rounded: false);
            Box(style, target.parent, "Edge", color, x - thickness, y, thickness, size.y, rounded: false);
            Box(style, target.parent, "Edge", color, x + size.x, y, thickness, size.y, rounded: false);
        }

        public static TextMeshProUGUI Text(HudStyle style, Transform parent, string name, string text, float size, Color color, FontStyles fontStyle, float x, float y, float width, float height)
        {
            var label = Rect(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = style.Font;
            label.fontSize = size;
            label.color = color;
            label.fontStyle = fontStyle;
            label.text = text;
            label.richText = true;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.TopLeft;
            return label;
        }

        /// <summary>Five drawn stars, <paramref name="stars"/> of them filled (the HUD font has no star glyph).</summary>
        public static void Stars(Transform parent, float x, float y, float size, float stars, Color filled, Color empty)
        {
            var count = Mathf.Clamp(Mathf.RoundToInt(stars), 0, 5);
            for (var i = 0; i < 5; i++)
            {
                var star = Rect(parent, "Star", x + i * size * 1.1f, y, size, size).gameObject.AddComponent<Image>();
                star.sprite = ReviewDashboard.StarSprite;
                star.color = i < count ? filled : new Color(empty.r, empty.g, empty.b, 0.25f);
                star.raycastTarget = false;
            }
        }

        public static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public static void Clear(RectTransform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
                Object.Destroy(parent.GetChild(i).gameObject);
        }
    }
}
