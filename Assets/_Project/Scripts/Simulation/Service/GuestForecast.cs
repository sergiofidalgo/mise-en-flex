using System;
using System.Collections.Generic;

namespace Game.Simulation.Service
{
    /// <summary>
    /// How many guests to expect tonight, shown while the kitchen preps (the user's call, 2026-11-10).
    /// It's the service's own arrival rule, averaged: groups arrive every ArrivalIntervalSeconds
    /// (divided by the menu's demand, and by RushMultiplier during a rush) until last orders, with
    /// jitter that averages out; each group's size follows the tables (PartyMix). So it moves as
    /// prices change or tables are added, as the real night would.
    /// </summary>
    public static class GuestForecast
    {
        /// <summary>Groups expected to walk in before last orders.</summary>
        public static float Groups(ServiceSettings settings, float demand)
        {
            var open = Math.Max(0f, settings.DurationSeconds - settings.LastOrdersSeconds);
            if (open <= 0f)
                return 0f;
            var interval = settings.ArrivalIntervalSeconds / Math.Max(0.01f, demand);
            if (interval <= 0f)
                return 0f;
            var rush = RushSeconds(settings, open);
            var calm = open - rush;
            return calm / interval + rush * Math.Max(0.01f, settings.RushMultiplier) / interval;
        }

        // How much of the service before last orders (open seconds) is rush: the rush hours on the
        // clock since the opening times (2026-11-11), otherwise the old single rush by fractions.
        private static float RushSeconds(ServiceSettings settings, float open)
        {
            if (settings.RushHours == null || settings.RushHours.Length == 0)
            {
                var rushFrom = Math.Min(open, settings.RushStart * settings.DurationSeconds);
                var rushTo = Math.Min(open, settings.RushEnd * settings.DurationSeconds);
                return Math.Max(0f, rushTo - rushFrom);
            }
            var clock = settings.Clock ?? new ServiceClock();
            var windows = new List<(float From, float To)>();
            foreach (var peak in settings.RushHours)
            {
                var from = Math.Max(0f, (peak - settings.RushHalfWidthHours - clock.OpensAtHour) * clock.SecondsPerHour);
                var to = Math.Min(open, (peak + settings.RushHalfWidthHours - clock.OpensAtHour) * clock.SecondsPerHour);
                if (to > from)
                    windows.Add((from, to));
            }
            // Overlapping rushes count once.
            windows.Sort((a, b) => a.From.CompareTo(b.From));
            var total = 0f;
            var end = 0f;
            foreach (var (from, to) in windows)
            {
                var start = Math.Max(from, end);
                if (to > start)
                    total += to - start;
                end = Math.Max(end, to);
            }
            return total;
        }

        /// <summary>The average group size these tables bring (PartyMix); 1 with no tables.</summary>
        public static float GroupSize(IReadOnlyList<int> tables)
        {
            var biggest = 1;
            foreach (var chairs in tables)
                biggest = Math.Max(biggest, chairs);
            if (tables.Count == 0 || biggest <= 1)
                return 1f;
            var mean = 0f;
            var left = 1f;
            for (var size = 1; size < biggest; size++)
            {
                var chance = PartyMix.Chance(size, tables);
                mean += size * chance;
                left -= chance;
            }
            return mean + biggest * Math.Max(0f, left);
        }

        /// <summary>Guests expected tonight, rounded to a whole number to show.</summary>
        public static int Guests(ServiceSettings settings, float demand, IReadOnlyList<int> tables) =>
            (int)Math.Round(Groups(settings, demand) * GroupSize(tables));
    }
}
