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
        /// Game minutes since midnight: the opening hour, then one game minute for each real second
        /// of service (the clock the kitchen has always shown).
        /// </summary>
        public static float Minutes(float opensAtHour, float elapsedSeconds) => opensAtHour * 60f + elapsedSeconds / 60f;

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
        public static (string Text, bool Notice) Line(DayPhase phase, ServicePhase service, float opensAtHour, float elapsedSeconds)
        {
            var name = Phase(phase);
            if (phase == DayPhase.Plan || phase == DayPhase.Prep)
                return (Tr.Format("{phase} · opens {time}", ("phase", name), ("time", Clock(Minutes(opensAtHour, 0f)))), false);
            if (phase != DayPhase.Service)
                return (name, false);
            var time = Clock(Minutes(opensAtHour, elapsedSeconds));
            return service switch
            {
                ServicePhase.LastOrders => (Tr.Format("Last orders · {time}", ("time", time)), true),
                ServicePhase.Closed or ServicePhase.Finished => (Tr.Format("Closed · {time}", ("time", time)), true),
                _ => (Tr.Format("{phase} · {time}", ("phase", name), ("time", time)), false),
            };
        }

        /// <summary>Today's phase in the player's language.</summary>
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

        public static string Phase(DayPhase phase) => phase switch
        {
            DayPhase.Plan => Tr.In("phase", "Plan"),
            DayPhase.Prep => Tr.In("phase", "Prep"),
            DayPhase.Service => Tr.In("phase", "Service"),
            _ => Tr.In("phase", "Review"),
        };
    }
}
