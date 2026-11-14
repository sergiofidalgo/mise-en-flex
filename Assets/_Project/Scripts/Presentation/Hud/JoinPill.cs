using Game.Data;
using Game.Gameplay.Stations;
using TMPro;
using UnityEngine;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The one join card (the user's pick, 2026-11-14), in place of a card per open seat: a narrow
    /// see-through pill after the last joined player's card, ringed with dashes in the colour of
    /// the seat it fills next, with "A Join" and the A gently breathing. Used by the office's player
    /// bar and the kitchen's Prep cards; it's gone once every seat is taken.
    /// </summary>
    public sealed class JoinPill
    {
        public const float Width = 152f;
        public const float Height = 58f;
        private const float BadgeSize = 34f;
        private const float Gap = 8f;
        private const float FillOpacity = 0.55f;
        private const float Tint = 0.18f;
        private const float BreathePeriod = 1.8f;
        private const float BreatheScale = 0.07f;

        private readonly HudStyle _style;
        private readonly RectTransform _root;
        private RectTransform _badge;
        private Color _drawn = Color.clear;

        public JoinPill(HudStyle style, Transform parent)
        {
            _style = style;
            _root = UiKit.Rect(parent, "JoinPill", 0f, 0f, Width, Height);
            _root.gameObject.SetActive(false);
        }

        public RectTransform Root => _root;

        /// <summary>Shows it with its top-left at (x, y) on its canvas (y down), in <paramref name="colour"/>.</summary>
        public void Show(float x, float y, Color colour, bool breathe = true)
        {
            _root.gameObject.SetActive(true);
            _root.anchoredPosition = new Vector2(x, -y);
            if (colour != _drawn)
                Draw(colour);
            // A slow breath on the A, so the card invites without shouting.
            var breath = breathe ? 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * Time.unscaledTime / BreathePeriod) : 0f;
            _badge.localScale = Vector3.one * (1f + BreatheScale * breath);
        }

        public void Hide() => _root.gameObject.SetActive(false);

        private void Draw(Color colour)
        {
            _drawn = colour;
            UiKit.Clear(_root);
            var fill = Color.Lerp(_style.CardBackground, colour, Tint);
            fill.a = FillOpacity;
            UiKit.RoundBox(_root, "Back", fill, 0f, 0f, Width, Height, Height / 2f);
            UiKit.DashedOutline(UiKit.Rect(_root, "Dashes", 0f, 0f, Width, Height), colour, Width, Height, Height / 2f, 12f, 8f, 4f);
            // The badge and the word centred together, whatever the word's length in the language.
            var join = UiKit.TextAt(_style.Bold, _root, "Join", Tr.Get("Join"), 26f, _style.TextColor, 0f, Height / 2f + 26f * 0.36f, Width - BadgeSize - Gap - 24f);
            var width = Mathf.Min(UiKit.Width(join), join.rectTransform.sizeDelta.x);
            var left = (Width - (BadgeSize + Gap + width)) / 2f;
            join.rectTransform.anchoredPosition = new Vector2(left + BadgeSize + Gap, join.rectTransform.anchoredPosition.y);
            var top = (Height - BadgeSize) / 2f;
            _badge = UiKit.Rect(_root, "Badge", left, top, BadgeSize, BadgeSize);
            _badge.pivot = new Vector2(0.5f, 0.5f);
            _badge.anchoredPosition = new Vector2(left + BadgeSize / 2f, -top - BadgeSize / 2f);
            UiKit.Box(_style, _badge, "Disc", _style.BadgeColor(PromptControl.South, true), 0f, 0f, BadgeSize, BadgeSize, circle: true);
            UiKit.TextAt(_style.DisplayFont, _badge, "Glyph", HudStyle.Glyph(PromptControl.South, true), BadgeSize * 0.58f, Color.white, BadgeSize / 2f, BadgeSize * 0.7f, BadgeSize, HorizontalAlignmentOptions.Center);
        }
    }
}
