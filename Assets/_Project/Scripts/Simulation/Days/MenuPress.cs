namespace Game.Simulation.Days
{
    /// <summary>
    /// One player's Menu button (decision 2026-11-10): a tap opens the pause menu, a hold readies up
    /// (and starts the next day from forkcast). Where holding means nothing (service, the office), a
    /// press is a tap at once, so the pause menu opens on the press, without waiting for the release.
    /// A button already down when the player arrives (the press that joined them) is ignored until
    /// it's let go.
    /// </summary>
    public sealed class MenuPress
    {
        public enum Result
        {
            None,
            Tap,
            Hold,
        }

        private bool _armed;
        private bool _down;
        private bool _held;
        private float _downFor;

        /// <summary>Seconds the button must be held to count as a hold.</summary>
        public float HoldSeconds { get; set; } = 0.6f;

        /// <summary>How far through the hold the current press is (0..1), for the fill ring; 0 when up.</summary>
        public float HoldProgress => _down && !_held && HoldSeconds > 0f ? System.Math.Min(1f, _downFor / HoldSeconds) : 0f;

        /// <summary>True while the button is pressed in a way that could still become a hold.</summary>
        public bool Holding => _down && !_held;

        /// <param name="pressed">Whether the button is down this frame.</param>
        /// <param name="seconds">Real seconds since the last update (unscaled: the pause stops game time).</param>
        /// <param name="holdMeansSomething">True in Plan, Prep and Review, where a hold readies up.</param>
        public Result Update(bool pressed, float seconds, bool holdMeansSomething)
        {
            if (!_armed)
            {
                // Seen up once: from now on, presses count.
                _armed = !pressed;
                return Result.None;
            }

            if (pressed && !_down)
            {
                _down = true;
                _held = false;
                _downFor = 0f;
                if (!holdMeansSomething)
                {
                    _held = true; // used up: letting go does nothing more
                    return Result.Tap;
                }
                return Result.None;
            }

            if (pressed)
            {
                if (_held)
                    return Result.None;
                _downFor += seconds;
                if (holdMeansSomething && _downFor >= HoldSeconds)
                {
                    _held = true;
                    return Result.Hold;
                }
                return Result.None;
            }

            // Let go.
            var wasTap = _down && !_held;
            _down = false;
            _held = false;
            _downFor = 0f;
            return wasTap ? Result.Tap : Result.None;
        }

        /// <summary>Forget the press in progress, e.g. when the menu opens: the button must come up first.</summary>
        public void Disarm()
        {
            _armed = false;
            _down = false;
            _held = false;
            _downFor = 0f;
        }
    }
}
