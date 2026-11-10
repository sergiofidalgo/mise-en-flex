using System;
using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Service;

namespace Game.Simulation.Days
{
    /// <summary>
    /// The last finished day, as forkcast's end-of-day summary shows it (the user's call,
    /// 2026-11-11): the summary always describes that day, whether it's just ended (Review) or the
    /// game was left and joined again (the next morning, with an empty Today). Built from the day's
    /// <see cref="DayRecord"/>, which is saved as Review starts, so a relaunch shows the same thing.
    /// </summary>
    public sealed class DaySummary
    {
        private DaySummary(int day, DayRecord record, Ledger ledger)
        {
            Day = day;
            Record = record;
            Ledger = ledger;
        }

        /// <summary>The finished day's number; 0 before any day has finished.</summary>
        public int Day { get; }

        /// <summary>The day "Start day N" begins.</summary>
        public int NextDay => Day + 1;

        /// <summary>Null before any day has finished.</summary>
        public DayRecord Record { get; }

        public bool HasDay => Record != null;

        public int Served => Record?.Served ?? 0;
        public int WalkOuts => Record?.WalkOuts ?? 0;
        public int TurnedAway => Record?.TurnedAway ?? 0;

        /// <summary>The day's money line by line. An older save kept only totals: they stand in as meals and purchases.</summary>
        public Ledger Ledger { get; }

        public IReadOnlyList<DishNight> Dishes => Record?.Dishes ?? (IReadOnlyList<DishNight>)Array.Empty<DishNight>();

        /// <summary>The last finished day of <paramref name="days"/>.</summary>
        public static DaySummary Of(DayCycle days)
        {
            var record = days.History.Count > 0 ? days.History[days.History.Count - 1] : null;
            if (record == null)
                return new DaySummary(Math.Max(0, days.Day - 1), null, new Ledger());
            return new DaySummary(record.Day, record, LedgerOf(record));
        }

        private static Ledger LedgerOf(DayRecord record)
        {
            if (record.Lines != null && record.Lines.Length > 0)
                return Ledger.FromArray(record.Lines);
            var ledger = new Ledger();
            ledger.Add(LedgerLine.Payments, record.In);
            ledger.Add(LedgerLine.Purchases, record.Out);
            return ledger;
        }

        /// <summary>
        /// The night's dishes from its guests: everyone served or who walked out, grouped by recipe
        /// (or by the order's name before the cookbook), with the price each sold at.
        /// </summary>
        public static List<DishNight> DishesOf(IEnumerable<Customer> guests, Func<string, int> priceOf)
        {
            var dishes = new List<DishNight>();
            foreach (var guest in guests)
            {
                if (guest.Order == null || (guest.Satisfaction == null && !guest.WalkedOut))
                    continue;
                var id = guest.Order.RecipeId ?? "";
                var name = Saving.SaveConversions.OrderName(guest.Order);
                var dish = dishes.FirstOrDefault(d => id.Length > 0 ? d.RecipeId == id : d.Dish == name);
                if (dish == null)
                {
                    dish = new DishNight { RecipeId = id, Dish = name, Price = id.Length > 0 && priceOf != null ? priceOf(id) : 0 };
                    dishes.Add(dish);
                }
                dish.Ordered++;
                if (guest.WalkedOut)
                    dish.WalkedOut++;
                else if (guest.RatedStars.HasValue)
                {
                    dish.StarSum += guest.RatedStars.Value;
                    dish.Rated++;
                }
            }
            return dishes;
        }
    }
}
