using System;
using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Cooking;
using Game.Simulation.Days;
using Game.Simulation.Storage;

namespace Game.Simulation.Market
{
    /// <summary>
    /// One ingredient on a stall's counter, at its price per portion. Each press buys one portion
    /// (the user's call, 2026-11-11), brand new and fully fresh, and it goes straight into the
    /// restaurant's storage (decision 2026-11-10). With no room anywhere, nothing is bought.
    /// </summary>
    public sealed class StallOffer
    {
        public StallOffer(string typeId, float price)
        {
            TypeId = typeId;
            Price = Math.Max(0f, price);
        }

        public string TypeId { get; }

        /// <summary>What one portion costs.</summary>
        public float Price { get; }

        /// <summary>True if any of <paramref name="storage"/> has room for one more portion.</summary>
        public bool Fits(IEnumerable<StorageUnit> storage) => storage.Any(unit => unit.RoomFor(TypeId) > 0);

        /// <summary>
        /// Buys one portion into the first of <paramref name="storage"/> with room for it (see
        /// <see cref="StorageOrder"/>), paid at once into the day's ingredients line, whatever the
        /// balance (money can go below zero, decision 2026-11-08).
        /// </summary>
        /// <param name="create">Makes the portion for a unit, at that unit's temperature, registered with the cooking simulation.</param>
        /// <returns>The unit it went into, or null if none had room: then nothing was paid.</returns>
        public StorageUnit BuyInto(DayCycle days, IEnumerable<StorageUnit> storage, Func<StorageUnit, Ingredient> create)
        {
            foreach (var unit in storage)
            {
                if (unit.Stock(TypeId, () => create(unit), 1) == 0)
                    continue;
                days.Charge(LedgerLine.Ingredients, Price);
                return unit;
            }
            return null;
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
