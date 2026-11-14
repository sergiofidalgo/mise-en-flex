using System;
using Game.Data;
using Game.Simulation.Days;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay
{
    /// <summary>
    /// New players join a kitchen only in Prep (the user's calls, 2026-11-13 and 2026-11-14), or
    /// when nobody is playing yet. On the dashboard (Plan, Review) and during service joining is closed, and a controller nobody plays on that presses A
    /// raises <see cref="Refused"/>, so the HUD can tell them to wait until it's over.
    /// </summary>
    public sealed class KitchenJoinGate : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private PlayerInputManager _manager;
        [SerializeField] private CoopSettings _settings;

        private bool _closed;

        /// <summary>Raised with the phase when someone tries to join while they can't.</summary>
        public event Action<DayPhase> Refused;

        private void Update()
        {
            var days = _root != null && _root.Services != null ? _root.Services.Days : null;
            if (days == null)
                return;
            var allowed = JoinRules.Allows(days.Phase, _manager.playerCount);
            // Kept every frame: the pause menu and a player leaving switch joining back on.
            if (!allowed && _manager.joiningEnabled)
                _manager.DisableJoining();
            else if (allowed && _closed && !_manager.joiningEnabled && _manager.playerCount < _settings.MaxPlayers)
                _manager.EnableJoining();
            _closed = !allowed;
            if (allowed)
                return;
            foreach (var pad in Gamepad.all)
                if (pad.buttonSouth.wasPressedThisFrame && !IsPlaying(pad))
                {
                    Refused?.Invoke(days.Phase);
                    return;
                }
        }

        private static bool IsPlaying(InputDevice device)
        {
            foreach (var player in PlayerInput.all)
                foreach (var paired in player.devices)
                    if (paired == device)
                        return true;
            return false;
        }
    }
}
