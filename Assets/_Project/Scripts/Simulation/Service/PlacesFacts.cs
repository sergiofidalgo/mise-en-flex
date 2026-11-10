using System.Collections.Generic;

namespace Game.Simulation.Service
{
    /// <summary>
    /// The facts behind forkcast's listing card, after a Google Places card in the game's comic voice
    /// (the user's call, 2026-11-10): real where the game knows them (street, menu prices, how many
    /// guests reviewed), and stable made-up details where it doesn't (a house number, which joke).
    /// Stable means the same restaurant always gets the same ones.
    /// </summary>
    public static class PlacesFacts
    {
        /// <summary>A stable number for any text: the same name always gives the same number.</summary>
        public static int Seed(string text)
        {
            unchecked
            {
                var hash = 17;
                foreach (var c in text ?? "")
                    hash = hash * 31 + c;
                return hash & 0x7fffffff;
            }
        }

        /// <summary>A house number for the restaurant's street, 2 to 89.</summary>
        public static int HouseNumber(string restaurant) => 2 + Seed(restaurant) % 88;

        /// <summary>Which of <paramref name="count"/> lines this restaurant gets, for a kind of line.</summary>
        public static int Pick(string restaurant, string kind, int count) =>
            count <= 0 ? 0 : Seed(restaurant + "|" + kind) % count;

        /// <summary>
        /// What a person spends, "$15–25": the cheapest dish on the menu to the dearest, rounded out to
        /// fives, at least five apart. Null with nothing priced.
        /// </summary>
        public static (int Low, int High)? PricePerPerson(IEnumerable<float> prices)
        {
            var any = false;
            var low = float.MaxValue;
            var high = 0f;
            foreach (var price in prices)
            {
                if (price <= 0f)
                    continue;
                any = true;
                low = System.Math.Min(low, price);
                high = System.Math.Max(high, price);
            }
            if (!any)
                return null;
            var from = (int)System.Math.Floor(low / 5f) * 5;
            var to = (int)System.Math.Ceiling(high / 5f) * 5;
            if (to - from < 5)
                to = from + 5;
            return (System.Math.Max(0, from), to);
        }

        /// <summary>Of the diners who reported a price, how many were sober: about a third, the joke goes.</summary>
        public static int Sober(int reporters) => reporters <= 0 ? 0 : System.Math.Max(1, reporters / 3);
    }
}
