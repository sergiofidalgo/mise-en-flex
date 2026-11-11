using System;
using System.Collections.Generic;

namespace Game.Simulation.Service
{
    /// <summary>
    /// The clock on the wall during a service: when the doors opened, and how many in-game seconds
    /// make an hour on it. Since the user's call of 2026-11-11 an hour passes in 30 real seconds
    /// (1800 in-game seconds at the usual 60× time scale), while cooking, patience and eating keep
    /// their real-time feel: only the clock runs faster. 3600 is the old one-to-one clock.
    /// </summary>
    public sealed class ServiceClock
    {
        public ServiceClock(float opensAtHour = 18f, float secondsPerHour = 3600f)
        {
            OpensAtHour = opensAtHour;
            SecondsPerHour = Math.Max(1f, secondsPerHour);
        }

        /// <summary>The hour the doors open (12 = 12:00).</summary>
        public float OpensAtHour { get; }

        /// <summary>In-game seconds that make an hour on this clock.</summary>
        public float SecondsPerHour { get; }

        /// <summary>The hour on the clock (may pass 24) after <paramref name="elapsed"/> in-game seconds of service.</summary>
        public float HourAt(float elapsed) => OpensAtHour + elapsed / SecondsPerHour;

        /// <summary>Minutes since midnight on the clock after <paramref name="elapsed"/> seconds of service.</summary>
        public float MinutesAt(float elapsed) => HourAt(elapsed) * 60f;

        /// <summary>In-game seconds of service that make <paramref name="hours"/> hours on the clock.</summary>
        public float Seconds(float hours) => hours * SecondsPerHour;

        /// <summary>"18:00", from minutes since midnight; 24:00 reads as 00:00.</summary>
        public static string Format(float minutes)
        {
            var whole = (int)Math.Round(minutes);
            return $"{whole / 60 % 24:00}:{whole % 60:00}";
        }
    }

    /// <summary>One choice of opening times: when the doors open and close, on the clock.</summary>
    public sealed class OpeningSlot
    {
        public OpeningSlot(string id, string name, float opens, float closes)
        {
            Id = id ?? "";
            Name = name ?? "";
            Opens = opens;
            Closes = Math.Max(opens + 0.5f, closes);
        }

        /// <summary>What the save keeps, e.g. "dinner".</summary>
        public string Id { get; }

        /// <summary>Its name in English, e.g. "Dinner", translated where shown.</summary>
        public string Name { get; }

        /// <summary>The hour the doors open (12 = 12:00).</summary>
        public float Opens { get; }

        /// <summary>The hour they close; 24 is midnight.</summary>
        public float Closes { get; }

        public float Hours => Closes - Opens;
    }

    /// <summary>
    /// A restaurant's opening times (the user's calls, 2026-11-11): one of a few slots, chosen on
    /// forkcast's Restaurant tab, sets how long service lasts and so how many guests come. Guests
    /// come at the usual pace, twice as often in the rush hours around 14:00 and 21:00, so a slot
    /// with both rushes brings far more than one with none. A choice counts from the next service.
    /// </summary>
    public sealed class OpeningTimes
    {
        private readonly List<OpeningSlot> _slots;
        private readonly int _default;

        public OpeningTimes(IEnumerable<OpeningSlot> slots, string defaultId, float secondsPerHour, float lastOrdersMinutes, float[] rushHours, float rushHalfWidthHours)
        {
            _slots = new List<OpeningSlot>(slots ?? Array.Empty<OpeningSlot>());
            if (_slots.Count == 0)
                _slots.Add(new OpeningSlot("dinner", "Dinner", 18f, 23f));
            _default = Math.Max(0, _slots.FindIndex(s => s.Id == defaultId));
            Chosen = _default;
            SecondsPerHour = Math.Max(1f, secondsPerHour);
            LastOrdersMinutes = Math.Max(0f, lastOrdersMinutes);
            RushHours = rushHours ?? Array.Empty<float>();
            RushHalfWidthHours = Math.Max(0f, rushHalfWidthHours);
        }

        public IReadOnlyList<OpeningSlot> Slots => _slots;

        /// <summary>The index of the slot in force from the next service.</summary>
        public int Chosen { get; private set; }

        public OpeningSlot Slot => _slots[Chosen];

        /// <summary>In-game seconds that make an hour on the clock.</summary>
        public float SecondsPerHour { get; }

        /// <summary>Minutes on the clock before closing when the door shuts to new guests.</summary>
        public float LastOrdersMinutes { get; }

        /// <summary>The hours the rushes peak at, on the clock.</summary>
        public IReadOnlyList<float> RushHours { get; }

        /// <summary>A rush lasts this many clock hours either side of its peak.</summary>
        public float RushHalfWidthHours { get; }

        /// <summary>Raised when the choice changes.</summary>
        public event Action<OpeningSlot> Changed;

        public void Choose(int index)
        {
            index = Math.Clamp(index, 0, _slots.Count - 1);
            if (index == Chosen)
                return;
            Chosen = index;
            Changed?.Invoke(Slot);
        }

        /// <summary>The saved choice; an empty or unknown id (an older save, a retuned list) is the default slot.</summary>
        public void Load(string id)
        {
            var index = string.IsNullOrEmpty(id) ? -1 : _slots.FindIndex(s => s.Id == id);
            Chosen = index >= 0 ? index : _default;
        }

        /// <summary>The clock a slot's service runs on.</summary>
        public ServiceClock ClockFor(OpeningSlot slot) => new ServiceClock(slot.Opens, SecondsPerHour);

        /// <summary>The rushes, as clock hours from and to, that fall (at least partly) inside a slot.</summary>
        public IEnumerable<(float From, float To)> RushesIn(OpeningSlot slot)
        {
            foreach (var peak in RushHours)
            {
                var from = Math.Max(slot.Opens, peak - RushHalfWidthHours);
                var to = Math.Min(slot.Closes, peak + RushHalfWidthHours);
                if (to > from)
                    yield return (from, to);
            }
        }

        /// <summary>Sets a service to the chosen slot (or <paramref name="slotIndex"/>): its clock, length, last orders and rushes.</summary>
        public void Apply(ServiceSettings settings, int slotIndex = -1)
        {
            var slot = slotIndex >= 0 && slotIndex < _slots.Count ? _slots[slotIndex] : Slot;
            var clock = ClockFor(slot);
            settings.Clock = clock;
            settings.DurationSeconds = clock.Seconds(slot.Hours);
            settings.LastOrdersSeconds = Math.Min(settings.DurationSeconds, clock.Seconds(LastOrdersMinutes / 60f));
            settings.RushHours = RushHours is float[] hours ? hours : new List<float>(RushHours).ToArray();
            settings.RushHalfWidthHours = RushHalfWidthHours;
            if (settings.Reviews != null)
                settings.Reviews.Clock = clock;
        }
    }
}
