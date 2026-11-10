using System;
using System.Collections.Generic;

namespace Game.Simulation.Campaign
{
    /// <summary>
    /// What the accountant lends on (the user's call, 2026-11-10): amounts in steps of 250 up to
    /// 2,000 borrowed in all, a flat 20% added when borrowing, paid back in 5 instalments, one per
    /// collection. No compounding, so what's owed is always readable on the dashboard.
    /// </summary>
    public sealed class LoanSettings
    {
        /// <summary>Amounts on offer are multiples of this (and the smallest loan).</summary>
        public float Step { get; set; } = 250f;

        /// <summary>The most that can be owed in borrowed money, before the boss's 20%.</summary>
        public float MostBorrowed { get; set; } = 2000f;

        /// <summary>Added once, when borrowing: 0.2 means borrowing 1,000 owes 1,200.</summary>
        public float Interest { get; set; } = 0.2f;

        /// <summary>Collections it takes to pay back what's owed after the latest loan.</summary>
        public int Instalments { get; set; } = 5;

        /// <summary>The most that can be owed, the boss's 20% included.</summary>
        public float MostOwed => MostBorrowed * (1f + Interest);
    }

    /// <summary>The campaign's debt to the boss's accountant (decision 2026-11-10: the loan belongs to the campaign).</summary>
    public sealed class LoanAccount
    {
        public LoanAccount(float owed = 0f, float instalment = 0f)
        {
            Owed = Math.Max(0f, owed);
            Instalment = Math.Max(0f, instalment);
        }

        /// <summary>Still to be paid back, the boss's 20% included.</summary>
        public float Owed { get; private set; }

        /// <summary>
        /// What each collection takes: a fifth of what was owed right after the latest loan, so
        /// borrowing again spreads the whole debt over five more collections.
        /// </summary>
        public float Instalment { get; private set; }

        /// <summary>The instalment the next collection takes: the last one may be smaller.</summary>
        public float NextInstalment => Math.Min(Instalment, Owed);

        /// <summary>The most that can still be borrowed, a whole number of steps.</summary>
        public float MostToBorrow(LoanSettings settings)
        {
            var roomOwed = settings.MostOwed - Owed;
            var borrowable = roomOwed / (1f + settings.Interest);
            // A hair of float slack, so 2,400 owed at 20% doesn't round 2,000 down to 1,750.
            var steps = Math.Floor(borrowable / settings.Step + 1e-4);
            return (float)Math.Max(0.0, steps * settings.Step);
        }

        /// <summary>Every amount on offer now, smallest first; empty once the cap is reached.</summary>
        public IReadOnlyList<float> AmountsOnOffer(LoanSettings settings)
        {
            var amounts = new List<float>();
            var most = MostToBorrow(settings);
            for (var amount = settings.Step; amount <= most + 0.01f; amount += settings.Step)
                amounts.Add(amount);
            return amounts;
        }

        /// <summary>
        /// Borrows <paramref name="amount"/>: the caller adds it to the balance. Not refused for being
        /// unwise (players learn by playing), only for breaking the terms.
        /// </summary>
        /// <returns>False, changing nothing, if it isn't a positive whole number of steps or goes over the cap.</returns>
        public bool Borrow(float amount, LoanSettings settings)
        {
            if (amount <= 0f || amount > MostToBorrow(settings) + 0.01f)
                return false;
            var steps = amount / settings.Step;
            if (Math.Abs(steps - Math.Round(steps)) > 1e-3)
                return false;
            Owed += Whole(amount * (1f + settings.Interest));
            // Rounded up, so five instalments always cover the debt.
            Instalment = (float)Math.Ceiling(Owed / Math.Max(1, settings.Instalments) - 1e-3);
            return true;
        }

        /// <summary>Takes one instalment off what's owed, as a collection is worked out.</summary>
        /// <returns>The instalment taken.</returns>
        public float TakeInstalment()
        {
            var taken = NextInstalment;
            Owed = Math.Max(0f, Owed - taken);
            if (Owed <= 0f)
                Instalment = 0f;
            return taken;
        }

        private static float Whole(float amount) => (float)Math.Round(amount, MidpointRounding.AwayFromZero);
    }
}
