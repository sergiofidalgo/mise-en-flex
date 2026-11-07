using Game.Data;
using Game.Gameplay.Stations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// One prompt line: a button badge and a short verb ("take", "flip"), or for an ingredient its
    /// picture and count ("×6"), falling back to its name when it has no picture.
    /// </summary>
    public sealed class PromptRow : MonoBehaviour
    {
        [SerializeField] private HudStyle _style;
        [SerializeField] private ButtonBadge _badge;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _icon;
        [SerializeField] private IngredientIcons _icons;

        public void Show(PromptLine line, bool gamepad)
        {
            gameObject.SetActive(true);
            _badge.Set(line.Control, gamepad);
            var picture = line.IconId != null ? _icons.Find(line.IconId) : null;
            _icon.gameObject.SetActive(picture != null);
            _icon.sprite = picture;
            _label.text = picture != null && line.IconLabel != null ? line.IconLabel : line.Label;
            _group.alpha = line.Available ? 1f : _style.UnavailableOpacity;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
