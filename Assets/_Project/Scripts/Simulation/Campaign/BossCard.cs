namespace Game.Simulation.Campaign
{
    /// <summary>
    /// What forkcast's boss card says (the user's call, 2026-11-10): the next collection first, how
    /// much of it the balance covers now, and what missing it brings. It turns urgent, pulsing with
    /// the boss frowning, once the money's short with two days or fewer left.
    /// </summary>
    public readonly struct BossCard
    {
        /// <summary>When the next collection falls.</summary>
        public enum When
        {
            Tonight,
            Tomorrow,
            InDays,
        }

        /// <summary>Days left at or under which a shortfall turns the card urgent.</summary>
        public const int UrgentDays = 2;

        private BossCard(float due, float balance, int daysLeft, When when, BossVisit ifMissed)
        {
            Due = due;
            Balance = balance;
            DaysLeft = daysLeft;
            Falls = when;
            IfMissed = ifMissed;
        }

        public float Due { get; }
        public float Balance { get; }

        /// <summary>Services still to run before it's taken, counting tonight's if it hasn't run.</summary>
        public int DaysLeft { get; }

        public When Falls { get; }

        /// <summary>The boss's next step if this one's missed.</summary>
        public BossVisit IfMissed { get; }

        /// <summary>How much of it the balance covers now (0..1).</summary>
        public float Covered => Due <= 0f ? 1f : System.Math.Clamp(Balance / Due, 0f, 1f);

        public float Shortfall => System.Math.Max(0f, Due - Balance);

        public bool IsShort => Shortfall > 0f;

        public bool Urgent => IsShort && DaysLeft <= UrgentDays;

        /// <param name="due">The next collection (the cut plus a loan instalment).</param>
        /// <param name="balance">The money now.</param>
        /// <param name="daysUntilFee">DayCycle.DaysUntilFee: services until it's taken, at least 1.</param>
        /// <param name="serviceStillToCome">True before today's service has run (Plan, Prep); false in Review.</param>
        /// <param name="step">How far the boss has gone.</param>
        /// <param name="restaurantsOwned">With one, his third step strips it rather than taking it.</param>
        public static BossCard For(float due, float balance, int daysUntilFee, bool serviceStillToCome, BossStep step, int restaurantsOwned)
        {
            var days = System.Math.Max(1, daysUntilFee);
            var when = days > 1 ? When.InDays : serviceStillToCome ? When.Tonight : When.Tomorrow;
            var next = (BossStep)System.Math.Min((int)BossStep.Erased, (int)step + 1);
            return new BossCard(due, balance, days, when, BossEscalation.VisitFor(next, restaurantsOwned));
        }
    }
}
