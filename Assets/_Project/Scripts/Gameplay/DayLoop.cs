using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Days;
using Game.Simulation.Service;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay
{
    /// <summary>
    /// Runs the day from the players' side. In Plan, Prep and Review each player readies up with
    /// Pause (Menu on a controller, Esc on the keyboard), and the day moves on once they all have.
    /// A service ends by itself, once everyone has left. A player whose controller is lost isn't
    /// waited for in the ready-up; during a service, the game stands still until it's back or
    /// someone still playing presses Menu to carry on without them (decision 2026-11-09).
    /// </summary>
    public sealed class DayLoop : MonoBehaviour
    {
        private const string PauseAction = "Pause";

        [SerializeField] private KitchenCompositionRoot _root;

        // Menu also joins: a player joining with it shouldn't ready up with the same press.
        private readonly HashSet<PlayerInput> _seenLastFrame = new HashSet<PlayerInput>();
        private readonly List<(int, bool)> _presence = new List<(int, bool)>();
        private readonly List<int> _present = new List<int>();
        private bool _stoppedTime;

        /// <summary>Who's ready in the current phase, for the HUD.</summary>
        public ReadyCheck Ready { get; } = new ReadyCheck();

        /// <summary>Who's lost their controller, and whether the service is waiting for them, for the HUD.</summary>
        public ControllerPause ControllerPause { get; } = new ControllerPause();

        /// <summary>False on the frame a player joins, so the joining press does nothing else.</summary>
        public bool WasHereLastFrame(PlayerInput player) => _seenLastFrame.Contains(player);

        private void Update()
        {
            var days = _root.Services.Days;
            UpdatePresence(days.Phase == DayPhase.Service);
            if (days.Phase == DayPhase.Service)
            {
                if (ControllerPause.Paused)
                    ReadCarryOn();
                if (_root.Services.Service.Phase == ServicePhase.Finished)
                {
                    var summary = ServiceSummary.From(_root.Services.Service);
                    var services = _root.Services;
                    var energy = services.Energy.Read(
                        services.Cooking.Hobs, services.Stations.All.Count(s => s is Game.Gameplay.Stations.GrillStation), services.Equipment.EnergyPrices);
                    days.FinishService(summary, services.Staff.TotalWages, _root.CloseKitchen(), energy);
                    _root.Services.Staff.EndDay(summary, _root.Services.StaffConfig.ToSettings());

                    // Saved as Review starts, once the day's results are in (decision 2026-11-06).
                    _root.ListTodaysReviews();
                    _root.SaveCampaign();
                }
            }
            else
            {
                ReadPresses();
                if (Ready.IsEveryoneReady(_present))
                {
                    Ready.Clear();
                    days.Advance();
                }
            }

            _seenLastFrame.Clear();
            foreach (var player in PlayerInput.all)
                _seenLastFrame.Add(player);
        }

        // Lost controllers: who they are, and whether the service stands still for them. Time stops
        // with the time scale (the simulation, customers, staff and chefs all run on scaled time),
        // and the Paused flag stops presses (the interactor's instant actions).
        private void UpdatePresence(bool inService)
        {
            _presence.Clear();
            _present.Clear();
            foreach (var player in PlayerInput.all)
            {
                var lost = PlayerPresence.IsLost(player);
                _presence.Add((player.playerIndex, lost));
                if (!lost)
                    _present.Add(player.playerIndex);
            }
            ControllerPause.Update(_presence, inService);

            var paused = ControllerPause.Paused;
            _root.Services.Paused = paused;
            if (paused != _stoppedTime)
            {
                Time.timeScale = paused ? 0f : 1f;
                _stoppedTime = paused;
            }
        }

        // While waiting, Menu from anyone still playing carries on without the lost players.
        private void ReadCarryOn()
        {
            foreach (var player in PlayerInput.all)
            {
                var action = player.actions.FindAction(PauseAction);
                if (action != null && action.WasPressedThisFrame() && !PlayerPresence.IsLost(player))
                {
                    ControllerPause.CarryOn();
                    UpdatePresence(true);
                    return;
                }
            }
        }

        // Never leave the next scene frozen.
        private void OnDisable()
        {
            if (_stoppedTime)
                Time.timeScale = 1f;
            _stoppedTime = false;
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
