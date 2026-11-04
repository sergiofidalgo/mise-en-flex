using System;

namespace Game.Simulation.Furniture
{
    public enum SaleProblem
    {
        None,
        NotForSale,
        NotEmpty,
        InUse,
        LastOne,
    }

    /// <summary>
    /// What can be sold, and for how much. Bad layouts are the player's to discover, but selling the
    /// last hob with no money left would end the restaurant, so that one is refused.
    /// </summary>
    public static class FurnitureRules
    {
        /// <param name="forSale">It's in the catalogue; the pass, sink and the like aren't.</param>
        /// <param name="isEmpty">Nothing on it or in it, and a hob's pan is home.</param>
        /// <param name="inUse">Someone is carrying it, or a helper is on their way to it.</param>
        /// <param name="keepOne">The kitchen can't work without one of these.</param>
        /// <param name="howMany">How many of these the kitchen has, this one included.</param>
        public static SaleProblem CheckSale(bool forSale, bool isEmpty, bool inUse, bool keepOne, int howMany)
        {
            if (!forSale)
                return SaleProblem.NotForSale;
            if (inUse)
                return SaleProblem.InUse;
            if (!isEmpty)
                return SaleProblem.NotEmpty;
            if (keepOne && howMany <= 1)
                return SaleProblem.LastOne;
            return SaleProblem.None;
        }

        /// <summary>What selling pays, to the whole unit of money, so prices stay readable.</summary>
        public static float SellPrice(float price, float fraction) =>
            (float)Math.Round(price * fraction, MidpointRounding.AwayFromZero);
    }
}
