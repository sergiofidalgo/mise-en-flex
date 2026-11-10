using System.Collections.Generic;

namespace Game.Simulation.Campaign
{
    /// <summary>Why the boss rings the personal mobile (§15.6).</summary>
    public enum BossCallKind
    {
        None,

        /// <summary>The collection falls due at the end of today, and the money isn't there yet.</summary>
        Near,

        /// <summary>Just paid in full.</summary>
        Paid,

        /// <summary>Paid in full after falling behind: the escalation is off.</summary>
        BackInGoodBooks,

        /// <summary>Missed: what's coming tomorrow morning depends on the step.</summary>
        Missed,
    }

    /// <summary>
    /// What the boss says on the phone: a few short lines, in English, translated where they're
    /// shown. Comic menace (the user's call, 2026-11-10): a Texas oilman who likes you, as long as
    /// you pay.
    /// </summary>
    public static class BossCalls
    {
        /// <summary>
        /// Whether to ring the morning of a collection day: it falls due as today's Review starts,
        /// and the balance doesn't cover it yet. Players can still earn it in today's service.
        /// </summary>
        public static bool ShouldWarnNear(int daysUntilCollection, Collection forecast) =>
            daysUntilCollection <= 1 && !forecast.PaidInFull;

        /// <summary>The call a collection brings, as Review starts.</summary>
        public static BossCallKind After(Collection collection, BossStep stepBefore) =>
            collection.PaidInFull
                ? (stepBefore != BossStep.None ? BossCallKind.BackInGoodBooks : BossCallKind.Paid)
                : BossCallKind.Missed;

        /// <summary>The call's lines. <paramref name="visit"/> says what a missed collection brings.</summary>
        public static IReadOnlyList<string> Lines(BossCallKind kind, BossVisit visit = BossVisit.None)
        {
            switch (kind)
            {
                case BossCallKind.Near:
                    return new[] { "Partner! Collection's tonight.", "I'd hate to see that till come up short." };
                case BossCallKind.Paid:
                    return new[] { "Money's in. That's my kind of chef.", "Keep those pans hot, partner." };
                case BossCallKind.BackInGoodBooks:
                    return new[] { "Well, look at that. Paid in full.", "We're square. The boys can stand down." };
                case BossCallKind.Missed:
                    switch (visit)
                    {
                        case BossVisit.Warning:
                            return new[] { "You came up short, partner.", "I'll swing by in the morning. Just to talk." };
                        case BossVisit.Repossession:
                            return new[] { "Short again? Now that hurts my feelings.", "The boys'll pick up some kitchenware tomorrow." };
                        case BossVisit.StripToMinimum:
                            return new[] { "Three times, partner. Three.", "Tomorrow the boys take everything you can live without.", "Next time, I take the rest." };
                        case BossVisit.TakeRestaurant:
                            return new[] { "Three times, partner. Three.", "Come morning, this place is mine." };
                        case BossVisit.Erasure:
                            return new[] { "I'm done waitin', chef.", "Tomorrow, you're out. All of it." };
                    }
                    break;
            }
            return new string[0];
        }

        /// <summary>Every line he can say, for the language tables.</summary>
        public static IEnumerable<string> AllLines()
        {
            var kinds = new[] { BossCallKind.Near, BossCallKind.Paid, BossCallKind.BackInGoodBooks, BossCallKind.Missed };
            var visits = (BossVisit[])System.Enum.GetValues(typeof(BossVisit));
            foreach (var kind in kinds)
                foreach (var visit in visits)
                    foreach (var line in Lines(kind, visit))
                        yield return line;
            foreach (var visit in visits)
                foreach (var line in VisitLines(visit))
                    yield return line;
            foreach (var line in Asides)
                yield return line;
        }

        /// <summary>What he says when a visit turns out differently: the debt cleared overnight, a restaurant elsewhere taken.</summary>
        public static readonly string[] Asides =
        {
            "Huh. Found the money, did ya?", "Lucky you.", "Next time, I take the rest.", "Not this one, mind. Yet.",
        };

        /// <summary>What the boss says in person, the morning after.</summary>
        public static IReadOnlyList<string> VisitLines(BossVisit visit)
        {
            switch (visit)
            {
                case BossVisit.Warning:
                    return new[] { "Nice little place you got here.", "Be a shame if it stopped payin'.", "Next time, the boys bring a dolly." };
                case BossVisit.Repossession:
                    return new[] { "Don't mind us.", "Just takin' what's mine." };
                case BossVisit.StripToMinimum:
                    return new[] { "One hob's plenty for a chef like you.", "Last warning, partner." };
                case BossVisit.TakeRestaurant:
                    return new[] { "Keys, please.", "This one's under new management." };
                case BossVisit.Erasure:
                    return new[] { "That's all, folks.", "Your cookbook stays. Somebody'll want it." };
            }
            return new string[0];
        }
    }
}
