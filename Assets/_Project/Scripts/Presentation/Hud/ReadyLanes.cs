using System;
using System.Collections.Generic;
using Game.Data;
using TMPro;
using UnityEngine;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The ready bar (the user's design, 2026-11-11): one segment per joined cook, in player-slot
    /// order, pale in their colour while they wait and filling solid as they hold their button. A
    /// cook becoming ready makes their label pop. Shared by the forkcast dashboard's footer and the
    /// kitchen's Prep bar, so both read the same; each says its own words in the segments.
    /// Plain C# drawing into a rect the owner gives it; the owner reads input and says who's ready.
    /// </summary>
    public sealed class ReadyLanes
    {
        /// <summary>How the bar looks and moves: set by its owner.</summary>
        public sealed class Look
        {
            public TMP_FontAsset ReadyFont;
            public TMP_FontAsset WaitingFont;
            public TMP_FontAsset BadgeFont;
            public float FontSize = 21f;
            public Color Light = new Color(1f, 0.97f, 0.93f);
            public Color Ink = new Color(0.17f, 0.13f, 0.11f);
            /// <summary>The bar's colour with nobody joined yet.</summary>
            public Color Nobody = new Color(0.89f, 0.69f, 0.29f);
            public float Gap = 4f;
            /// <summary>How solid a segment's waiting part is; the part filled by a hold is always solid.</summary>
            public float IdleOpacity = 1f;
            public float FillSpeed = 12f;
            public float PopSeconds = 0.45f;
            public float Pop = 0.18f;
        }

        /// <summary>A button glyph in a segment: its letter and colour.</summary>
        public readonly struct Glyph
        {
            public readonly string Letter;
            public readonly Color Colour;

            public Glyph(string letter, Color colour)
            {
                Letter = letter;
                Colour = colour;
            }
        }

        private sealed class Lane
        {
            public int Player;
            public RectTransform Fill;
            public float Width;
            public float Shown;
            public Color Colour;
            public RectTransform Label;
            public float Pop;
            public bool WasReady;
        }

        private readonly RectTransform _parent;
        private readonly float _width;
        private readonly float _height;
        private readonly Look _look;
        private readonly List<Lane> _lanes = new List<Lane>();
        private string _builtFor;

        public ReadyLanes(RectTransform parent, float width, float height, Look look)
        {
            _parent = parent;
            _width = width;
            _height = height;
            _look = look;
        }

        /// <summary>How many segments there are.</summary>
        public int Count => _lanes.Count;

        /// <summary>
        /// One segment per player slot given (none: one segment for nobody, -1). Rebuilt only when
        /// who's here changes; returns true when it was.
        /// </summary>
        public bool Build(IReadOnlyList<int> players, Func<int, Color> colourOf)
        {
            var key = string.Join(",", players);
            if (key == _builtFor)
                return false;
            _builtFor = key;
            UiKit.Clear(_parent);
            _lanes.Clear();
            var count = Mathf.Max(1, players.Count);
            var width = (_width - (count - 1) * _look.Gap) / count;
            for (var i = 0; i < count; i++)
            {
                var x = i * (width + _look.Gap);
                var lane = new Lane { Player = i < players.Count ? players[i] : -1, Width = width };
                // Their own colour, even alone: pale while waiting, solid as they hold.
                lane.Colour = lane.Player >= 0 ? colourOf(lane.Player) : _look.Nobody;
                var idle = Color.Lerp(_look.Light, lane.Colour, 0.3f);
                idle.a = _look.IdleOpacity;
                UiKit.RoundBox(_parent, "Empty", idle, x, 0f, width, _height, 0f);
                lane.Fill = UiKit.RoundBox(_parent, "Fill", lane.Colour, x, 0f, 0f, _height, 0f).rectTransform;
                lane.Label = UiKit.Rect(_parent, "Label", x, 0f, width, _height);
                // Scaled about its centre for the pop as the cook becomes ready.
                lane.Label.pivot = new Vector2(0.5f, 0.5f);
                lane.Label.anchoredPosition += new Vector2(width / 2f, -_height / 2f);
                _lanes.Add(lane);
            }
            return true;
        }

        /// <summary>
        /// Writes each segment's words: ready ones in white (ink on a light colour) on their colour,
        /// waiting ones in a darker shade of it on the pale. A glyph, if given, leads the words, the
        /// two centred together.
        /// </summary>
        public void Label(Func<int, bool> isReady, Func<int, bool, (string Text, Glyph? Glyph)> words)
        {
            foreach (var lane in _lanes)
            {
                UiKit.Clear(lane.Label);
                var ready = lane.Player >= 0 && isReady(lane.Player);
                var (text, glyph) = words(lane.Player, ready);
                var tone = ready
                    ? (lane.Colour.grayscale > 0.72f ? _look.Ink : Color.white) // dark only on a light colour (yellow)
                    : Color.Lerp(lane.Colour, _look.Ink, 0.45f);
                var font = ready ? _look.ReadyFont : _look.WaitingFont;
                var baseline = _height / 2f + _look.FontSize * 0.36f;
                var ring = Mathf.Round(_look.FontSize * 1.43f);
                const float gap = 10f;
                var label = UiKit.TextAt(font, lane.Label, "Text", text, _look.FontSize, tone, 0f, baseline, lane.Width - 24f - (glyph.HasValue ? ring + gap : 0f));
                var start = (lane.Width - ((glyph.HasValue ? ring + gap : 0f) + UiKit.Width(label))) / 2f;
                if (glyph.HasValue)
                    Badge(lane.Label, glyph.Value, start + ring / 2f, _height / 2f, ring);
                label.rectTransform.anchoredPosition = new Vector2(start + (glyph.HasValue ? ring + gap : 0f), label.rectTransform.anchoredPosition.y);
            }
        }

        /// <summary>
        /// Every frame: each segment eases after its player's hold (full when ready), and pops as
        /// they become ready. Returns whether anyone not yet ready is holding, and how many are ready.
        /// </summary>
        public (bool AnyHold, int Ready) Tick(float dt, Func<int, bool> isReady, Func<int, float> progress)
        {
            var ease = 1f - Mathf.Exp(-_look.FillSpeed * dt);
            var anyHold = false;
            var readyCount = 0;
            foreach (var lane in _lanes)
            {
                var ready = lane.Player >= 0 && isReady(lane.Player);
                var held = progress(lane.Player);
                var target = ready ? 1f : held;
                if (ready)
                    readyCount++;
                anyHold |= !ready && held > 0f;
                lane.Shown = Mathf.Abs(lane.Shown - target) < 0.001f ? target : Mathf.Lerp(lane.Shown, target, ease);
                lane.Fill.sizeDelta = new Vector2(lane.Width * lane.Shown, _height);

                // A gentle pop of their label as they become ready: up a little, easing back.
                if (ready && !lane.WasReady)
                    lane.Pop = _look.PopSeconds;
                lane.WasReady = ready;
                lane.Pop = Mathf.Max(0f, lane.Pop - dt);
                var t = _look.PopSeconds > 0f ? lane.Pop / _look.PopSeconds : 0f;
                var pop = 1f + _look.Pop * Mathf.Sin(t * Mathf.PI);
                lane.Label.localScale = new Vector3(pop, pop, 1f);
            }
            return (anyHold, readyCount);
        }

        // A face button as forkcast draws them: its colour, a white ring in proportion, the letter
        // in white (ink on the yellow Y).
        private void Badge(Transform parent, Glyph glyph, float cx, float cy, float diameter)
        {
            UiKit.StrokedCircle(parent, glyph.Letter, glyph.Colour, Color.white, Mathf.Max(3f, diameter * 4f / 31f), cx - diameter / 2f, cy - diameter / 2f, diameter);
            var size = diameter / 2f;
            UiKit.TextAt(_look.BadgeFont, parent, glyph.Letter, glyph.Letter, size, glyph.Letter == "Y" ? _look.Ink : Color.white, cx, cy + size * 0.37f, diameter, HorizontalAlignmentOptions.Center);
        }
    }
}
