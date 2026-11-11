using Game.Data;
using Game.Simulation.Days;
using Game.Simulation.Service;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// What the kitchen's day card says (decision 2026-11-10): "Day 3" on top, and under it today's
    /// phase with the game clock, "Service · 18:42". Before service the clock reads the opening
    /// time; near the end the phase word gives way to "Last orders" or "Closed", shown as a notice.
    /// Plain C#, tested without Play mode.
    /// </summary>
    public static class DayCardText
    {
        /// <summary>The top line. <see cref="DayCycle.Day"/> already counts from 1.</summary>
        public static string Title(int day) => Tr.Format("Day {day}", ("day", day));

        /// <summary>
        /// Minutes since midnight on the service clock: the opening hour, then an hour for every
        /// <paramref name="secondsPerHour"/> in-game seconds of service (1800 since 2026-11-11: an
        /// hour in 30 real seconds; 3600, the default, is the old clock).
        /// </summary>
        public static float Minutes(float opensAtHour, float elapsedSeconds, float secondsPerHour = 3600f) =>
            opensAtHour * 60f + elapsedSeconds / System.Math.Max(1f, secondsPerHour) * 60f;

        /// <summary>A 24-hour clock, "18:05", wrapping past midnight.</summary>
        public static string Clock(float minutes)
        {
            var whole = (int)System.Math.Floor(minutes);
            var hours = ((whole / 60) % 24 + 24) % 24;
            var mins = ((whole % 60) + 60) % 60;
            return $"{hours:00}:{mins:00}";
        }

        /// <summary>
        /// The second line, and whether it's a notice (Last orders, Closed) to show in the notice
        /// colour.
        /// </summary>
        public static (string Text, bool Notice) Line(DayPhase phase, ServicePhase service, float opensAtHour, float elapsedSeconds, float secondsPerHour = 3600f)
        {
            var name = Phase(phase);
            if (phase == DayPhase.Plan || phase == DayPhase.Prep)
                return (Tr.Format("{phase} · opens {time}", ("phase", name), ("time", Clock(Minutes(opensAtHour, 0f)))), false);
            if (phase != DayPhase.Service)
                return (name, false);
            var time = Clock(Minutes(opensAtHour, elapsedSeconds, secondsPerHour));
            return service switch
            {
                ServicePhase.LastOrders => (Tr.Format("Last orders · {time}", ("time", time)), true),
                ServicePhase.Closed or ServicePhase.Finished => (Tr.Format("Closed · {time}", ("time", time)), true),
                _ => (Tr.Format("{phase} · {time}", ("phase", name), ("time", time)), false),
            };
        }

        /// <summary>
        /// Prep's guests line (the user's call, 2026-11-11): "About 5 guests", the forecast's
        /// whole number.
        /// </summary>
        public static string Guests(int count) => Tr.Plural(count, "About {n} guest", "About {n} guests");

        /// <summary>When the doors open, under "Prep": "Opens 18:00".</summary>
        public static string Opens(float opensAtHour) => Tr.Format("Opens {time}", ("time", Clock(Minutes(opensAtHour, 0f))));

        /// <summary>
        /// The average group size the tables bring (GuestForecast.GroupSize), in words: "Groups of
        /// one", "Groups of one or two", up to "Groups of three"; bigger tables read "Groups of about
        /// N". Halfway between two sizes names both, so a mixed room doesn't read as all pairs.
        /// </summary>
        public static string Groups(float average)
        {
            if (average < 1.3f)
                return Tr.Get("Groups of one");
            if (average < 1.7f)
                return Tr.Get("Groups of one or two");
            if (average < 2.3f)
                return Tr.Get("Groups of two");
            if (average < 2.7f)
                return Tr.Get("Groups of two or three");
            if (average < 3.3f)
                return Tr.Get("Groups of three");
            return Tr.Format("Groups of about {count}", ("count", (int)System.Math.Round(average)));
        }

        /// <summary>The day's weekday, short: day 1 is a Monday.</summary>
        public static string Weekday(int day) => (((day - 1) % 7 + 7) % 7) switch
        {
            0 => Tr.Get("Mon"),
            1 => Tr.Get("Tue"),
            2 => Tr.Get("Wed"),
            3 => Tr.Get("Thu"),
            4 => Tr.Get("Fri"),
            5 => Tr.Get("Sat"),
            _ => Tr.Get("Sun"),
        };

        /// <summary>Today's phase in the player's language.</summary>
        public static string Phase(DayPhase phase) => phase switch
        {
            DayPhase.Plan => Tr.In("phase", "Plan"),
            DayPhase.Prep => Tr.In("phase", "Prep"),
            DayPhase.Service => Tr.In("phase", "Service"),
            _ => Tr.In("phase", "Review"),
        };
    }
}
