using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Days;
using Game.Simulation.Service;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay
{
    /// <summary>
    /// Runs the day from the players' side. In Prep each player readies up by holding Y (the
    /// user's call, 2026-11-11, as on the forkcast dashboard in Plan and Review); a tap of Y still
    /// does its job at the station they face, and holding again takes the ready back. Menu only
    /// pauses. The day moves on once they all are.
    /// A service ends by itself, once everyone has left. A player whose controller is lost isn't
    /// waited for in the ready-up; during a service, the game stands still until it's back or
    /// someone still playing presses Menu to carry on without them (decision 2026-11-09).
    /// </summary>
    public sealed class DayLoop : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [Tooltip("The boss's morning visits: nobody readies up while he's in.")]
        [SerializeField] private Debt.BossVisitDirector _boss;

        private const string ReadyAction = "ButtonY";

        // A press already down when a player joins isn't a hold until it's let go (StartHold).
        private readonly HashSet<PlayerInput> _seenLastFrame = new HashSet<PlayerInput>();
        private readonly List<(int, bool)> _presence = new List<(int, bool)>();
        private readonly List<int> _present = new List<int>();
        private bool _stoppedTime;

        /// <summary>Who's ready in the current phase, for the HUD.</summary>
        public ReadyCheck Ready { get; } = new ReadyCheck();

        // Each player's Y in Prep: a hold toggles their ready.
        private readonly StartHold _readyHold = new StartHold();

        /// <summary>How far through holding Y to ready up this player is (0..1), for the ready bar.</summary>
        public float ReadyHoldProgress(int player) => _readyHold.Progress(player);

        /// <summary>Who's lost their controller, and whether the service is waiting for them, for the HUD.</summary>
        public ControllerPause ControllerPause { get; } = new ControllerPause();

        /// <summary>
        /// A restaurant with no finished day starts straight in day 1's Prep: there's no summary to
        /// show before day 1 (the user's call, 2026-11-11). On in the game; the play-test suite turns
        /// it off (SaveIsolation) so its tests can drive a new kitchen's day by hand from Plan.
        /// </summary>
        public static bool NewRestaurantsStartInPrep { get; set; } = true;

        private bool _started;

        /// <summary>False on the frame a player joins, so the joining press does nothing else.</summary>
        public bool WasHereLastFrame(PlayerInput player) => _seenLastFrame.Contains(player);

        private void Update()
        {
            var days = _root.Services.Days;
            // Once, on the first frame (everything has started by then, so Prep's own setup runs as
            // usual): a new restaurant skips the summary it has nothing to show in.
            if (!_started)
            {
                _started = true;
                if (NewRestaurantsStartInPrep && days.Phase == DayPhase.Plan && days.History.Count == 0)
                    days.Advance();
            }
            // Menu never readies up (Y does, 2026-11-11), so a tap on it opens the pause menu at once.
            MenuButton.HoldReadies = false;
            UpdatePresence(days.Phase == DayPhase.Service);
            if (days.Phase == DayPhase.Service)
            {
                if (ControllerPause.Paused && !GamePause.MenuOpen)
                    ReadCarryOn();
                if (_root.Services.Service.Phase == ServicePhase.Finished)
                {
                    var summary = ServiceSummary.From(_root.Services.Service);
                    var services = _root.Services;
                    var energy = services.Energy.Read(
                        services.Cooking.Hobs, services.Stations.All.Count(s => s is Game.Gameplay.Stations.GrillStation), services.Equipment.EnergyPrices);
                    days.FinishService(summary, services.Staff.TotalWages, _root.CloseKitchen(), energy);
                    _root.Services.Staff.EndDay(summary, _root.Services.StaffConfig.ToSettings());

                    // The boss's collection moves the escalation on or resets it, before the save.
                    _root.RecordCollection();

                    // Saved as Review starts, once the day's results are in (decision 2026-11-06).
                    _root.ListTodaysReviews();
                    _root.SaveCampaign();
                }
            }
            else if (!GamePause.MenuOpen && (_boss == null || !_boss.Busy))
            {
                if (days.Phase == DayPhase.Prep)
                    ReadPresses();
                else
                    _readyHold.DisarmAll();
                if (Ready.IsEveryoneReady(_present))
                {
                    Ready.Clear();
                    // forkcast's "Start day N" goes from Review through the next morning without
                    // stopping (the user's call, 2026-11-10); Prep's own hold opens the doors.
                    var steps = days.Phase == DayPhase.Prep ? 1 : ForkcastScreen.StepsToPrep(days.Phase);
                    for (var i = 0; i < steps; i++)
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

            // The pause menu freezes everyone the same way (decision 2026-11-10).
            var paused = ControllerPause.Paused || GamePause.MenuOpen;
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
                if (!PlayerPresence.IsLost(player) && MenuButton.Read(player) == MenuPress.Result.Tap)
                {
                    MenuButton.Consume(player); // carrying on, not pausing
                    ControllerPause.CarryOn();
                    UpdatePresence(true);
                    return;
                }
            }
        }

        // Never leave the next scene frozen.
        private void OnDisable()
        {
            MenuButton.HoldReadies = false;
            if (_stoppedTime)
                Time.timeScale = 1f;
            _stoppedTime = false;
        }

        private void ReadPresses()
        {
            _readyHold.HoldSeconds = MenuButton.HoldSeconds; // the pause menu's tuned hold
            foreach (var player in PlayerInput.all)
            {
                var action = player.actions != null ? player.actions.FindAction(ReadyAction) : null;
                var down = action != null && action.enabled && action.IsPressed();
                // Rearranging the kitchen, Y opens the shop: it never readies up mid-move.
                if (player.TryGetComponent(out Stations.PlayerInteractor interactor) && interactor.IsManaging)
                {
                    _readyHold.Disarm(player.playerIndex);
                    continue;
                }
                // Read for everyone, so the press that joined a player is seen and ignored. A tap
                // is the station's (PlayerInteractor); only a hold counts here.
                var press = _readyHold.Update(player.playerIndex, down, Time.unscaledDeltaTime);
                if (_seenLastFrame.Contains(player) && press == MenuPress.Result.Hold)
                    Ready.Toggle(player.playerIndex);
            }
        }
    }
}
