using System;
using Game.Simulation.Cooking;
using Game.Simulation.Days;
using Game.Simulation.Items;

namespace Game.Simulation.Market
{
    /// <summary>The two qualities every stall sells (decision 2026-11-10).</summary>
    public enum Grade
    {
        Cheap,
        Premium,
    }

    /// <summary>
    /// How the street's stalls price and fill a crate, the same for every stall. Premium stock costs
    /// more and starts fresher; freshness is what the quality score and spoilage already read, so
    /// "premium scores better" needs no rule of its own.
    /// </summary>
    public sealed class MarketSettings
    {
        /// <summary>Portions in one crate: a full storage slot.</summary>
        public int CratePortions { get; set; } = 6;

        /// <summary>Freshness cheap stock starts at (1 = just picked).</summary>
        public float CheapFreshness { get; set; } = 0.7f;

        public float PremiumFreshness { get; set; } = 1f;

        /// <summary>Times the ingredient's own price, per portion.</summary>
        public float CheapPriceFactor { get; set; } = 1f;

        public float PremiumPriceFactor { get; set; } = 1.6f;

        /// <summary>A crate of <paramref name="typeId"/> in one grade, from its price per portion.</summary>
        public StallOffer Offer(string typeId, float pricePerPortion, Grade grade)
        {
            var premium = grade == Grade.Premium;
            return new StallOffer(
                typeId,
                grade,
                pricePerPortion * (premium ? PremiumPriceFactor : CheapPriceFactor),
                Math.Max(1, CratePortions),
                premium ? PremiumFreshness : CheapFreshness);
        }
    }

    /// <summary>One crate on a stall's counter: what it is, its grade, and what it costs.</summary>
    public sealed class StallOffer
    {
        public StallOffer(string typeId, Grade grade, float pricePerPortion, int portions, float startFreshness)
        {
            TypeId = typeId;
            Grade = grade;
            PricePerPortion = Math.Max(0f, pricePerPortion);
            Portions = portions;
            StartFreshness = Math.Max(0f, Math.Min(1f, startFreshness));
        }

        public string TypeId { get; }

        public Grade Grade { get; }

        public float PricePerPortion { get; }

        public int Portions { get; }

        public float StartFreshness { get; }

        /// <summary>What the whole crate costs, rounded to whole money as prices are shown.</summary>
        public float CratePrice => (float)Math.Round(PricePerPortion * Portions);

        /// <summary>
        /// Buys a crate: it's paid at once into the day's ingredients line, whatever the balance
        /// (money can go below zero, decision 2026-11-08), and filled with fresh stock.
        /// </summary>
        /// <param name="create">Makes one portion, registered with the cooking simulation.</param>
        public Crate Buy(DayCycle days, Func<Ingredient> create)
        {
            var crate = new Crate(Portions);
            for (var i = 0; i < Portions; i++)
            {
                var portion = create();
                portion.SetStartingFreshness(StartFreshness);
                crate.Put(portion);
            }
            days.Charge(LedgerLine.Ingredients, CratePrice);
            return crate;
        }
    }
}
