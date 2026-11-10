namespace Game.Simulation.Service
{
    public enum CustomerIndicatorKind
    {
        /// <summary>Someone's there: walking in, or waiting for a seat.</summary>
        Someone,
        /// <summary>Sitting and waiting for food: shows how long they'll wait.</summary>
        WaitingForFood,
        Eating,
        /// <summary>On their way out: shows how it went, briefly.</summary>
        Leaving,
    }

    public enum CustomerMood
    {
        Happy,
        Neutral,
        Unhappy,
    }

    /// <summary>
    /// What the marker at the edge of the screen says about a customer out of view (decision
    /// 2026-11-07). The camera follows the players and staff, so customers can be off screen.
    /// </summary>
    public static class CustomerIndicator
    {
        public static CustomerIndicatorKind KindOf(Customer customer)
        {
            switch (customer.State)
            {
                case CustomerState.Waiting:
                    return CustomerIndicatorKind.WaitingForFood;
                case CustomerState.Eating:
                case CustomerState.DoneEating:
                    return CustomerIndicatorKind.Eating;
                case CustomerState.Leaving:
                case CustomerState.Gone:
                    return CustomerIndicatorKind.Leaving;
                default:
                    return CustomerIndicatorKind.Someone;
            }
        }

        /// <summary>Going by how they rated their meal; anyone who walked out or was turned away isn't happy.</summary>
        /// <param name="happyFrom">Stars from which they leave happy.</param>
        /// <param name="neutralFrom">Stars from which they leave neutral.</param>
        public static CustomerMood MoodOf(Customer customer, float happyFrom, float neutralFrom)
        {
            if (customer.WalkedOut || customer.Satisfaction == null)
                return CustomerMood.Unhappy;
            if (customer.RatedStars >= happyFrom)
                return CustomerMood.Happy;
            return customer.RatedStars >= neutralFrom ? CustomerMood.Neutral : CustomerMood.Unhappy;
        }
    }
}
