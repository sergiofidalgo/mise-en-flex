using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// A small card of rich text, with an optional meter under it, that follows a point in the
    /// world (an order ticket with patience, a food tag). Built from the <see cref="HudStyle"/> in
    /// code: it's a background, a text and a bar, so a prefab would add nothing to tune. Fades in
    /// and out like the prompt cards.
    /// </summary>
    public sealed class WorldLabel : MonoBehaviour
    {
        private HudStyle _style;
        private RectTransform _rect;
        private CanvasGroup _group;
        private TextMeshProUGUI _text;
        private GameObject _bar;
        private RectTransform _barFill;
        private Image _barFillImage;
        private float _targetAlpha;

        /// <param name="pivot">Which point of the card sits on the world point, e.g. (0.5, 0) bottom-centre.</param>
        public static WorldLabel Create(HudStyle style, RectTransform canvas, string name, Vector2 padding, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var label = go.AddComponent<WorldLabel>();
            label._style = style;
            label._rect = (RectTransform)go.transform;
            label._rect.pivot = pivot;
            label._group = go.AddComponent<CanvasGroup>();
            label._group.alpha = 0f;
            label._group.blocksRaycasts = false;

            var background = go.AddComponent<Image>();
            background.sprite = style.Rounded;
            background.type = Image.Type.Sliced;
            background.color = style.CardBackground;
            background.raycastTarget = false;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)padding.x, (int)padding.x, (int)padding.y, (int)padding.y);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = style.MeterGap;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(go.transform, false);
            text.font = style.Font;
            text.fontSize = style.LabelFontSize;
            text.color = style.TextColor;
            text.alignment = TextAlignmentOptions.Center;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            label._text = text;

            // Meter: a dark track with a fill whose right edge follows the value.
            var bar = new GameObject("Meter", typeof(RectTransform));
            bar.transform.SetParent(go.transform, false);
            var track = bar.AddComponent<Image>();
            track.color = style.MeterTrack;
            track.raycastTarget = false;
            var element = bar.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = style.MeterHeight;
            element.minWidth = style.MeterMinWidth;
            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(bar.transform, false);
            label._barFillImage = fill.AddComponent<Image>();
            label._barFillImage.raycastTarget = false;
            label._barFill = (RectTransform)fill.transform;
            label._barFill.anchorMin = Vector2.zero;
            label._barFill.anchorMax = Vector2.one;
            label._barFill.offsetMin = label._barFill.offsetMax = Vector2.zero;
            label._bar = bar;
            bar.SetActive(false);
            return label;
        }

        public bool IsShown => _targetAlpha > 0f;

        /// <summary>Shows <paramref name="text"/> with its pivot at <paramref name="world"/>, if it's on screen.</summary>
        /// <param name="meter">0..1 for a meter under the text (coloured by <paramref name="meterColor"/>), or null for none.</param>
        public void Show(string text, Vector3 world, Camera camera, RectTransform canvas, float? meter = null, Color meterColor = default)
        {
            var screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                Hide();
                return;
            }

            if (_text.text != text)
                _text.text = text;
            var hasText = !string.IsNullOrEmpty(text);
            if (_text.gameObject.activeSelf != hasText)
                _text.gameObject.SetActive(hasText);
            if (_bar.activeSelf != meter.HasValue)
                _bar.SetActive(meter.HasValue);
            if (meter.HasValue)
            {
                _barFill.anchorMax = new Vector2(Mathf.Clamp01(meter.Value), 1f);
                _barFillImage.color = meterColor;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out var local);
            _rect.anchoredPosition = local;
            _targetAlpha = 1f;
        }

        public void Hide() => _targetAlpha = 0f;

        private void Update()
        {
            var speed = _style.Fade > 0f ? 1f / _style.Fade : float.MaxValue;
            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, speed * Time.unscaledDeltaTime);
        }
    }
}
