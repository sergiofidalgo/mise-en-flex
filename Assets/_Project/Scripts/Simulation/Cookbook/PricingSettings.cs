using System;

namespace Game.Simulation.Cookbook
{
    /// <summary>
    /// How a dish's price changes who comes and how they rate it (decision 2026-11-10). Everything
    /// is in terms of r = price ÷ suggested price, so it means the same for a cheap side and a steak.
    /// Filled from the CookbookConfig asset.
    /// </summary>
    public sealed class PricingSettings
    {
        /// <summary>Lowest price a dish can be set to.</summary>
        public int MinPrice { get; set; } = 1;

        /// <summary>Highest price, as a multiple of the suggested price.</summary>
        public float MaxPriceFactor { get; set; } = 5f;

        /// <summary>
        /// How strongly price drives arrivals: demand = r^-elasticity. At 1.5, 20% over the
        /// suggestion brings about 24% fewer guests and 20% under about 40% more.
        /// </summary>
        public float DemandElasticity { get; set; } = 1.5f;

        /// <summary>Demand never falls below this, however dear: someone always comes for a look.</summary>
        public float MinDemand { get; set; } = 0.15f;

        /// <summary>Demand never rises above this, however cheap: word only travels so fast.</summary>
        public float MaxDemand { get; set; } = 1.5f;

        /// <summary>How strongly a dish's price moves it against the menu's others: weight × r^-this.</summary>
        public float ChoiceElasticity { get; set; } = 1f;

        /// <summary>Stars a guest takes off per 100% over the suggestion (and adds when under).</summary>
        public float StarsPerRatio { get; set; } = 2.5f;

        /// <summary>The most stars price can add or take off.</summary>
        public float MaxStarShift { get; set; } = 1f;
    }

    /// <summary>The formulas behind <see cref="PricingSettings"/>, plain and testable.</summary>
    public static class PriceResponse
    {
        /// <summary>Price over suggested price; 1 when there's no suggestion to compare with.</summary>
        public static float Ratio(float price, float suggested) =>
            suggested > 0f ? Math.Max(0.01f, price) / suggested : 1f;

        /// <summary>
        /// How many guests come, relative to the suggested price. A power curve so equal
        /// proportional changes have equal effects either way (double the price, half-ish as many
        /// guests), clamped so neither extreme empties or floods the room.
        /// </summary>
        public static float Demand(float ratio, PricingSettings settings) =>
            Math.Clamp((float)Math.Pow(ratio, -settings.DemandElasticity), settings.MinDemand, settings.MaxDemand);

        /// <summary>How much likelier (or less) the dish is picked from the menu.</summary>
        public static float ChoiceFactor(float ratio, PricingSettings settings) =>
            (float)Math.Pow(ratio, -settings.ChoiceElasticity);

        /// <summary>
        /// Stars added to the rating: negative when dear, positive when cheap. Linear in r, so a
        /// 20% mark-up costs half a star at the defaults; clamped so price alone never decides a review.
        /// </summary>
        public static float Expectation(float ratio, PricingSettings settings) =>
            Math.Clamp(-settings.StarsPerRatio * (ratio - 1f), -settings.MaxStarShift, settings.MaxStarShift);
    }
}
