using System.Collections.Generic;
using Game.Simulation.Days;
using Game.Simulation.Service;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay
{
    /// <summary>
    /// Runs the day from the players' side. In Plan, Prep and Review each player readies up with
    /// Pause (Menu on a controller, Esc on the keyboard), and the day moves on once they all have.
    /// A service ends by itself, once everyone has left.
    /// </summary>
    public sealed class DayLoop : MonoBehaviour
    {
        private const string PauseAction = "Pause";

        [SerializeField] private KitchenCompositionRoot _root;

        // Menu also joins: a player joining with it shouldn't ready up with the same press.
        private readonly HashSet<PlayerInput> _seenLastFrame = new HashSet<PlayerInput>();

        /// <summary>Who's ready in the current phase, for the HUD.</summary>
        public ReadyCheck Ready { get; } = new ReadyCheck();

        /// <summary>False on the frame a player joins, so the joining press does nothing else.</summary>
        public bool WasHereLastFrame(PlayerInput player) => _seenLastFrame.Contains(player);

        private void Update()
        {
            var days = _root.Services.Days;
            if (days.Phase == DayPhase.Service)
            {
                if (_root.Services.Service.Phase == ServicePhase.Finished)
                {
                    var summary = ServiceSummary.From(_root.Services.Service);
                    days.FinishService(summary, _root.Services.Staff.TotalWages, _root.CloseKitchen());
                    _root.Services.Staff.EndDay(summary, _root.Services.StaffConfig.ToSettings());
                }
            }
            else
            {
                ReadPresses();
                if (Ready.IsEveryoneReady(PlayerInput.all.Count))
                {
                    Ready.Clear();
                    days.Advance();
                }
            }

            _seenLastFrame.Clear();
            foreach (var player in PlayerInput.all)
                _seenLastFrame.Add(player);
        }

        private void ReadPresses()
        {
            foreach (var player in PlayerInput.all)
            {
                var action = player.actions.FindAction(PauseAction);
                if (action == null || !action.WasPressedThisFrame() || !_seenLastFrame.Contains(player))
                    continue;

                // Still rearranging the kitchen: not ready yet.
                if (player.TryGetComponent(out Stations.PlayerInteractor interactor) && interactor.IsManaging)
                    continue;
                Ready.Toggle(player.playerIndex);
            }
        }
    }
}
