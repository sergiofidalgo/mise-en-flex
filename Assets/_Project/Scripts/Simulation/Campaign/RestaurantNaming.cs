using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Game.Simulation.Campaign
{
    /// <summary>Restaurant names: tidying what was typed, and suggesting a few to pick from.</summary>
    public static class RestaurantNaming
    {
        /// <summary>
        /// Leading and trailing spaces go, and runs of spaces become one. Null becomes empty.
        /// </summary>
        public static string Clean(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";
            var text = new StringBuilder(name.Length);
            var space = false;
            foreach (var c in name.Trim())
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!space)
                        text.Append(' ');
                    space = true;
                    continue;
                }
                space = false;
                text.Append(c);
            }
            return text.ToString();
        }

        /// <summary>
        /// <paramref name="count"/> names from <paramref name="pool"/>, in an order set by
        /// <paramref name="seed"/> (so a reroll gives others), leaving out any in
        /// <paramref name="taken"/> (the lot's old name, restaurants already owned).
        /// </summary>
        public static IReadOnlyList<string> Suggest(IEnumerable<string> pool, int seed, int count, IEnumerable<string> taken = null)
        {
            var excluded = new HashSet<string>((taken ?? Enumerable.Empty<string>()).Select(Clean), StringComparer.OrdinalIgnoreCase);
            var names = (pool ?? Enumerable.Empty<string>()).Select(Clean)
                .Where(n => n.Length > 0 && !excluded.Contains(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            // Fisher-Yates with a seeded random, so the same seed always gives the same names.
            var random = new Random(seed);
            for (var i = names.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (names[i], names[j]) = (names[j], names[i]);
            }
            return names.Take(Math.Max(0, count)).ToList();
        }
    }
}
