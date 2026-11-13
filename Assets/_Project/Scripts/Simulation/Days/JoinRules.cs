namespace Game.Simulation.Days
{
    /// <summary>
    /// When a new player may join a kitchen (the user's call, 2026-11-13): in Plan and Prep. During
    /// service and while the Review is up, they're told to wait until it's over instead.
    /// </summary>
    public static class JoinRules
    {
        public static bool Allows(DayPhase phase) => phase == DayPhase.Plan || phase == DayPhase.Prep;

        /// <summary>What someone pressing A to join is told when they can't yet, in English (the HUD translates it).</summary>
        public static string WaitMessage(DayPhase phase) => phase == DayPhase.Review
            ? "Join when the next day starts"
            : "Wait for this service to finish to join";
    }
}
