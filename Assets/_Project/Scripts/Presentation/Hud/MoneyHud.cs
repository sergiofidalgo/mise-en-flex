using Game.Gameplay;
using Game.Simulation.Days;
using Game.Simulation.Service;
using UnityEngine;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The money at the top of the screen beside the clock: the balance in Prep, what this service has
    /// made during it. Each payment shows briefly next to it in green and each walk-out in red, so
    /// rewards and losses land where they add up. Money paid out, such as stock bought at a street
    /// stall, shows in red beside it too, and the total itself turns red and flinches (decision
    /// 2026-11-10), so a purchase is felt even mid-rush.
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
        [Tooltip("Real seconds the total stays red and shakes after money's paid out.")]
        [SerializeField, Min(0f)] private float _flinchSeconds = 0.5f;
        [Tooltip("Canvas pixels the total shakes side to side at first.")]
        [SerializeField, Min(0f)] private float _flinchDistance = 7f;
        [Tooltip("Shake speed in radians per second.")]
        [SerializeField, Min(0f)] private float _flinchFrequency = 55f;

        private WorldLabel _label;
        private ServiceSimulation _service;
        private float _lastGain;
        private float _gainUntil;
        private float _flinchedAt = float.MinValue;
        private DayCycle _days;

        private void Start()
        {
            _service = _root.Services.Service;
            _service.Paid += HandlePaid;
            _service.WalkedOut += HandleWalkedOut;
            _root.Services.ServiceReplaced += HandleServiceReplaced;
            _days = _root.Services.Days;
            _days.Charged += HandleCharged;
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
            if (_days != null)
                _days.Charged -= HandleCharged;
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

        private void HandleCharged(LedgerLine line, float amount)
        {
            if (amount <= 0f)
                return;
            _lastGain = -amount;
            _gainUntil = Time.time + _gainSeconds;
            _flinchedAt = Time.time;
        }

        private void LateUpdate()
        {
            if (_label == null)
                return;
            // In Prep the balance, which purchases come out of; in service what it has made. The
            // Review has the day's money, and Plan its own balance line.
            var phase = _root.Services.Days.Phase;
            if (phase != DayPhase.Service && phase != DayPhase.Prep)
            {
                _label.Hide();
                return;
            }
            // In the player's language and currency (Locale).
            var total = phase == DayPhase.Prep ? _root.Services.Days.Balance : _service.Money;
            var text = Game.Data.Locale.Money(total, Game.Data.MoneyStyle.Whole);

            // Just paid out: the total goes red and shakes, fading back over the flinch.
            var position = new Vector2(_besideClock, _canvas.rect.height / 2f - _topMargin);
            var since = Time.time - _flinchedAt;
            if (since < _flinchSeconds)
            {
                text = HudStyle.Colored(text, _lossColor);
                var fade = 1f - since / _flinchSeconds;
                position.x += Mathf.Sin(since * _flinchFrequency) * _flinchDistance * fade;
            }
            else if (total < 0f)
            {
                text = HudStyle.Colored(text, _lossColor); // in debt to the boss
            }

            if (Time.time < _gainUntil)
                text += "  " + HudStyle.Colored(Game.Data.Locale.Money(_lastGain, Game.Data.MoneyStyle.Whole, signed: true), _lastGain < 0f ? _lossColor : _gainColor);
            _label.ShowAt(text, position);
        }
    }
}
