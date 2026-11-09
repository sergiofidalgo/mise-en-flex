using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// Small helpers for screens built in code (the main menu): rectangles laid out from their parent's
    /// top-left corner in canvas pixels, rounded boxes with exact corner radii, text placed by its
    /// baseline, and drawn stars, in the house style.
    /// </summary>
    public static class UiKit
    {
        // Shapes are drawn at twice the canvas resolution, so they stay crisp on a Retina screen.
        private const float ShapeScale = 2f;

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

        /// <summary>A box with corners of exactly <paramref name="radius"/> canvas pixels (0 for square).</summary>
        public static Image RoundBox(Transform parent, string name, Color color, float x, float y, float width, float height, float radius)
        {
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (radius > 0f)
            {
                image.sprite = RoundedSprite(Mathf.Min(radius, Mathf.Min(width, height) / 2f));
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        /// <summary>
        /// A filled box with a border of <paramref name="stroke"/> pixels centred on its edge, the way a
        /// stroked shape is drawn in the mockups: two stacked rounded boxes, the border one larger.
        /// </summary>
        public static Image StrokedBox(Transform parent, string name, Color fill, Color edge, float stroke, float x, float y, float width, float height, float radius)
        {
            var half = stroke / 2f;
            RoundBox(parent, name + "Edge", edge, x - half, y - half, width + stroke, height + stroke, radius + half);
            return RoundBox(parent, name, fill, x + half, y + half, width - stroke, height - stroke, Mathf.Max(0f, radius - half));
        }

        /// <summary>A circle or, when width and height differ, an ellipse.</summary>
        public static Image Ellipse(Transform parent, string name, Color color, float x, float y, float width, float height)
        {
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.sprite = CircleSprite(Mathf.Max(width, height));
            return image;
        }

        /// <summary>A circle of <paramref name="diameter"/> with a ring of <paramref name="stroke"/> centred on its edge.</summary>
        public static void StrokedCircle(Transform parent, string name, Color fill, Color edge, float stroke, float x, float y, float diameter)
        {
            StrokedEllipse(parent, name, fill, edge, stroke, x, y, diameter, diameter);
        }

        public static void StrokedEllipse(Transform parent, string name, Color fill, Color edge, float stroke, float x, float y, float width, float height)
        {
            var half = stroke / 2f;
            Ellipse(parent, name + "Edge", edge, x - half, y - half, width + stroke, height + stroke);
            Ellipse(parent, name, fill, x + half, y + half, width - stroke, height - stroke);
        }

        public static Image Picture(Transform parent, string name, Sprite sprite, float x, float y, float width, float height, Color? tint = null)
        {
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = tint ?? Color.white;
            image.raycastTarget = false;
            image.enabled = sprite != null;
            return image;
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

        /// <summary>
        /// Text whose first baseline sits at <paramref name="baseline"/>, the way type is placed in a
        /// layout tool. TextMeshPro's baseline alignment puts the baseline on the rectangle's middle,
        /// so the rectangle is centred on it. <paramref name="x"/> is the left edge, the centre or the
        /// right edge, following <paramref name="align"/>. One line, cut with an ellipsis at
        /// <paramref name="width"/>, unless <paramref name="lines"/> allows more.
        /// </summary>
        public static TextMeshProUGUI TextAt(TMP_FontAsset font, Transform parent, string name, string text, float size, Color color, float x, float baseline, float width, HorizontalAlignmentOptions align = HorizontalAlignmentOptions.Left, int lines = 1)
        {
            var left = align == HorizontalAlignmentOptions.Center ? x - width / 2f : align == HorizontalAlignmentOptions.Right ? x - width : x;
            var label = Rect(parent, name, left, baseline - size, width, size * 2f).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                label.font = font;
            label.fontSize = size;
            label.color = color;
            label.fontStyle = FontStyles.Normal;
            label.richText = true;
            label.raycastTarget = false;
            label.horizontalAlignment = align;
            label.verticalAlignment = VerticalAlignmentOptions.Baseline;
            label.textWrappingMode = lines > 1 ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = lines > 1 ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            label.maxVisibleLines = lines;
            label.text = text;
            if (font != null && color.grayscale < 0.5f)
                label.fontSharedMaterial = DarkTextMaterial(font);
            return label;
        }

        // Dark SDF text on a light background renders thinner than designed in a linear-colour
        // project (edges are blended in linear light), measured at 20–40% less ink than the mockups,
        // while light-on-dark matched. A small face dilate restores the designed weight.
        private const float DarkTextDilate = 0.12f;
        private static readonly Dictionary<TMP_FontAsset, Material> DarkMaterials = new Dictionary<TMP_FontAsset, Material>();

        private static Material DarkTextMaterial(TMP_FontAsset font)
        {
            if (DarkMaterials.TryGetValue(font, out var material) && material != null)
                return material;
            material = new Material(font.material) { name = font.name + " Dark", hideFlags = HideFlags.DontSave };
            material.SetFloat(ShaderUtilities.ID_FaceDilate, DarkTextDilate);
            DarkMaterials[font] = material;
            return material;
        }

        /// <summary>How wide <paramref name="label"/>'s text is drawn, in canvas pixels.</summary>
        public static float Width(TextMeshProUGUI label) => label.GetPreferredValues(label.text, 10000f, 10000f).x;

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

        /// <summary>Five stars of <paramref name="size"/>, <paramref name="pitch"/> apart, the unearned ones in <paramref name="empty"/>. Returns the right edge.</summary>
        public static float StarRow(Transform parent, float x, float y, float size, float pitch, float stars, Color filled, Color empty)
        {
            var count = Mathf.Clamp(Mathf.RoundToInt(stars), 0, 5);
            for (var i = 0; i < 5; i++)
                Picture(parent, "Star", ReviewDashboard.StarSprite, x + i * pitch, y, size, size, i < count ? filled : empty);
            return x + 4f * pitch + size;
        }

        // The mockups' avatar colours: soft, distinct, and the same for the same guest every time.
        private static readonly Color[] AvatarColours =
        {
            new Color32(0x9f, 0xb5, 0x9a, 0xff), new Color32(0xb4, 0x9a, 0xcb, 0xff), new Color32(0xe3, 0xb9, 0xa0, 0xff),
            new Color32(0xc4, 0xa0, 0x8f, 0xff), new Color32(0x8f, 0xa3, 0xc4, 0xff), new Color32(0xd9, 0xc2, 0x7e, 0xff),
            new Color32(0x9c, 0xc3, 0xc0, 0xff), new Color32(0xd6, 0x9f, 0xa8, 0xff),
        };

        /// <summary>A guest's avatar colour, from their name (string hashes vary between runs, so it's computed here).</summary>
        public static Color AvatarColour(string guest)
        {
            var hash = 0;
            foreach (var c in guest ?? "")
                hash = unchecked(hash * 31 + c);
            return AvatarColours[(hash & 0x7fffffff) % AvatarColours.Length];
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
            {
                // Edit mode too: the capture tool draws the menu without entering Play mode.
                if (Application.isPlaying)
                    Object.Destroy(parent.GetChild(i).gameObject);
                else
                    Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        // ---- Shapes, made once per size ----

        private static readonly Dictionary<int, Sprite> RoundedSprites = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> CircleSprites = new Dictionary<int, Sprite>();

        /// <summary>
        /// A nine-sliced rounded square whose corners measure exactly <paramref name="radius"/> canvas
        /// pixels, so corners match the mockup instead of a scaled generic sprite. Cached per quarter pixel.
        /// </summary>
        public static Sprite RoundedSprite(float radius)
        {
            var key = Mathf.Max(1, Mathf.RoundToInt(radius * 4f));
            if (RoundedSprites.TryGetValue(key, out var cached) && cached != null)
                return cached;
            var r = key / 4f * ShapeScale;
            var border = Mathf.CeilToInt(r) + 1;
            var size = border * 2 + 2;
            var texture = Shape(size, (px, py) => RoundedBoxDistance(px, py, size, r));
            // 100 × ShapeScale pixels per unit: two texture pixels to one canvas pixel.
            var sprite = Sprite.Create(texture, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f * ShapeScale, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = $"Rounded{key / 4f}";
            RoundedSprites[key] = sprite;
            return sprite;
        }

        /// <summary>A circle drawn at about twice the size it's shown, so its edge stays smooth.</summary>
        public static Sprite CircleSprite(float diameter)
        {
            var size = Mathf.Clamp(Mathf.CeilToInt(diameter * ShapeScale), 8, 256);
            if (CircleSprites.TryGetValue(size, out var cached) && cached != null)
                return cached;
            var radius = size / 2f;
            var texture = Shape(size, (px, py) => new Vector2(px - radius, py - radius).magnitude - radius);
            var sprite = Sprite.Create(texture, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f * ShapeScale);
            sprite.name = $"Circle{size}";
            CircleSprites[size] = sprite;
            return sprite;
        }

        // Signed distance from a pixel centre to the edge of a size×size square with corners of
        // radius r, negative inside. Coverage is then 0.5 − distance, clamped: a one-pixel smooth edge.
        private static float RoundedBoxDistance(float px, float py, int size, float r)
        {
            var half = size / 2f;
            var qx = Mathf.Abs(px - half) - (half - r);
            var qy = Mathf.Abs(py - half) - (half - r);
            var outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static Texture2D Shape(int size, System.Func<float, float, float> distance)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var coverage = Mathf.Clamp01(0.5f - distance(x + 0.5f, y + 0.5f));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
