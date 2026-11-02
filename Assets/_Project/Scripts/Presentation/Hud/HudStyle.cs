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

        [Header("Shapes (Unity's built-in UI sprites)")]
        [SerializeField] private Sprite _circle;
        [SerializeField] private Sprite _rounded;

        public TMP_FontAsset Font => _font;
        public float FontSize => _fontSize;
        public Color TextColor => _textColor;
        public Color CardBackground => _cardBackground;
        public float CardEdge => _cardEdge;
        public float BadgeSize => _badgeSize;
        public float UnavailableOpacity => _unavailableOpacity;
        public float Fade => _fade;
        public Sprite Circle => _circle;
        public Sprite Rounded => _rounded;
        public Color KeycapText => _keycapText;

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
