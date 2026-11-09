using Game.Gameplay;
using Game.Simulation.Days;
using Game.Simulation.Service;
using UnityEngine;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The money made this service, at the top of the screen beside the clock. Each payment shows briefly next to
    /// it in green and each walk-out in red, so rewards and losses land where they add up.
    /// </summary>
    public sealed class MoneyHud : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenCompositionRoot _root;

        [Tooltip("Canvas pixels below the top of the screen, level with the clock.")]
        [SerializeField, Min(0f)] private float _topMargin = 16f;
        [Tooltip("Canvas pixels from the centre of the screen: the clock is on the other side.")]
        [SerializeField, Min(0f)] private float _besideClock = 6f;
        [SerializeField, Min(8f)] private float _fontSize = 32f;
        [Tooltip("Real seconds the last payment stays next to the total.")]
        [SerializeField, Min(0f)] private float _gainSeconds = 2f;
        [SerializeField] private Vector2 _padding = new Vector2(18f, 8f);
        [SerializeField] private Color _gainColor = new Color(0.45f, 0.85f, 0.40f);
        [SerializeField] private Color _lossColor = new Color(0.95f, 0.30f, 0.26f);

        private WorldLabel _label;
        private ServiceSimulation _service;
        private float _lastGain;
        private float _gainUntil;

        private void Start()
        {
            _service = _root.Services.Service;
            _service.Paid += HandlePaid;
            _service.WalkedOut += HandleWalkedOut;
            _root.Services.ServiceReplaced += HandleServiceReplaced;
            _label = WorldLabel.Create(_style, _canvas, "Money", _padding, new Vector2(0f, 1f));
            _label.FontSize = _fontSize;
            _label.Font = _style.DisplayFont;
        }

        private void OnDestroy()
        {
            if (_service != null)
            {
                _service.Paid -= HandlePaid;
                _service.WalkedOut -= HandleWalkedOut;
                _root.Services.ServiceReplaced -= HandleServiceReplaced;
            }
        }

        private void HandleServiceReplaced(ServiceSimulation old, ServiceSimulation next)
        {
            old.Paid -= HandlePaid;
            old.WalkedOut -= HandleWalkedOut;
            next.Paid += HandlePaid;
            next.WalkedOut += HandleWalkedOut;
            _service = next;
        }

        private void HandlePaid(Customer customer)
        {
            _lastGain = customer.Bill?.Total ?? 0f;
            _gainUntil = Time.time + _gainSeconds;
        }

        private void HandleWalkedOut(Customer customer)
        {
            _lastGain = -customer.WalkOutPenalty;
            _gainUntil = Time.time + _gainSeconds;
        }

        private void LateUpdate()
        {
            if (_label == null)
                return;
            // Only during service: the Review has the day's money, and Plan the balance.
            if (_root.Services.Days.Phase != DayPhase.Service)
            {
                _label.Hide();
                return;
            }
            // In the player's language and currency (Locale).
            var text = Game.Data.Locale.Money(_service.Money, Game.Data.MoneyStyle.Whole);
            if (Time.time < _gainUntil)
                text += "  " + HudStyle.Colored(Game.Data.Locale.Money(_lastGain, Game.Data.MoneyStyle.Whole, signed: true), _lastGain < 0f ? _lossColor : _gainColor);
            _label.ShowAt(text, new Vector2(_besideClock, _canvas.rect.height / 2f - _topMargin));
        }
    }
}
