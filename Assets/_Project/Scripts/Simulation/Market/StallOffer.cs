using System;
using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Cooking;
using Game.Simulation.Days;
using Game.Simulation.Storage;

namespace Game.Simulation.Market
{
    /// <summary>The two qualities every stall sells (decision 2026-11-10).</summary>
    public enum Grade
    {
        Cheap,
        Premium,
    }

    /// <summary>
    /// How the street's stalls price and portion what they sell, the same for every stall. Premium
    /// stock costs more and starts fresher; freshness is what the quality score and spoilage already
    /// read, so "premium scores better" needs no rule of its own.
    /// </summary>
    public sealed class MarketSettings
    {
        /// <summary>Portions one purchase brings, at most: a full storage slot.</summary>
        public int PortionsPerPurchase { get; set; } = 6;

        /// <summary>Freshness cheap stock starts at (1 = just picked).</summary>
        public float CheapFreshness { get; set; } = 0.7f;

        public float PremiumFreshness { get; set; } = 1f;

        /// <summary>Times the ingredient's own price, per portion.</summary>
        public float CheapPriceFactor { get; set; } = 1f;

        public float PremiumPriceFactor { get; set; } = 1.6f;

        /// <summary>A purchase of <paramref name="typeId"/> in one grade, from its price per portion.</summary>
        public StallOffer Offer(string typeId, float pricePerPortion, Grade grade)
        {
            var premium = grade == Grade.Premium;
            return new StallOffer(
                typeId,
                grade,
                pricePerPortion * (premium ? PremiumPriceFactor : CheapPriceFactor),
                Math.Max(1, PortionsPerPurchase),
                premium ? PremiumFreshness : CheapFreshness);
        }
    }

    /// <summary>
    /// One thing on a stall's counter: what it is, its grade and what it costs. Bought stock goes
    /// straight into the restaurant's storage (decision 2026-11-10), as much as there's room for,
    /// and only that much is paid for.
    /// </summary>
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

        /// <summary>The most one purchase brings.</summary>
        public int Portions { get; }

        public float StartFreshness { get; }

        /// <summary>What <paramref name="portions"/> cost, rounded to whole money as prices are shown.</summary>
        public float PriceFor(int portions) => (float)Math.Round(PricePerPortion * portions);

        /// <summary>How many portions a purchase would bring now: as many as the storage has room for, up to <see cref="Portions"/>.</summary>
        public int PortionsThatFit(IEnumerable<StorageUnit> storage) =>
            Math.Min(Portions, storage.Sum(unit => unit.RoomFor(TypeId)));

        /// <summary>
        /// Buys stock straight into storage, filling <paramref name="storage"/> in the order given
        /// (see <see cref="StorageOrder"/>). Paid at once into the day's ingredients line, whatever
        /// the balance (money can go below zero, decision 2026-11-08), for the portions that fitted.
        /// </summary>
        /// <param name="create">Makes one portion for a unit, at that unit's temperature, registered with the cooking simulation.</param>
        /// <returns>Where the stock went: each unit that took some, and how many. Empty if nothing fitted, and nothing was paid.</returns>
        public List<(StorageUnit Unit, int Count)> BuyInto(DayCycle days, IEnumerable<StorageUnit> storage, Func<StorageUnit, Ingredient> create)
        {
            var stocked = new List<(StorageUnit, int)>();
            var bought = 0;
            foreach (var unit in storage)
            {
                if (bought >= Portions)
                    break;
                var count = unit.Stock(TypeId, () =>
                {
                    var portion = create(unit);
                    portion.SetStartingFreshness(StartFreshness);
                    return portion;
                }, Portions - bought);
                if (count > 0)
                    stocked.Add((unit, count));
                bought += count;
            }
            if (bought > 0)
                days.Charge(LedgerLine.Ingredients, PriceFor(bought));
            return stocked;
        }
    }

    /// <summary>Which storage bought stock goes to first (decision 2026-11-10).</summary>
    public static class StorageOrder
    {
        /// <summary>
        /// Food that keeps cold goes to the fridges first and spills over onto the shelves when
        /// they're full; everything else goes to the shelves first and spills over into the
        /// fridges. Within each kind, the given order stays.
        /// </summary>
        public static List<T> For<T>(IEnumerable<T> units, Func<T, bool> isFridge, bool keepsCold) =>
            units.OrderBy(unit => isFridge(unit) == keepsCold ? 0 : 1).ToList();
    }
}
