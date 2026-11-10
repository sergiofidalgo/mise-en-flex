using System;

namespace Game.Simulation.Campaign
{
    /// <summary>How far the boss has gone (§15.3): one step per missed collection, reset by paying in full.</summary>
    public enum BossStep
    {
        None = 0,
        Warned = 1,
        Repossessed = 2,
        RestaurantTaken = 3,
        Erased = 4,
    }

    /// <summary>What the boss does in person, the morning after a missed collection (§15.3).</summary>
    public enum BossVisit
    {
        None = 0,

        /// <summary>The boss and his two bodyguards come in, he warns them, and they leave.</summary>
        Warning = 1,

        /// <summary>The bodyguards carry out appliances worth at least the shortfall, credited at resale value.</summary>
        Repossession = 2,

        /// <summary>Step 3 with only one restaurant (the user's call, 2026-11-10): stripped to the bare minimum, and a last warning.</summary>
        StripToMinimum = 3,

        /// <summary>Step 3 with more than one restaurant: he takes the one that missed the collection.</summary>
        TakeRestaurant = 4,

        /// <summary>The campaign ends: money and restaurants go, the cookbook stays, a dusty portrait goes up.</summary>
        Erasure = 5,
    }

    /// <summary>
    /// The escalation state machine (§15.3, decision 2026-11-10). Each missed collection moves it one
    /// step and books a visit for the next morning; paying in full resets it to nothing, and cancels
    /// a visit not yet made. Saved with the campaign, so leaving doesn't dodge him.
    /// </summary>
    public sealed class BossEscalation
    {
        public BossEscalation(BossStep step = BossStep.None, BossVisit pending = BossVisit.None, string pendingAt = "")
        {
            Step = step;
            Pending = pending;
            PendingAt = pendingAt ?? "";
        }

        public BossStep Step { get; private set; }

        /// <summary>What he'll do the next morning there's a day; None if nothing.</summary>
        public BossVisit Pending { get; private set; }

        /// <summary>The definition id of the restaurant whose collection was missed: where he comes.</summary>
        public string PendingAt { get; private set; }

        public bool IsErased => Step == BossStep.Erased;

        /// <summary>Records a collection as it's worked out.</summary>
        /// <param name="restaurantsOwned">How many the campaign owns now: with one, step 3 can't take it.</param>
        /// <param name="restaurant">The definition id of the restaurant whose collection it was.</param>
        /// <returns>The visit booked for the next morning, or None if it was paid in full.</returns>
        public BossVisit Record(Collection collection, int restaurantsOwned, string restaurant)
        {
            if (IsErased)
                return BossVisit.Erasure;
            if (collection.PaidInFull)
            {
                Step = BossStep.None;
                Pending = BossVisit.None;
                PendingAt = "";
                return BossVisit.None;
            }

            Step = (BossStep)Math.Min((int)BossStep.Erased, (int)Step + 1);
            Pending = VisitFor(Step, restaurantsOwned);
            PendingAt = restaurant ?? "";
            return Pending;
        }

        /// <summary>The visit has been made: nothing pending until the next missed collection.</summary>
        public void Visited()
        {
            Pending = BossVisit.None;
            PendingAt = "";
        }

        /// <summary>What reaching <paramref name="step"/> brings.</summary>
        public static BossVisit VisitFor(BossStep step, int restaurantsOwned)
        {
            switch (step)
            {
                case BossStep.Warned: return BossVisit.Warning;
                case BossStep.Repossessed: return BossVisit.Repossession;
                case BossStep.RestaurantTaken: return restaurantsOwned > 1 ? BossVisit.TakeRestaurant : BossVisit.StripToMinimum;
                case BossStep.Erased: return BossVisit.Erasure;
                default: return BossVisit.None;
            }
        }
    }
}
