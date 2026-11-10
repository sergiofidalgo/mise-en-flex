using System.Collections.Generic;
using Game.Simulation.Days;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay
{
    /// <summary>
    /// Every player's Menu button, read once a frame for everyone who asks (decision 2026-11-10): a
    /// tap opens the pause menu (<c>PauseMenu</c>), a hold readies up (<see cref="DayLoop"/>). The
    /// rule is <see cref="MenuPress"/>; this keeps one per player so the pause menu and the day loop
    /// see the same press. A deliberate small static, like <see cref="PlayerRoster"/>: the two
    /// readers live in different assemblies and scenes.
    /// </summary>
    public static class MenuButton
    {
        private const string PauseAction = "Pause";

        private static readonly Dictionary<PlayerInput, MenuPress> Presses = new Dictionary<PlayerInput, MenuPress>();
        private static readonly Dictionary<PlayerInput, (int Frame, MenuPress.Result Result)> ThisFrame = new Dictionary<PlayerInput, (int, MenuPress.Result)>();
        private static readonly List<PlayerInput> Gone = new List<PlayerInput>();

        /// <summary>True where a hold readies up (Plan, Prep, Review). Set by the day loop each frame.</summary>
        public static bool HoldReadies { get; set; }

        /// <summary>Seconds Menu is held to ready up.</summary>
        public static float HoldSeconds { get; set; } = 0.6f;

        /// <summary>What this player's Menu button did this frame. Asking twice in a frame gives the same answer.</summary>
        public static MenuPress.Result Read(PlayerInput player)
        {
            if (player == null)
                return MenuPress.Result.None;
            if (ThisFrame.TryGetValue(player, out var cached) && cached.Frame == Time.frameCount)
                return cached.Result;

            var press = PressOf(player);
            press.HoldSeconds = HoldSeconds;
            var action = player.actions != null ? player.actions.FindAction(PauseAction) : null;
            var down = action != null && action.enabled && action.IsPressed();
            // Still rearranging the kitchen: a hold doesn't ready up, so the pause opens on the press.
            var managing = player.TryGetComponent(out Stations.PlayerInteractor interactor) && interactor.IsManaging;
            var result = press.Update(down, Time.unscaledDeltaTime, HoldReadies && !managing);
            ThisFrame[player] = (Time.frameCount, result);
            return result;
        }

        /// <summary>How far through a hold this player is (0..1), for the ring on their ready strip.</summary>
        public static float HoldProgress(PlayerInput player) =>
            player != null && Presses.TryGetValue(player, out var press) ? press.HoldProgress : 0f;

        /// <summary>This frame's press was used (carrying on without a lost player): nobody else acts on it.</summary>
        public static void Consume(PlayerInput player)
        {
            if (player != null)
                ThisFrame[player] = (Time.frameCount, MenuPress.Result.None);
        }

        /// <summary>Everyone's button must come up before it counts again (the pause menu just closed).</summary>
        public static void DisarmAll()
        {
            foreach (var press in Presses.Values)
                press.Disarm();
        }

        private static MenuPress PressOf(PlayerInput player)
        {
            if (Presses.TryGetValue(player, out var press))
                return press;

            // Players who left or whose scene unloaded.
            Gone.Clear();
            foreach (var key in Presses.Keys)
                if (key == null)
                    Gone.Add(key);
            foreach (var key in Gone)
            {
                Presses.Remove(key);
                ThisFrame.Remove(key);
            }

            press = new MenuPress();
            Presses[player] = press;
            return press;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Presses.Clear();
            ThisFrame.Clear();
            HoldReadies = false;
        }
    }

    /// <summary>
    /// Whether the pause menu is open (decision 2026-11-10): it freezes the game for everyone. The
    /// day loop folds it into the same stop as a lost controller (time scale 0 and
    /// <see cref="KitchenServices.Paused"/>); scenes without a day loop (the office) leave the time
    /// scale to the menu.
    /// </summary>
    public static class GamePause
    {
        public static bool MenuOpen { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => MenuOpen = false;
    }
}
