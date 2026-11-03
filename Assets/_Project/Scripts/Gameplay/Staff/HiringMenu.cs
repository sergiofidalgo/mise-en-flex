using Game.Simulation.Days;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Staff
{
    /// <summary>
    /// The Plan phase's hiring controls: the face buttons A, X and Y hire the listed candidates, and
    /// B lets the current staff member go (decision 2026-11-04). Any player can press them.
    /// </summary>
    public sealed class HiringMenu : MonoBehaviour
    {
        private static readonly string[] HireActions = { "Interact", "Use", "ButtonY" };
        private const string FireAction = "ButtonB";

        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private DayLoop _dayLoop;

        private void Update()
        {
            var services = _root.Services;
            if (services.Days.Phase != DayPhase.Plan)
                return;

            foreach (var player in PlayerInput.all)
            {
                // A also joins: joining shouldn't hire someone with the same press.
                if (!_dayLoop.WasHereLastFrame(player))
                    continue;

                for (var i = 0; i < HireActions.Length && i < services.Staff.Candidates.Count; i++)
                {
                    var candidate = services.Staff.Candidates[i];
                    if (player.actions[HireActions[i]].WasPressedThisFrame() && services.Staff.CanHire(candidate, services.Days.Balance))
                        services.Staff.Hire(candidate, services.Days.Balance);
                }

                if (player.actions[FireAction].WasPressedThisFrame() && services.Staff.Hired.Count > 0)
                    services.Staff.Fire(services.Staff.Hired[services.Staff.Hired.Count - 1]);
            }
        }
    }
}
