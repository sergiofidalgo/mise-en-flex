using Game.Gameplay;
using Game.Simulation.Service;
using UnityEngine;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The money taken this service, top centre of the screen. Each payment shows briefly next to
    /// it in green, so the reward for a dish lands where the player can see it add up.
    /// </summary>
    public sealed class MoneyHud : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenCompositionRoot _root;

        [Tooltip("Canvas pixels below the top of the screen.")]
        [SerializeField, Min(0f)] private float _topMargin = 16f;
        [SerializeField, Min(8f)] private float _fontSize = 30f;
        [Tooltip("Real seconds the last payment stays next to the total.")]
        [SerializeField, Min(0f)] private float _gainSeconds = 2f;
        [SerializeField] private Vector2 _padding = new Vector2(18f, 8f);
        [SerializeField] private Color _gainColor = new Color(0.45f, 0.85f, 0.40f);

        private WorldLabel _label;
        private ServiceSimulation _service;
        private string _currency;
        private float _lastGain;
        private float _gainUntil;

        private void Start()
        {
            _service = _root.Services.Service;
            _currency = _root.Services.ServiceConfig.Currency;
            _service.Paid += HandlePaid;
            _label = WorldLabel.Create(_style, _canvas, "Money", _padding, new Vector2(0.5f, 1f));
            _label.FontSize = _fontSize;
        }

        private void OnDestroy()
        {
            if (_service != null)
                _service.Paid -= HandlePaid;
        }

        private void HandlePaid(Customer customer)
        {
            _lastGain = customer.Bill?.Total ?? 0f;
            _gainUntil = Time.time + _gainSeconds;
        }

        private void LateUpdate()
        {
            if (_label == null)
                return;
            var text = $"{_currency}{_service.Money:0}";
            if (Time.time < _gainUntil)
                text += "  " + HudStyle.Colored($"+{_currency}{_lastGain:0}", _gainColor);
            _label.ShowAt(text, new Vector2(0f, _canvas.rect.height / 2f - _topMargin));
        }
    }
}
