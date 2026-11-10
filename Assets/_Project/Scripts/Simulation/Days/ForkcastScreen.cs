using System;

namespace Game.Simulation.Days
{
    /// <summary>
    /// Whether the forkcast dashboard is up (the user's call, 2026-11-10). It opens as Review starts
    /// and stays up into the next morning's Plan, and a kitchen that loads into Plan opens on it.
    /// During Plan, B (or View, which goes to rearranging) closes it so the chefs can walk around,
    /// and Y reopens it.
    /// Review keeps it up (the chefs wait); Prep and Service never show it.
    /// </summary>
    public sealed class ForkcastScreen
    {
        public ForkcastScreen(DayPhase phase)
        {
            Phase = phase;
            IsOpen = OpensFor(phase);
        }

        public DayPhase Phase { get; private set; }

        public bool IsOpen { get; private set; }

        /// <summary>Raised whenever it opens or closes.</summary>
        public event Action<bool> Changed;

        /// <summary>The phases it's up for as they start.</summary>
        public static bool OpensFor(DayPhase phase) => phase == DayPhase.Plan || phase == DayPhase.Review;

        /// <summary>Only in Plan can players put it away and bring it back.</summary>
        public bool CanToggle => Phase == DayPhase.Plan;

        public void PhaseChanged(DayPhase phase)
        {
            Phase = phase;
            Set(OpensFor(phase));
        }

        /// <summary>B in Plan. Returns whether it closed.</summary>
        public bool Close()
        {
            if (!CanToggle || !IsOpen)
                return false;
            Set(false);
            return true;
        }

        /// <summary>View in Plan. Returns whether it opened.</summary>
        public bool Reopen()
        {
            if (!CanToggle || IsOpen)
                return false;
            Set(true);
            return true;
        }

        private void Set(bool open)
        {
            if (open == IsOpen)
                return;
            IsOpen = open;
            Changed?.Invoke(open);
        }
    }
}
