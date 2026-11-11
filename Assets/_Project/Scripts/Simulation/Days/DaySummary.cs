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

        /// <summary>False for a day saved before the books kept every line (and its dishes): only totals.</summary>
        public bool HasLines => Record?.Lines != null && Record.Lines.Length > 0;

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
        /// The meal ordered most (the user's card, 2026-11-11), counted by dish rather than by how
        /// it was cooked: "steak, rare" and "steak, medium" are both steak. Ties go alphabetically.
        /// Returns the recipe id when the orders had one, otherwise the dish's own name ("steak"),
        /// and null with nothing ordered.
        /// </summary>
        public static string MostOrdered(IEnumerable<DishNight> dishes)
        {
            var counts = new Dictionary<string, int>();
            foreach (var dish in dishes)
            {
                var key = !string.IsNullOrEmpty(dish.RecipeId) ? dish.RecipeId : MainOf(dish.Dish);
                if (key.Length == 0)
                    continue;
                counts.TryGetValue(key, out var count);
                counts[key] = count + dish.Ordered;
            }
            return counts.Count == 0 ? null : counts.OrderByDescending(c => c.Value).ThenBy(c => c.Key, StringComparer.Ordinal).First().Key;
        }

        /// <summary>How generous the guests were, for the tips card's caption (the user's question, 2026-11-11).</summary>
        public enum TipMood
        {
            None,
            Polite,
            Liked,
            Loved,
        }

        /// <summary>Tips under this share of the meals are merely polite.</summary>
        public const float PoliteTipShare = 0.05f;

        /// <summary>Tips over this share of the meals mean they loved it.</summary>
        public const float LovedTipShare = 0.15f;

        /// <summary>
        /// The night's tips against what the meals took: none, polite (under 5%), liked (5–15%),
        /// loved (over 15%). Tips with no meal takings count as loved: someone tipped for nothing.
        /// </summary>
        public static TipMood TipsMood(float tips, float meals)
        {
            if (tips <= 0.005f)
                return TipMood.None;
            if (meals <= 0f)
                return TipMood.Loved;
            var share = tips / meals;
            return share < PoliteTipShare ? TipMood.Polite : share > LovedTipShare ? TipMood.Loved : TipMood.Liked;
        }

        /// <summary>"steak, medium" → "steak": the dish before how it was cooked.</summary>
        public static string MainOf(string order)
        {
            if (string.IsNullOrEmpty(order))
                return "";
            var comma = order.IndexOf(',');
            return (comma >= 0 ? order.Substring(0, comma) : order).Trim();
        }

        /// <summary>
        /// The night's dishes from its guests: everyone served or who walked out, grouped by recipe
        /// (or by the order's name before the cookbook), with the price each sold at.
        /// </summary>
        public static List<DishNight> DishesOf(IEnumerable<Customer> guests, Func<string, float> priceOf)
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
                    // What the guest actually paid for it, when they did; otherwise the menu's price.
                    var paid = guest.Bill.HasValue ? (float)Math.Round(guest.Bill.Value.Payment, 2) : 0f;
                    dish = new DishNight { RecipeId = id, Dish = name, Price = paid > 0 ? paid : id.Length > 0 && priceOf != null ? priceOf(id) : 0 };
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
