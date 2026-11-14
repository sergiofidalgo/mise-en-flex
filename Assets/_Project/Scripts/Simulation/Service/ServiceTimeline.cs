using System;
using System.Collections.Generic;

namespace Game.Simulation.Service
{
    /// <summary>A stretch of the service as fractions of it, 0 at opening to 1 at closing.</summary>
    public readonly struct TimelineSpan
    {
        public TimelineSpan(float from, float to, float peakHour)
        {
            From = from;
            To = to;
            PeakHour = peakHour;
        }

        public float From { get; }
        public float To { get; }

        /// <summary>For a rush, the hour on the clock it peaks at.</summary>
        public float PeakHour { get; }
    }

    /// <summary>One hour mark on the timeline.</summary>
    public readonly struct TimelineHour
    {
        public TimelineHour(float fraction, int hour, bool labelled)
        {
            Fraction = fraction;
            Hour = hour;
            Labelled = labelled;
        }

        public float Fraction { get; }

        /// <summary>The hour on a 24-hour clock face (midnight is 0).</summary>
        public int Hour { get; }

        /// <summary>Whether it carries its number: every hour on a short service, every other on a long one.</summary>
        public bool Labelled { get; }
    }

    /// <summary>
    /// The service laid out from opening to closing (the user's pick, 2026-11-14): where the rushes
    /// fall, where last orders start and how far the clock has got, all as fractions of the
    /// service, for the day card's timeline to draw.
    /// </summary>
    public static class ServiceTimeline
    {
        /// <summary>Services longer than this many hours label every other hour, so the numbers don't crowd.</summary>
        public const float EveryHourUpTo = 6f;

        public static float OpensAt(ServiceSettings settings) => Clock(settings).OpensAtHour;

        public static float ClosesAt(ServiceSettings settings) => OpensAt(settings) + Hours(settings);

        public static float Hours(ServiceSettings settings) => settings.DurationSeconds / Clock(settings).SecondsPerHour;

        /// <summary>How far through the service <paramref name="elapsed"/> in-game seconds is, 0 to 1.</summary>
        public static float Fraction(ServiceSettings settings, float elapsed) =>
            settings.DurationSeconds > 0f ? Clamp01(elapsed / settings.DurationSeconds) : 1f;

        /// <summary>Where last orders start, as a fraction of the service.</summary>
        public static float LastOrdersFrom(ServiceSettings settings) =>
            settings.DurationSeconds > 0f ? Clamp01(1f - settings.LastOrdersSeconds / settings.DurationSeconds) : 1f;

        /// <summary>
        /// The rushes inside the service, clipped to it. With no rush hours set, the old single rush
        /// from RushStart to RushEnd, if it brings anyone more often.
        /// </summary>
        public static List<TimelineSpan> Rushes(ServiceSettings settings)
        {
            var spans = new List<TimelineSpan>();
            var opens = OpensAt(settings);
            var hours = Hours(settings);
            if (hours <= 0f)
                return spans;
            if (settings.RushHours != null && settings.RushHours.Length > 0)
            {
                foreach (var peak in settings.RushHours)
                {
                    var from = Clamp01((peak - settings.RushHalfWidthHours - opens) / hours);
                    var to = Clamp01((peak + settings.RushHalfWidthHours - opens) / hours);
                    if (to > from)
                        spans.Add(new TimelineSpan(from, to, peak));
                }
                return spans;
            }
            if (settings.RushMultiplier > 1f && settings.RushEnd > settings.RushStart)
            {
                var from = Clamp01(settings.RushStart);
                var to = Clamp01(settings.RushEnd);
                if (to > from)
                    spans.Add(new TimelineSpan(from, to, opens + hours * (from + to) / 2f));
            }
            return spans;
        }

        /// <summary>The rush <paramref name="elapsed"/> falls in, its index in <see cref="Rushes"/>, or -1.</summary>
        public static int RushAt(ServiceSettings settings, float elapsed)
        {
            var at = Fraction(settings, elapsed);
            var rushes = Rushes(settings);
            for (var i = 0; i < rushes.Count; i++)
            {
                if (at >= rushes[i].From && at < rushes[i].To)
                    return i;
            }
            return -1;
        }

        /// <summary>The whole hours from opening to closing, each with its place on the timeline.</summary>
        public static List<TimelineHour> HourMarks(ServiceSettings settings)
        {
            var marks = new List<TimelineHour>();
            var opens = OpensAt(settings);
            var hours = Hours(settings);
            if (hours <= 0f)
                return marks;
            var every = hours <= EveryHourUpTo ? 1 : 2;
            for (var hour = (int)Math.Ceiling(opens - 1e-4f); hour <= opens + hours + 1e-4f; hour++)
                marks.Add(new TimelineHour(Clamp01((hour - opens) / hours), ((hour % 24) + 24) % 24, hour % every == 0));
            return marks;
        }

        /// <summary>The opening slot this service runs to (its opening hour and length), or null if none matches.</summary>
        public static OpeningSlot SlotOf(OpeningTimes opening, ServiceSettings settings)
        {
            if (opening == null)
                return null;
            var opens = OpensAt(settings);
            var hours = Hours(settings);
            foreach (var slot in opening.Slots)
            {
                if (Math.Abs(slot.Opens - opens) < 0.01f && Math.Abs(slot.Hours - hours) < 0.01f)
                    return slot;
            }
            return null;
        }

        /// <summary>Whether a rush peaking at <paramref name="peakHour"/> is the lunch one (before 17:00) or dinner's.</summary>
        public static bool IsLunchRush(float peakHour) => peakHour % 24f < 17f;

        private static ServiceClock Clock(ServiceSettings settings) => settings.Clock ?? new ServiceClock();

        private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
    }
}
