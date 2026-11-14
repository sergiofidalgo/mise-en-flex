namespace Game.Simulation.Days
{
    /// <summary>
    /// When a new player may join a kitchen (the user's calls, 2026-11-13 and 2026-11-14): in Prep,
    /// as in the office; never on the dashboard (Plan, Review) or during service, where they're
    /// told to wait instead. A kitchen nobody is playing yet takes its first player in any phase,
    /// so the dashboard's "Press to join" can't leave it stuck.
    /// </summary>
    public static class JoinRules
    {
        public static bool Allows(DayPhase phase, int playersIn = 1) => playersIn == 0 || phase == DayPhase.Prep;

        /// <summary>What someone pressing A to join is told when they can't yet, in English (the HUD translates it).</summary>
        public static string WaitMessage(DayPhase phase) => phase == DayPhase.Service
            ? "Wait for this service to finish to join"
            : "Join when Prep starts";
    }
}
