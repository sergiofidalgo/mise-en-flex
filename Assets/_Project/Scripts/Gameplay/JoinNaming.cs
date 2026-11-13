using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Gameplay.Saving;
using Game.Gameplay.Stations;
using Game.Simulation.Characters;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay
{
    /// <summary>
    /// Every new player names themselves as they join (the user's calls, 2026-11-13), in the office
    /// or a kitchen: the naming keyboard opens under their card, they stand still until Done, and
    /// the name goes on their card and into their chef's profile. Players carried over from the
    /// last scene aren't new and skip it. The D-pad moves (repeating while held), A types or
    /// chooses, X deletes, Y rolls other suggestions. <see cref="Arrived"/> says when a player is
    /// ready: at once for one carried over, on Done for a new one (the office's lift dings then).
    /// </summary>
    public sealed class JoinNaming : MonoBehaviour
    {
        private const string InteractAction = "Interact";
        private const string DeleteAction = "Use"; // X
        private const string RollAction = "ButtonY";
        private const string NavigateAction = "Navigate"; // the D-pad or arrows

        [SerializeField] private PlayerInputManager _manager;
        [SerializeField] private PlayerJoinController _joins;
        [Header("Feel")]
        [SerializeField, Range(0.1f, 0.95f)] private float _threshold = 0.5f;
        [Tooltip("Holding the D-pad steps again after this long (s), then every repeat (s).")]
        [SerializeField, Min(0.02f)] private float _repeatDelay = 0.3f;
        [SerializeField, Min(0.02f)] private float _repeatEvery = 0.09f;

        private sealed class Session
        {
            public PlayerInput Player;
            public ChefNaming Naming;
            public int StartedFrame;
            public Vector2Int Held;
            public float HeldFor;
            public float NextRepeat;
        }

        /// <summary>
        /// Off: players join without naming, as before. For PlayMode tests that join a chef and
        /// drive them at once (their suite turns it off; the naming tests turn it back on).
        /// </summary>
        public static bool Enabled { get; set; } = true;

        private readonly Dictionary<PlayerInput, Session> _sessions = new Dictionary<PlayerInput, Session>();
        private readonly List<Session> _done = new List<Session>();

        /// <summary>Raised when a player is ready to play: carried over (at once) or named (on Done).</summary>
        public event Action<PlayerInput> Arrived;

        /// <summary>Raised on every key that did something, and on a refused press, for feedback.</summary>
        public event Action<PlayerInput, bool> Pressed;

        /// <summary>The naming in progress for a player slot, or null.</summary>
        public ChefNaming For(int slot) => _sessions.Values.FirstOrDefault(s => s.Player != null && s.Player.playerIndex == slot)?.Naming;

        public bool IsNaming(PlayerInput player) => player != null && _sessions.ContainsKey(player);

        private void OnEnable()
        {
            _manager.onPlayerJoined += HandleJoined;
            _manager.onPlayerLeft += HandleLeft;
        }

        private void OnDisable()
        {
            _manager.onPlayerJoined -= HandleJoined;
            _manager.onPlayerLeft -= HandleLeft;
        }

        private void HandleJoined(PlayerInput player)
        {
            if (!Enabled || (_joins != null && _joins.IsRejoining))
            {
                Arrived?.Invoke(player);
                return;
            }
            var slot = player.playerIndex;
            var profile = ChefProfileFile.Current;
            var taken = PlayerInput.all.Where(p => p != player).Select(p => profile.Name(p.playerIndex)).Where(n => n.Length > 0);
            var pool = GuestNames.All.TryGetValue(Locale.Language, out var names) ? names : GuestNames.All["en"];
            var seed = unchecked(slot * 31 + Environment.TickCount);
            _sessions[player] = new Session
            {
                Player = player,
                Naming = new ChefNaming(profile.Name(slot), pool, taken, seed),
                // The A that joined them mustn't also press a key.
                StartedFrame = Time.frameCount,
            };
            Hold(player, true);
        }

        private void HandleLeft(PlayerInput player) => _sessions.Remove(player);

        private void Update()
        {
            _done.Clear();
            foreach (var session in _sessions.Values)
            {
                var player = session.Player;
                if (player == null || session.StartedFrame == Time.frameCount)
                    continue;
                var naming = session.Naming;
                if (WasPressed(player, RollAction))
                {
                    naming.Roll();
                    Pressed?.Invoke(player, true);
                }
                if (WasPressed(player, DeleteAction))
                    Pressed?.Invoke(player, naming.Delete());
                var step = Push(session);
                if (step != Vector2Int.zero)
                    naming.Move(step.x, -step.y);
                if (WasPressed(player, InteractAction))
                    Pressed?.Invoke(player, naming.Press());
                if (naming.Done)
                    _done.Add(session);
            }
            foreach (var session in _done)
            {
                _sessions.Remove(session.Player);
                ChefProfileFile.SetName(session.Player.playerIndex, session.Naming.Chosen);
                Hold(session.Player, false);
                Arrived?.Invoke(session.Player);
            }
        }

        // While naming, the chef stands still and their buttons don't reach the kitchen or office.
        private static void Hold(PlayerInput player, bool naming)
        {
            if (player.TryGetComponent<PlayerInteractor>(out var hands))
                hands.IsBusy = naming;
        }

        // One step per push, then repeating while held, like a menu. The stronger axis wins, so a
        // slightly diagonal push doesn't skip a row.
        private Vector2Int Push(Session session)
        {
            var value = session.Player.actions[NavigateAction].ReadValue<Vector2>();
            var direction = Vector2Int.zero;
            if (Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y)) >= _threshold)
                direction = Mathf.Abs(value.x) >= Mathf.Abs(value.y)
                    ? new Vector2Int((int)Mathf.Sign(value.x), 0)
                    : new Vector2Int(0, (int)Mathf.Sign(value.y));
            if (direction == Vector2Int.zero)
            {
                session.Held = Vector2Int.zero;
                return direction;
            }
            if (direction != session.Held)
            {
                session.Held = direction;
                session.HeldFor = 0f;
                session.NextRepeat = _repeatDelay;
                return direction;
            }
            session.HeldFor += Time.unscaledDeltaTime;
            if (session.HeldFor < session.NextRepeat)
                return Vector2Int.zero;
            session.NextRepeat += _repeatEvery;
            return direction;
        }

        private static bool WasPressed(PlayerInput player, string action)
        {
            var input = player.actions.FindAction(action);
            return input != null && input.WasPressedThisFrame();
        }
    }
}
