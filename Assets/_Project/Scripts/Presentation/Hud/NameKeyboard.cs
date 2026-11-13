using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Stations;
using Game.Simulation.Campaign;
using Game.Simulation.Characters;
using TMPro;
using UnityEngine;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The naming keyboard a new player gets as they join (the user's calls, 2026-11-13): a name
    /// plate as wide as their player card, hanging under it, with the name so far, last time's name
    /// to keep, the letter grid, and Space and Done; X deletes and Menu finishes. The key under the
    /// cursor fills in the player's colour. Only Xbox buttons are named. Drawn by the office's
    /// player bar and the kitchen's player cards alike.
    /// </summary>
    public static class NameKeyboard
    {
        private const float Pad = 16f;
        private const float Gap = 6f;
        private const float KeyHeight = 44f;
        private const float LetterHeight = 42f;
        private static readonly Color Cream = new Color(0.95f, 0.91f, 0.82f);
        private static readonly Color Dim = new Color(0.95f, 0.91f, 0.82f, 0.62f);
        private static readonly Color Gold = new Color(0.93f, 0.74f, 0.33f);
        private static readonly Color Key = new Color(0.95f, 0.91f, 0.82f, 0.1f);
        private static readonly Color Ink = new Color(0.16f, 0.11f, 0.09f);

        /// <summary>
        /// Clears <paramref name="parent"/> and draws <paramref name="naming"/> into it,
        /// <paramref name="width"/> wide, from its top-left. Returns the height drawn.
        /// </summary>
        public static float Draw(HudStyle style, RectTransform parent, ChefNaming naming, Color colour, float width)
        {
            UiKit.Clear(parent);
            var picker = naming.Picker;
            var inner = width - 2f * Pad;
            var content = UiKit.Rect(parent, "Content", 0f, 0f, width, 10f);
            var y = Pad;

            UiKit.TextAt(style.ExtraBold, content, "Title", Tr.Get("Your name"), 26f, Cream, Pad, y + 26f, inner);
            y += 36f;

            // The name so far, a caret after it, and how much room is left.
            UiKit.RoundBox(content, "Field", new Color(Cream.r, Cream.g, Cream.b, 0.14f), Pad, y, inner, 52f, 12f);
            var typed = picker.Text.Length > 0
                ? picker.Text + $"<color=#{ColorUtility.ToHtmlStringRGB(colour)}>|</color>"
                : $"<color=#{ColorUtility.ToHtmlStringRGBA(Dim)}>{Tr.Get("Pick or type")}</color>";
            UiKit.TextAt(style.Bold, content, "Typed", typed, 28f, Cream, Pad + 14f, y + 36f, inner - 76f);
            UiKit.TextAt(style.Medium, content, "Count", $"{picker.Text.Length}/{picker.MaxLength}", 18f, Dim, Pad + inner - 12f, y + 33f, 60f, HorizontalAlignmentOptions.Right);
            y += 64f;

            for (var r = 0; r < picker.Rows.Count; r++)
            {
                var row = picker.Rows[r];
                if (row.Count == 0)
                    continue;
                if (r == 0)
                    y = Label(style, content, Tr.Get("Same as last time"), Pad, y, inner);
                else if (r == 1)
                    y = Label(style, content, Tr.Get("Or one of these"), Pad, y, inner);
                y = Keys(style, content, row, r, picker, colour, y, inner) + (r <= 1 ? 12f : Gap);
            }
            y += 8f;

            var x = Hint(style, content, PromptControl.West, Tr.Get("Delete"), Pad, y);
            MenuHint(style, content, Tr.Get("Done"), x + 18f, y);
            var height = y + 34f + Pad;

            // The plate behind it all, drawn last and sent to the back: its height is known now.
            var back = UiKit.Rect(parent, "Back", 0f, 0f, width, height);
            NamePlate.Draw(style, back, colour, 0f, 0f, width, height);
            back.SetAsFirstSibling();
            content.sizeDelta = new Vector2(width, height);
            parent.sizeDelta = new Vector2(width, height);
            return height;
        }

        private static float Label(HudStyle style, RectTransform parent, string text, float x, float y, float width)
        {
            UiKit.TextAt(style.Bold, parent, "Label", text, 18f, Gold, x, y + 16f, width);
            return y + 24f;
        }

        private static float Keys(HudStyle style, RectTransform parent, IReadOnlyList<NameKey> row, int r, NamePicker picker, Color colour, float y, float width)
        {
            var letters = row[0].Kind == NameKeyKind.Letter;
            var height = letters ? LetterHeight : KeyHeight;
            var columns = letters ? NamePicker.LetterRows[0].Length : row.Count;
            var keyWidth = row[0].Kind == NameKeyKind.Keep ? width : (width - Gap * (columns - 1)) / columns;
            for (var c = 0; c < row.Count; c++)
            {
                var key = row[c];
                var selected = picker.Row == r && picker.Column == c;
                var x = Pad + c * (keyWidth + Gap);
                UiKit.RoundBox(parent, "Key", selected ? colour : Key, x, y, keyWidth, height, 10f);
                var ink = selected ? (colour.grayscale > 0.55f ? Ink : Color.white) : Cream;
                var size = letters ? 26f : 20f;
                var label = UiKit.TextAt(style.Bold, parent, "Label", KeyLabel(key), size, ink, x + keyWidth / 2f, y + height / 2f + size * 0.36f, keyWidth - 8f, HorizontalAlignmentOptions.Center);
                if (!letters)
                {
                    // A long suggestion or word shrinks to fit its key rather than being cut.
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 15f;
                    label.fontSizeMax = size;
                }
            }
            return y + height;
        }

        private static float Hint(HudStyle style, RectTransform parent, PromptControl control, string text, float x, float y)
        {
            const float size = 34f;
            var badge = UiKit.Rect(parent, "Badge", x, y, size, size);
            UiKit.Ellipse(badge, "Disc", style.BadgeColor(control, true), 0f, 0f, size, size);
            UiKit.TextAt(style.Bold, badge, "Letter", HudStyle.Glyph(control, true), size * 0.6f, Color.white, size / 2f, size * 0.72f, size, HorizontalAlignmentOptions.Center);
            var label = UiKit.TextAt(style.Bold, parent, "Hint", text, 20f, Cream, x + size + 8f, y + 24f, 150f);
            return x + size + 8f + Mathf.Min(150f, UiKit.Width(label));
        }

        // The Xbox Menu button: a dark key with its three short lines, drawn (no font has it).
        private static void MenuHint(HudStyle style, RectTransform parent, string text, float x, float y)
        {
            const float size = 34f;
            UiKit.Ellipse(parent, "MenuKey", style.BadgeColor(PromptControl.Triggers, true), x, y, size, size);
            for (var i = -1; i <= 1; i++)
                UiKit.RoundBox(parent, "MenuLine", Color.white, x + 10f, y + size / 2f - 1.5f + i * 6f, size - 20f, 3f, 1.5f);
            UiKit.TextAt(style.Bold, parent, "Hint", text, 20f, Cream, x + size + 8f, y + 24f, 150f);
        }

        private static string KeyLabel(NameKey key) => key.Kind switch
        {
            NameKeyKind.Space => Tr.Get("Space"),
            NameKeyKind.Delete => Tr.Get("Delete"),
            NameKeyKind.Done => Tr.Get("Done"),
            _ => key.Text,
        };
    }
}
