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

        public float? Read(out bool reset)
        {
            reset = false;
            var services = _root != null ? _root.Services : null;
            if (services == null || services.Days == null)
                return null;
            var days = services.Days;
            return Live(days.Balance, days.Phase, services.Service != null ? services.Service.Money : 0f);
        }

        public bool Covered
        {
            get
            {
                var services = _root != null ? _root.Services : null;
                return services != null && services.Forkcast != null && services.Forkcast.IsOpen;
            }
        }
    }
}
