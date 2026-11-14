using System.Linq;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>The service laid out for the day card's timeline (2026-11-14).</summary>
    public class ServiceTimelineTests
    {
        private static OpeningTimes Opening() => new OpeningTimes(new[]
        {
            new OpeningSlot("lunch", "Lunch", 12f, 16f),
            new OpeningSlot("dinner", "Dinner", 18f, 24f),
            new OpeningSlot("allday", "All day", 12f, 24f),
        }, "dinner", 30f, 30f, new[] { 14f, 21f }, 0.75f);

        private static ServiceSettings Slot(int index)
        {
            var settings = new ServiceSettings();
            Opening().Apply(settings, index);
            return settings;
        }

        [Test]
        public void AnAllDayServiceRunsFromNoonToMidnight()
        {
            var settings = Slot(2);
            Assert.That(ServiceTimeline.OpensAt(settings), Is.EqualTo(12f).Within(1e-4f));
            Assert.That(ServiceTimeline.ClosesAt(settings), Is.EqualTo(24f).Within(1e-4f));
            Assert.That(ServiceTimeline.Fraction(settings, settings.DurationSeconds / 2f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(ServiceTimeline.Fraction(settings, settings.DurationSeconds * 2f), Is.EqualTo(1f));
        }

        [Test]
        public void BothRushesSitWhereTheirHoursFall()
        {
            var rushes = ServiceTimeline.Rushes(Slot(2));
            Assert.That(rushes.Count, Is.EqualTo(2));
            Assert.That(rushes[0].From, Is.EqualTo((14f - 0.75f - 12f) / 12f).Within(1e-4f));
            Assert.That(rushes[1].To, Is.EqualTo((21f + 0.75f - 12f) / 12f).Within(1e-4f));
            Assert.That(ServiceTimeline.IsLunchRush(rushes[0].PeakHour), Is.True);
            Assert.That(ServiceTimeline.IsLunchRush(rushes[1].PeakHour), Is.False);
        }

        [Test]
        public void ALunchOnlyHasTheLunchRush()
        {
            var rushes = ServiceTimeline.Rushes(Slot(0));
            Assert.That(rushes.Count, Is.EqualTo(1));
            Assert.That(rushes[0].PeakHour, Is.EqualTo(14f));
        }

        [Test]
        public void TheRushTheClockIsInIsFound()
        {
            var settings = Slot(1); // dinner, 18:00 to midnight, 30 s an hour
            Assert.That(ServiceTimeline.RushAt(settings, 3f * 30f), Is.EqualTo(0)); // 21:00
            Assert.That(ServiceTimeline.RushAt(settings, 0.5f * 30f), Is.EqualTo(-1)); // 18:30
        }

        [Test]
        public void LastOrdersStartHalfAnHourBeforeClosing()
        {
            Assert.That(ServiceTimeline.LastOrdersFrom(Slot(1)), Is.EqualTo(1f - 0.5f / 6f).Within(1e-4f));
        }

        [Test]
        public void AShortServiceLabelsEveryHourAndALongOneEveryOther()
        {
            var dinner = ServiceTimeline.HourMarks(Slot(1));
            Assert.That(dinner.Select(m => m.Hour), Is.EqualTo(new[] { 18, 19, 20, 21, 22, 23, 0 }));
            Assert.That(dinner.All(m => m.Labelled), Is.True);
            var allDay = ServiceTimeline.HourMarks(Slot(2));
            Assert.That(allDay.Count, Is.EqualTo(13));
            Assert.That(allDay.Where(m => m.Labelled).Select(m => m.Hour), Is.EqualTo(new[] { 12, 14, 16, 18, 20, 22, 0 }));
            Assert.That(allDay.Last().Fraction, Is.EqualTo(1f));
        }

        [Test]
        public void TheServiceKnowsItsSlot()
        {
            var opening = Opening();
            Assert.That(ServiceTimeline.SlotOf(opening, Slot(2)).Id, Is.EqualTo("allday"));
            Assert.That(ServiceTimeline.SlotOf(opening, new ServiceSettings { DurationSeconds = 100f }), Is.Null);
        }
    }
}
