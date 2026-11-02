using Game.Gameplay.Stations;
using TMPro;
using UnityEngine;

namespace Game.Presentation.Hud
{
    /// <summary>One prompt line: a button badge and a short verb ("take", "flip", "Steak ×6").</summary>
    public sealed class PromptRow : MonoBehaviour
    {
        [SerializeField] private HudStyle _style;
        [SerializeField] private ButtonBadge _badge;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private CanvasGroup _group;

        public void Show(PromptLine line, bool gamepad)
        {
            gameObject.SetActive(true);
            _badge.Set(line.Control, gamepad);
            _label.text = line.Label;
            _group.alpha = line.Available ? 1f : _style.UnavailableOpacity;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
