using System;
using System.Numerics;

namespace Game.Simulation.Street
{
    /// <summary>
    /// The light of the day by the clock (the user's request, 2026-11-12: the opening times players
    /// choose should show in the scene, "day, night, or both", with the light changing through a
    /// full day). What the sun and sky give outdoors at each hour, as a colour everything out there
    /// is lit by: white through the day, gold towards evening, mauve at dusk, a deep blue at night.
    /// Indoors the restaurant has its own lights, so it only warms a little as night falls and
    /// never goes dark: the kitchen must read the same at any hour.
    /// </summary>
    public static class Daylight
    {
        public static readonly Vector3 Day = new Vector3(1f, 1f, 1f);
        public static readonly Vector3 Dawn = new Vector3(0.95f, 0.8f, 0.72f);
        public static readonly Vector3 Golden = new Vector3(1f, 0.84f, 0.66f);
        public static readonly Vector3 Dusk = new Vector3(0.66f, 0.54f, 0.68f);
        public static readonly Vector3 Night = new Vector3(0.27f, 0.33f, 0.54f);
        /// <summary>The restaurant's own light at night: its lamps, a little warmer than daylight.</summary>
        public static readonly Vector3 IndoorsAtNight = new Vector3(1f, 0.93f, 0.84f);

        // The hours the light turns at. Between two of them it changes evenly.
        private static readonly (float Hour, Vector3 Light)[] Keys =
        {
            (0f, Night), (5.5f, Night), (7f, Dawn), (8.5f, Day), (16.5f, Day), (18.25f, Golden), (19.5f, Dusk), (20.75f, Night), (24f, Night),
        };

        /// <summary>The light outdoors at <paramref name="hour"/> on the clock (which may pass 24).</summary>
        public static Vector3 At(float hour)
        {
            var onClock = hour - 24f * (float)Math.Floor(hour / 24f);
            for (var i = 1; i < Keys.Length; i++)
            {
                if (onClock > Keys[i].Hour)
                    continue;
                var from = Keys[i - 1];
                var to = Keys[i];
                var t = to.Hour > from.Hour ? (onClock - from.Hour) / (to.Hour - from.Hour) : 1f;
                return Vector3.Lerp(from.Light, to.Light, t);
            }
            return Night;
        }

        /// <summary>How far into night it is, 0 in full daylight to 1 in the dead of night, by how bright the light is.</summary>
        public static float Nightfall(float hour)
        {
            var bright = Brightness(At(hour));
            var darkest = Brightness(Night);
            return Math.Max(0f, Math.Min(1f, (1f - bright) / (1f - darkest)));
        }

        /// <summary>The light inside the restaurant at <paramref name="hour"/>: daylight by day, its own lamps by night.</summary>
        public static Vector3 Indoors(float hour) => Vector3.Lerp(Day, IndoorsAtNight, Nightfall(hour));

        private static float Brightness(Vector3 light) => (light.X + light.Y + light.Z) / 3f;
    }
}
