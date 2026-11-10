using System.Collections.Generic;

namespace Game.Simulation.Days
{
    /// <summary>
    /// forkcast's Start button (the user's call, 2026-11-10): each player's A button, where a tap
    /// acts on what's chosen (hire, unlock) and a hold fills the Start button and readies that
    /// player. One <see cref="MenuPress"/> per player slot, so a tap is told from a hold the same way
    /// as the Menu button, and the press that joined a player is ignored until it's let go.
    /// </summary>
    public sealed class StartHold
    {
        private readonly Dictionary<int, MenuPress> _presses = new Dictionary<int, MenuPress>();

        /// <summary>Seconds A is held to fill the button.</summary>
        public float HoldSeconds { get; set; } = 0.8f;

        /// <param name="player">The player slot.</param>
        /// <param name="down">Whether their A is down this frame.</param>
        /// <param name="seconds">Real seconds since the last update.</param>
        /// <returns>Tap when let go early, Hold the moment the button fills, otherwise None.</returns>
        public MenuPress.Result Update(int player, bool down, float seconds)
        {
            var press = PressOf(player);
            press.HoldSeconds = HoldSeconds;
            return press.Update(down, seconds, holdMeansSomething: true);
        }

        /// <summary>How full this player's hold is (0..1).</summary>
        public float Progress(int player) => _presses.TryGetValue(player, out var press) ? press.HoldProgress : 0f;

        /// <summary>The fullest hold of anyone's, for the button's bar.</summary>
        public float MostProgress
        {
            get
            {
                var most = 0f;
                foreach (var press in _presses.Values)
                    most = System.Math.Max(most, press.HoldProgress);
                return most;
            }
        }

        /// <summary>The press was used for something else (the reply wheel): it must come up first.</summary>
        public void Disarm(int player) => PressOf(player).Disarm();

        /// <summary>Everyone's press, e.g. as forkcast opens.</summary>
        public void DisarmAll()
        {
            foreach (var press in _presses.Values)
                press.Disarm();
        }

        private MenuPress PressOf(int player)
        {
            if (!_presses.TryGetValue(player, out var press))
                _presses[player] = press = new MenuPress();
            return press;
        }
    }
}
