using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Gameplay.Stations;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// A small strip at the bottom centre of the screen: a word for the phase, then button hints with
    /// the buttons drawn as Xbox shows them (hints name only Xbox buttons, never keys). It's for the
    /// View and Menu buttons, which no font has, as forkcast's hint bar draws them. Rebuilt only when
    /// what it says changes; fades with the HUD's fade time.
    /// </summary>
    public sealed class HintStrip
    {
        /// <summary>The View button (two windows) and the Menu button (three bars); "A", "B", "X" and "Y" are face buttons.</summary>
        public const string View = "View", Menu = "Menu";

        private const float Height = 50f, Pad = 18f, KeyWidth = 44f, KeyHeight = 32f, KeyGap = 10f, HintGap = 28f;
        private const float LeadSize = 26f, LabelSize = 24f;

        private readonly HudStyle _style;
        private readonly RectTransform _rect;
        private readonly CanvasGroup _group;
        private readonly Color _key = new Color(0.12f, 0.10f, 0.09f, 1f);
        private string _shown;
        private float _target;

        public HintStrip(HudStyle style, RectTransform canvas, string name)
        {
            _style = style;
            _rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            _rect.SetParent(canvas, false);
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(0.5f, 0f);
            _group = _rect.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        /// <summary>The strip's height in canvas pixels, for what sits above it.</summary>
        public float StripHeight => Height;

        public void Show(string lead, IReadOnlyList<(string Key, string Text)> hints, float bottomMargin)
        {
            _target = 1f;
            _rect.anchoredPosition = new Vector2(0f, bottomMargin);
            var key = lead + "|" + string.Join("|", Keys(hints));
            if (key == _shown)
                return;
            _shown = key;
            Draw(lead, hints);
        }

        public void Hide() => _target = 0f;

        /// <summary>Fades towards shown or hidden; call once a frame.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            var fade = _style.Fade;
            _group.alpha = fade <= 0f ? _target : Mathf.MoveTowards(_group.alpha, _target, unscaledDeltaTime / fade);
        }

        private static IEnumerable<string> Keys(IReadOnlyList<(string Key, string Text)> hints)
        {
            foreach (var hint in hints)
                yield return hint.Key + ":" + hint.Text;
        }

        private void Draw(string lead, IReadOnlyList<(string Key, string Text)> hints)
        {
            UiKit.Clear(_rect);
            var text = _style.TextColor;
            var baseline = Height / 2f + LabelSize * 0.36f;

            // Laid out left to right from the padding, then the pill is sized to fit and centred.
            var x = Pad;
            var leadLabel = UiKit.TextAt(_style.Bold, _rect, "Lead", lead, LeadSize, text, x, Height / 2f + LeadSize * 0.36f, 400f);
            x += UiKit.Width(leadLabel) + HintGap;
            foreach (var (key, label) in hints)
            {
                DrawKey(key, x, (Height - KeyHeight) / 2f);
                x += KeyWidth + KeyGap;
                var hint = UiKit.TextAt(_style.Medium, _rect, "Hint", label, LabelSize, text, x, baseline, 600f);
                x += UiKit.Width(hint) + HintGap;
            }
            var width = x - HintGap + Pad;
            _rect.sizeDelta = new Vector2(width, Height);
            var back = UiKit.RoundBox(_rect, "Back", _style.CardBackground, 0f, 0f, width, Height, Height / 2f);
            back.transform.SetAsFirstSibling();
        }

        private void DrawKey(string key, float x, float y)
        {
            var paper = Color.white;
            var cx = x + KeyWidth / 2f;
            var cy = y + KeyHeight / 2f;
            // Face buttons as round badges in their Xbox colours, as everywhere else on the HUD.
            var face = key switch
            {
                "A" => (PromptControl?)PromptControl.South,
                "B" => PromptControl.East,
                "X" => PromptControl.West,
                "Y" => PromptControl.North,
                _ => null,
            };
            if (face != null)
            {
                UiKit.StrokedCircle(_rect, key, _style.BadgeColor(face.Value, true), paper, 3f, cx - KeyHeight / 2f, y, KeyHeight);
                UiKit.TextAt(_style.Bold, _rect, "Glyph", key, 17f, key == "Y" ? _key : paper, cx, cy + 6f, KeyWidth, HorizontalAlignmentOptions.Center);
                return;
            }
            UiKit.RoundBox(_rect, key, _key, x, y, KeyWidth, KeyHeight, 8f);
            if (key == View)
            {
                UiKit.StrokedBox(_rect, "Back", _key, paper, 2f, cx - 9f, cy - 8f, 13f, 11f, 2f);
                UiKit.StrokedBox(_rect, "Front", _key, paper, 2f, cx - 4f, cy - 3f, 13f, 11f, 2f);
                return;
            }
            if (key == Menu)
            {
                for (var i = 0; i < 3; i++)
                    UiKit.RoundBox(_rect, "Bar", paper, cx - 7f, cy - 5f + i * 4f, 14f, 2f, 1f);
                return;
            }
            UiKit.TextAt(_style.Bold, _rect, "Glyph", key, 16f, paper, cx, cy + 6f, KeyWidth, HorizontalAlignmentOptions.Center);
        }
    }
}
