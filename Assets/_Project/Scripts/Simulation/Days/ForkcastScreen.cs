using System;

namespace Game.Simulation.Days
{
    /// <summary>
    /// forkcast, the one screen between days (the user's calls, 2026-11-10). It opens as Review
    /// starts and stays up through the next morning's Plan; a kitchen that loads into Plan opens on
    /// it too. Nothing closes it but starting prep: holding A on its Start button goes from Review
    /// (or Plan) straight into Prep, and it isn't seen again until the next Review.
    /// </summary>
    public sealed class ForkcastScreen
    {
        public ForkcastScreen(DayPhase phase)
        {
            IsOpen = OpensFor(phase);
        }

        public bool IsOpen { get; private set; }

        /// <summary>Raised whenever it opens or closes.</summary>
        public event Action<bool> Changed;

        /// <summary>The phases it's up for: everything between one service and the next prep.</summary>
        public static bool OpensFor(DayPhase phase) => phase == DayPhase.Plan || phase == DayPhase.Review;

        /// <summary>
        /// How many <see cref="DayCycle.Advance"/> steps "Start prep" takes from <paramref name="phase"/>:
        /// Review goes through the next morning's Plan without stopping; Plan goes on to Prep.
        /// Elsewhere there's no Start button.
        /// </summary>
        public static int StepsToPrep(DayPhase phase) => phase switch
        {
            DayPhase.Review => 2,
            DayPhase.Plan => 1,
            _ => 0,
        };

        public void PhaseChanged(DayPhase phase)
        {
            var open = OpensFor(phase);
            if (open == IsOpen)
                return;
            IsOpen = open;
            Changed?.Invoke(open);
        }
    }
}
