using System;

namespace Game.Simulation.Days
{
    /// <summary>
    /// The few seconds between Prep and the doors opening (decision 2026-11-11): a 4-3-2-1 on
    /// screen while the chefs keep working, then the guests walk in. Counts real (unpaused) seconds,
    /// not the service's game clock.
    /// </summary>
    public sealed class ServiceCountdown
    {
        private const float Epsilon = 1e-4f;

        /// <summary>Seconds left; 0 when not counting.</summary>
        public float Remaining { get; private set; }

        /// <summary>The length it started from.</summary>
        public float Length { get; private set; }

        public bool Running => Remaining > 0f;

        /// <summary>The number on screen: 4, 3, 2, 1 for a 4-second countdown; 0 when not counting.</summary>
        public int Shown => Running ? (int)Math.Ceiling(Remaining - Epsilon) : 0;

        /// <summary>How far through the current number (0 as it appears, 1 as it ends).</summary>
        public float StepProgress => Running ? 1f - (Remaining - (Shown - 1)) : 0f;

        /// <summary>Raised once, as it reaches zero.</summary>
        public event Action Finished;

        /// <summary>Starts counting. A length of zero or less opens at once.</summary>
        public void Start(float seconds)
        {
            Length = Math.Max(0f, seconds);
            Remaining = Length;
            if (!Running)
                Finished?.Invoke();
        }

        public void Tick(float seconds)
        {
            if (!Running || seconds <= 0f)
                return;
            Remaining -= seconds;
            // Ticks that add up to the length leave a float crumb (4 − 40 × 0.1 isn't quite 0).
            if (Remaining < Epsilon)
                Remaining = 0f;
            if (!Running)
                Finished?.Invoke();
        }
    }
}
