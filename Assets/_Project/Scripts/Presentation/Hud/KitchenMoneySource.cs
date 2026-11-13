using Game.Gameplay;
using Game.Simulation.Days;
using UnityEngine;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The money card's balance in a kitchen: the day's balance, and during service each payment as
    /// it's made (decision 2026-11-10). Hidden behind the forkcast dashboard (Review, and Plan until it's put away).
    /// </summary>
    public sealed class KitchenMoneySource : MonoBehaviour, IMoneySource
    {
        [SerializeField] private KitchenCompositionRoot _root;

        /// <summary>
        /// The money to show in a kitchen: the balance, plus during service what it has taken so far
        /// (payments and tips, less walk-outs), which the books only bank as Review starts.
        /// </summary>
        public static float Live(float balance, DayPhase phase, float takings) =>
            phase == DayPhase.Service ? balance + takings : balance;

        private DayCycle _listening;
        private bool _paidOut;

        public float? Read(out bool reset)
        {
            reset = false;
            var services = _root != null ? _root.Services : null;
            if (services == null || services.Days == null)
                return null;
            var days = services.Days;
            if (_listening != days)
            {
                if (_listening != null)
                    _listening.Charged -= HandleCharged;
                _listening = days;
                days.Charged += HandleCharged;
            }
            return Live(days.Balance, days.Phase, services.Service != null ? services.Service.Money : 0f);
        }

        // Stock bought at a stall, and any other bill: the card shakes red even for cents.
        private void HandleCharged(LedgerLine line, float amount) => _paidOut |= amount > 0f;

        public bool TakePaidOut()
        {
            var paidOut = _paidOut;
            _paidOut = false;
            return paidOut;
        }

        private void OnDestroy()
        {
            if (_listening != null)
                _listening.Charged -= HandleCharged;
        }

        // Nothing in the kitchen covers it any more: forkcast shows the money card over itself, in
        // its own corner, instead of drawing a chip of its own (the user's call, 2026-11-13).
        public bool Covered => false;
    }
}
