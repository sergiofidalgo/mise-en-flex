using System;
using Game.Gameplay.Stations;
using TMPro;
using UnityEngine;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// One place for the HUD's look: font, sizes, colours and fades (decision 2026-11-02: in-world
    /// information plus Xbox-coloured badges). Keeping it in one asset means the whole HUD can be
    /// retuned, or given colour-blind palettes, without touching code.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/HUD Style", fileName = "HudStyle")]
    public sealed class HudStyle : ScriptableObject
    {
        [Serializable]
        public sealed class NamedColor
        {
            [SerializeField] private string _name;
            [SerializeField] private Color _color;

            public NamedColor() { }

            public NamedColor(string name, Color color)
            {
                _name = name;
                _color = color;
            }

            public string Name => _name;
            public Color Color => _color;
        }

        [Header("Text")]
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField, Min(8f)] private float _fontSize = 22f;
        [SerializeField] private Color _textColor = new Color(0.95f, 0.96f, 0.97f);

        [Header("Cards")]
        [SerializeField] private Color _cardBackground = new Color(0.07f, 0.08f, 0.10f, 0.86f);
        [Tooltip("Pixels of the player-coloured edge around a card.")]
        [SerializeField, Min(0f)] private float _cardEdge = 3f;

        [Header("Badges (Xbox colours; keyboard keycaps)")]
        [SerializeField, Min(8f)] private float _badgeSize = 34f;
        [SerializeField] private Color _buttonA = new Color(0.42f, 0.74f, 0.25f);
        [SerializeField] private Color _buttonB = new Color(0.86f, 0.25f, 0.22f);
        [SerializeField] private Color _buttonX = new Color(0.20f, 0.50f, 0.90f);
        [SerializeField] private Color _buttonY = new Color(0.96f, 0.76f, 0.17f);
        [SerializeField] private Color _triggers = new Color(0.32f, 0.33f, 0.37f);
        [SerializeField] private Color _keycap = new Color(0.88f, 0.89f, 0.91f);
        [SerializeField] private Color _keycapText = new Color(0.12f, 0.12f, 0.14f);

        [Header("States")]
        [Tooltip("Opacity of an action that isn't available right now.")]
        [SerializeField, Range(0f, 1f)] private float _unavailableOpacity = 0.35f;
        [Tooltip("Seconds for prompts to fade in or out.")]
        [SerializeField, Min(0f)] private float _fade = 0.12f;
        [Tooltip("Canvas pixels every card keeps from the screen's edges: cards that follow something near an edge slide back in.")]
        [SerializeField, Min(0f)] private float _screenMargin = 12f;

        [Header("Doneness (tickets, food tags)")]
        [Tooltip("One colour per doneness level name, shared by order tickets and food tags so they can be matched at a glance. Close to the steak's own band colours, brightened to read on dark cards.")]
        [SerializeField] private NamedColor[] _donenessColors =
        {
            new NamedColor("Raw", new Color(0.62f, 0.55f, 0.85f)),
            new NamedColor("Burnt", new Color(1f, 0.32f, 0.24f)),
            new NamedColor("Rare", new Color(0.95f, 0.25f, 0.28f)),
            new NamedColor("Medium", new Color(0.98f, 0.72f, 0.46f)),
            new NamedColor("Well done", new Color(0.66f, 0.62f, 0.58f)),
            new NamedColor("Softened", new Color(0.98f, 0.92f, 0.62f)),
            new NamedColor("Cooked", new Color(0.92f, 0.70f, 0.32f)),
        };
        [Tooltip("Font size of order tickets and food tags.")]
        [SerializeField, Min(8f)] private float _labelFontSize = 20f;

        [Header("Patience meter (tickets, queue)")]
        [SerializeField] private Color _meterTrack = new Color(0.22f, 0.23f, 0.26f, 1f);
        [Tooltip("Meter colour from empty (0) to full (1): red when they're about to walk out.")]
        [SerializeField] private ColorStop[] _patienceColors =
        {
            new ColorStop(0.15f, new Color(0.95f, 0.30f, 0.26f)),
            new ColorStop(0.40f, new Color(0.96f, 0.76f, 0.25f)),
            new ColorStop(0.70f, new Color(0.45f, 0.85f, 0.40f)),
        };
        [SerializeField, Min(1f)] private float _meterHeight = 6f;
        [Tooltip("Canvas pixels; a queue meter has no text to set its width.")]
        [SerializeField, Min(1f)] private float _meterMinWidth = 56f;
        [SerializeField, Min(0f)] private float _meterGap = 4f;

        [Header("Shapes (Unity's built-in UI sprites)")]
        [SerializeField] private Sprite _circle;
        [SerializeField] private Sprite _rounded;

        [Header("Type for screens drawn to a mockup (Inter and Lora, SIL Open Font License)")]
        [SerializeField] private TMP_FontAsset _medium;
        [SerializeField] private TMP_FontAsset _semiBold;
        [SerializeField] private TMP_FontAsset _bold;
        [SerializeField] private TMP_FontAsset _extraBold;
        [SerializeField] private TMP_FontAsset _serifItalic;

        [Header("forkcast art (exported from the dashboard mockups)")]
        [SerializeField] private Sprite _forkcastLogo;
        [SerializeField] private Sprite _searchIcon;
        [SerializeField] private Sprite _starFull;
        [SerializeField] private Sprite _starHalf;
        [SerializeField] private Sprite _starEmpty;
        [SerializeField] private Sprite _warningIcon;
        [SerializeField] private Sprite _replyMark;

        // Each falls back to the HUD font until Tools/Game/Apply UI Art has filled it in.
        public TMP_FontAsset Medium => _medium != null ? _medium : _font;
        public TMP_FontAsset SemiBold => _semiBold != null ? _semiBold : _font;
        public TMP_FontAsset Bold => _bold != null ? _bold : _font;
        public TMP_FontAsset ExtraBold => _extraBold != null ? _extraBold : Bold;
        public TMP_FontAsset SerifItalic => _serifItalic != null ? _serifItalic : _font;
        public Sprite ForkcastLogo => _forkcastLogo;
        public Sprite SearchIcon => _searchIcon;
        public Sprite StarFull => _starFull;
        public Sprite StarHalf => _starHalf;
        public Sprite StarEmpty => _starEmpty;
        public Sprite WarningIcon => _warningIcon;
        public Sprite ReplyMark => _replyMark;

        public TMP_FontAsset Font => _font;
        public float FontSize => _fontSize;
        public Color TextColor => _textColor;
        public Color CardBackground => _cardBackground;
        public float CardEdge => _cardEdge;
        public float BadgeSize => _badgeSize;
        public float UnavailableOpacity => _unavailableOpacity;
        public float Fade => _fade;
        public float ScreenMargin => _screenMargin;
        public Sprite Circle => _circle;
        public Sprite Rounded => _rounded;
        public Color KeycapText => _keycapText;
        public float LabelFontSize => _labelFontSize;
        public Color MeterTrack => _meterTrack;
        public float MeterHeight => _meterHeight;
        public float MeterMinWidth => _meterMinWidth;
        public float MeterGap => _meterGap;

        /// <summary>Colour for a 0..1 rating, the same red-to-green as the patience meter.</summary>
        public Color RatingColor(float rating) => PatienceColor(rating);

        /// <summary>Patience meter colour for 0..1 patience left.</summary>
        public Color PatienceColor(float patience) => ColorStop.Sample(_patienceColors, patience, _patienceColors[0].Color, 0f);

        /// <summary>The colour for a doneness level name; the text colour for unknown names.</summary>
        public Color DonenessColor(string name)
        {
            foreach (var entry in _donenessColors)
            {
                if (entry.Name == name)
                    return entry.Color;
            }
            return _textColor;
        }

        /// <summary><paramref name="text"/> wrapped in a TextMeshPro colour tag.</summary>
        public static string Colored(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

        /// <summary>Fill colour of a control's badge for the player's device.</summary>
        public Color BadgeColor(PromptControl control, bool gamepad)
        {
            if (control == PromptControl.Triggers)
                return _triggers;
            if (!gamepad)
                return _keycap;
            return control switch
            {
                PromptControl.South => _buttonA,
                PromptControl.East => _buttonB,
                PromptControl.West => _buttonX,
                _ => _buttonY,
            };
        }

        /// <summary>
        /// The label on a control's badge. The keyboard mirrors the face buttons as X/D/A/W with Q/E
        /// for the triggers (decision 2026-11-02).
        /// </summary>
        public static string Glyph(PromptControl control, bool gamepad) => control switch
        {
            PromptControl.South => gamepad ? "A" : "X",
            PromptControl.East => gamepad ? "B" : "D",
            PromptControl.West => gamepad ? "X" : "A",
            PromptControl.North => gamepad ? "Y" : "W",
            _ => gamepad ? "LT RT" : "Q E",
        };
    }
}
