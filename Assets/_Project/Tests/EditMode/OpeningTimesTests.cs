using System.Linq;
using Game.Presentation.Hud;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Opening times (the user's calls, 2026-11-11): an hour on the service clock is 30 real seconds,
    /// a few slots set when service opens and closes, and guests come twice as often around 14:00
    /// and 21:00.
    /// </summary>
    public class OpeningTimesTests
    {
        // 30 real seconds an hour at the usual 60× time scale.
        private const float Hour = 1800f;

        private static OpeningTimes Times() => new OpeningTimes(new[]
            {
                new OpeningSlot("lunch", "Lunch", 12f, 16f),
                new OpeningSlot("dinner", "Dinner", 18f, 24f),
                new OpeningSlot("allday", "All day", 12f, 24f),
            },
            "dinner", Hour, 30f, new[] { 14f, 21f }, 1f);

        private static ServiceSettings Settings() => new ServiceSettings
        {
            ArrivalIntervalSeconds = 25f * 60f,
            ArrivalJitter = 0f,
            RushMultiplier = 2f,
        };

        [Test]
        public void A_new_restaurant_opens_for_dinner()
        {
            Assert.That(Times().Slot.Id, Is.EqualTo("dinner"));
        }

        [Test]
        public void A_saved_choice_comes_back_and_an_unknown_one_is_the_default()
        {
            var times = Times();
            times.Load("allday");
            Assert.That(times.Slot.Id, Is.EqualTo("allday"));
            times.Load("brunch");
            Assert.That(times.Slot.Id, Is.EqualTo("dinner"));
            times.Load("");
            Assert.That(times.Slot.Id, Is.EqualTo("dinner"), "an older save");
        }

        [Test]
        public void Choosing_raises_changed_once()
        {
            var times = Times();
            var raised = 0;
            times.Changed += _ => raised++;
            times.Choose(0);
            times.Choose(0);
            Assert.That(raised, Is.EqualTo(1));
            Assert.That(times.Slot.Id, Is.EqualTo("lunch"));
        }

        [Test]
        public void The_slot_sets_the_clock_the_length_and_last_orders()
        {
            var times = Times();
            times.Choose(2); // all day
            var settings = Settings();
            times.Apply(settings);
            Assert.That(settings.DurationSeconds, Is.EqualTo(12f * Hour), "12 hours: 6 real minutes");
            Assert.That(settings.LastOrdersSeconds, Is.EqualTo(Hour / 2f), "30 minutes on the clock");
            Assert.That(settings.Clock.OpensAtHour, Is.EqualTo(12f));
            Assert.That(settings.Reviews.Clock, Is.SameAs(settings.Clock), "reviews tell the time on the same clock");
        }

        [Test]
        public void An_hour_on_the_clock_passes_in_its_own_seconds()
        {
            var clock = new ServiceClock(12f, Hour);
            Assert.That(ServiceClock.Format(clock.MinutesAt(0f)), Is.EqualTo("12:00"));
            Assert.That(ServiceClock.Format(clock.MinutesAt(Hour * 2.5f)), Is.EqualTo("14:30"));
            Assert.That(ServiceClock.Format(clock.MinutesAt(Hour * 12f)), Is.EqualTo("00:00"));
            Assert.That(DayCardText.Clock(DayCardText.Minutes(18f, 900f, Hour)), Is.EqualTo("18:30"), "the day card's clock");
            Assert.That(DayCardText.Clock(DayCardText.Minutes(18f, 900f)), Is.EqualTo("18:15"), "the old clock by default");
        }

        [Test]
        public void Rushes_fall_around_two_and_nine_on_the_clock()
        {
            var settings = Settings();
            Times().Apply(settings, 2); // 12:00–24:00
            Assert.That(settings.InRush(0.5f * Hour), Is.False, "12:30");
            Assert.That(settings.InRush(1.5f * Hour), Is.True, "13:30");
            Assert.That(settings.InRush(3.5f * Hour), Is.False, "15:30");
            Assert.That(settings.InRush(8.5f * Hour), Is.True, "20:30");
            Assert.That(settings.InRush(10.5f * Hour), Is.False, "22:30");
            Assert.That(settings.ArrivalInterval(1.5f * Hour), Is.EqualTo(settings.ArrivalInterval(0.5f * Hour) / 2f).Within(0.01f));
        }

        [Test]
        public void Each_slot_lists_the_rushes_inside_it()
        {
            var times = Times();
            Assert.That(times.RushesIn(times.Slots[0]).ToList(), Is.EqualTo(new[] { (13f, 15f) }));
            Assert.That(times.RushesIn(times.Slots[1]).ToList(), Is.EqualTo(new[] { (20f, 22f) }));
            Assert.That(times.RushesIn(times.Slots[2]).Count(), Is.EqualTo(2));
        }

        [Test]
        public void All_day_brings_the_most_guests_and_lunch_the_fewest()
        {
            var times = Times();
            float Groups(int slot)
            {
                var settings = Settings();
                times.Apply(settings, slot);
                return GuestForecast.Groups(settings, 1f);
            }
            // Lunch: 3.5 h before last orders (105 real s), 2 of them rush: 63 s calm + 60 s rush at
            // one per 25 s, doubled → 1.8 + 4.8 = 6.6. Dinner: 5.5 h, 2 rush → 3.5 calm: 4.2 + 4.8 = 9.
            // All day: 11.5 h, 4 rush → 7.5 calm: 9 + 9.6 = 18.6.
            Assert.That(Groups(0), Is.EqualTo(6.6f).Within(0.01f));
            Assert.That(Groups(1), Is.EqualTo(9f).Within(0.01f));
            Assert.That(Groups(2), Is.EqualTo(18.6f).Within(0.01f));
        }

        [Test]
        public void Guests_really_come_more_often_in_the_rush()
        {
            var settings = Settings();
            settings.LastOrdersSeconds = 0f;
            Times().Apply(settings, 2);
            var service = new ServiceSimulation(settings, 0);
            for (var t = 0f; t < settings.DurationSeconds - Hour; t += 10f)
                service.Tick(10f);
            var arrivals = service.Customers.Select(c => c.Party).Distinct().Select(p => p[0].ArrivedAt).ToList();
            var rush = arrivals.Count(at => settings.InRush(at));
            // Four rush hours and seven calm ones in the first eleven, at one group per 25 real s
            // (an hour is 30): about 9.6 rush groups if the rush is twice as busy, 4.8 if it isn't,
            // and about 8.4 calm ones.
            Assert.That(rush, Is.InRange(7, 11));
            Assert.That(arrivals.Count - rush, Is.InRange(7, 10));
        }

        [Test]
        public void Choosing_before_the_doors_open_retimes_the_service()
        {
            var settings = Settings();
            var times = Times();
            times.Apply(settings);
            var service = new ServiceSimulation(settings, 0);
            times.Choose(0);
            service.SetOpening(times);
            Assert.That(service.Settings.Clock.OpensAtHour, Is.EqualTo(12f));
            Assert.That(service.Settings.DurationSeconds, Is.EqualTo(4f * Hour));
        }
    }
}
