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
    /// chooses, X deletes and Menu finishes (as Done), read straight from the player's controller. <see cref="Arrived"/> says when a player is
    /// ready: at once for one carried over, on Done for a new one (the office's lift dings then).
    /// </summary>
    public sealed class JoinNaming : MonoBehaviour
    {
        // Read from the player's own controller, not their actions: the kitchen switches action
        // maps by phase (Plan's cards), and naming must work in any of them.
        private enum Button { A, X, Start }

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

        private static readonly HashSet<PlayerInput> Everyone = new HashSet<PlayerInput>();

        /// <summary>True while <paramref name="player"/> is naming themselves, in whichever scene: menus that read every player's buttons (forkcast, ready-up) leave them alone.</summary>
        public static bool IsBusyNaming(PlayerInput player) => player != null && Everyone.Contains(player);

        private void OnEnable()
        {
            _manager.onPlayerJoined += HandleJoined;
            _manager.onPlayerLeft += HandleLeft;
        }

        private void OnDisable()
        {
            foreach (var player in _sessions.Keys)
                Everyone.Remove(player);
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
            _sessions[player] = new Session
            {
                Player = player,
                Naming = new ChefNaming(profile.Name(slot)),
                // The A that joined them mustn't also press a key.
                StartedFrame = Time.frameCount,
            };
            Hold(player, true);
            Everyone.Add(player);
        }

        private void HandleLeft(PlayerInput player)
        {
            _sessions.Remove(player);
            Everyone.Remove(player);
        }

        private void Update()
        {
            _done.Clear();
            foreach (var session in _sessions.Values)
            {
                var player = session.Player;
                if (player == null || session.StartedFrame == Time.frameCount)
                    continue;
                var naming = session.Naming;
                if (WasPressed(player, Button.X))
                    Pressed?.Invoke(player, naming.Delete());
                var step = Push(session);
                if (step != Vector2Int.zero)
                    naming.Move(step.x, -step.y);
                if (WasPressed(player, Button.A))
                    Pressed?.Invoke(player, naming.Press());
                else if (WasPressed(player, Button.Start))
                    Pressed?.Invoke(player, naming.Finish());
                if (naming.Done)
                    _done.Add(session);
            }
            foreach (var session in _done)
            {
                _sessions.Remove(session.Player);
                ChefProfileFile.SetName(session.Player.playerIndex, session.Naming.Chosen);
                Hold(session.Player, false);
                Everyone.Remove(session.Player);
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
            var value = Direction(session.Player);
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

        // The buttons by their Xbox names; on the keyboard (for testing) X and A stand in for A and
        // X (decision 2026-11-02), and Escape for Menu.
        private static bool WasPressed(PlayerInput player, Button button)
        {
            foreach (var device in player.devices)
            {
                if (device is Gamepad pad)
                {
                    var control = button == Button.A ? pad.buttonSouth : button == Button.X ? pad.buttonWest : pad.startButton;
                    if (control.wasPressedThisFrame)
                        return true;
                }
                else if (device is Keyboard keys)
                {
                    var key = button == Button.A ? keys.xKey : button == Button.X ? keys.aKey : keys.escapeKey;
                    if (key.wasPressedThisFrame || (button == Button.A && keys.enterKey.wasPressedThisFrame))
                        return true;
                }
            }
            return false;
        }

        // The D-pad (or the arrow keys): too loose on the stick for a grid of keys (2026-11-10).
        private static Vector2 Direction(PlayerInput player)
        {
            foreach (var device in player.devices)
            {
                if (device is Gamepad pad)
                {
                    var value = pad.dpad.ReadValue();
                    if (value.sqrMagnitude > 0.01f)
                        return value;
                }
                else if (device is Keyboard keys)
                {
                    var value = new Vector2(
                        (keys.rightArrowKey.isPressed ? 1f : 0f) - (keys.leftArrowKey.isPressed ? 1f : 0f),
                        (keys.upArrowKey.isPressed ? 1f : 0f) - (keys.downArrowKey.isPressed ? 1f : 0f));
                    if (value.sqrMagnitude > 0.01f)
                        return value;
                }
            }
            return Vector2.zero;
        }
    }
}
