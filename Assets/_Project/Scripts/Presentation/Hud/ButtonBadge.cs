using Game.Gameplay.Stations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// A control drawn as a badge: an Xbox-coloured circle for face buttons, a pill for the
    /// triggers, or a light keycap for keyboard players.
    /// </summary>
    public sealed class ButtonBadge : MonoBehaviour
    {
        // Pills for the triggers are wider than a round badge to fit both labels.
        private const float TriggerWidthFactor = 1.9f;

        [SerializeField] private HudStyle _style;
        [SerializeField] private RectTransform _rect;
        [SerializeField] private LayoutElement _layout;
        [SerializeField] private Image _background;
        [SerializeField] private TextMeshProUGUI _glyph;

        public void Set(PromptControl control, bool gamepad)
        {
            var triggers = control == PromptControl.Triggers;
            var stick = control == PromptControl.LeftStick;
            var round = (gamepad || stick) && !triggers;
            var size = _style.BadgeSize;

            var width = triggers ? size * TriggerWidthFactor : size;
            _rect.sizeDelta = new Vector2(width, size);
            _layout.preferredWidth = width;
            _layout.preferredHeight = size;
            _background.sprite = round ? _style.Circle : _style.Rounded;
            _background.color = _style.BadgeColor(control, gamepad);

            _glyph.font = _style.DisplayFont;
            _glyph.text = HudStyle.Glyph(control, gamepad);
            _glyph.color = gamepad || triggers || stick ? Color.white : _style.KeycapText;
            _glyph.fontSize = triggers || stick ? size * 0.42f : size * 0.6f;
        }
    }
}
