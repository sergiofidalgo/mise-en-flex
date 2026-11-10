using System.Collections.Generic;
using Game.Simulation.Days;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>forkcast's end-of-day summary: always the last finished day (2026-11-11).</summary>
    public class DaySummaryTests
    {
        private static ServiceSummary Night() => ServiceSummary.From(new ServiceSimulation(new ServiceSettings(), seatCount: 1));

        private static void PlayDay(DayCycle days, float ingredients)
        {
            if (days.Phase == DayPhase.Review)
                days.Advance();
            days.Advance();
            days.Advance();
            days.Charge(LedgerLine.Ingredients, ingredients);
            days.FinishService(Night(), wages: 30f);
        }

        [Test]
        public void Before_any_day_ends_there_is_nothing_to_summarise()
        {
            var summary = DaySummary.Of(new DayCycle(100f));
            Assert.That(summary.HasDay, Is.False);
            Assert.That(summary.NextDay, Is.EqualTo(1));
        }

        [Test]
        public void In_review_it_is_the_day_just_finished()
        {
            var days = new DayCycle(500f);
            PlayDay(days, 40f);
            var summary = DaySummary.Of(days);
            Assert.That(summary.Day, Is.EqualTo(1));
            Assert.That(summary.NextDay, Is.EqualTo(2));
            Assert.That(summary.Ledger.Of(LedgerLine.Ingredients), Is.EqualTo(40f));
            Assert.That(summary.Ledger.Of(LedgerLine.Wages), Is.EqualTo(30f));
        }

        [Test]
        public void The_next_morning_it_is_still_yesterday_not_the_empty_new_day()
        {
            var days = new DayCycle(500f);
            PlayDay(days, 40f);
            PlayDay(days, 70f);
            days.Advance(); // day 3's morning: Today is empty
            Assert.That(days.Day, Is.EqualTo(3));
            Assert.That(days.Today.Out, Is.Zero);
            var summary = DaySummary.Of(days);
            Assert.That(summary.Day, Is.EqualTo(2));
            Assert.That(summary.NextDay, Is.EqualTo(3));
            Assert.That(summary.Ledger.Of(LedgerLine.Ingredients), Is.EqualTo(70f));
        }

        [Test]
        public void A_saved_and_reloaded_history_gives_the_same_books()
        {
            var days = new DayCycle(500f);
            PlayDay(days, 55f);
            var record = days.History[0];
            var json = UnityEngine.JsonUtility.ToJson(new Wrapper { History = new List<DayRecord> { record } });
            var back = UnityEngine.JsonUtility.FromJson<Wrapper>(json).History;
            var reloaded = DayCycle.Continuing(record.Day, days.Balance, history: back);
            var summary = DaySummary.Of(reloaded);
            Assert.That(summary.Day, Is.EqualTo(1));
            Assert.That(summary.Ledger.Of(LedgerLine.Ingredients), Is.EqualTo(55f));
            Assert.That(summary.Ledger.Of(LedgerLine.Wages), Is.EqualTo(30f));
        }

        [System.Serializable]
        private class Wrapper
        {
            public List<DayRecord> History = new List<DayRecord>();
        }

        [Test]
        public void An_older_save_without_lines_falls_back_to_its_totals()
        {
            var days = DayCycle.Continuing(4, 100f, history: new[] { new DayRecord { Day = 3, In = 200f, Out = 90f } });
            var summary = DaySummary.Of(days);
            Assert.That(summary.Day, Is.EqualTo(3));
            Assert.That(summary.Ledger.In, Is.EqualTo(200f));
            Assert.That(summary.Ledger.Out, Is.EqualTo(90f));
        }

        [Test]
        public void A_collection_night_is_kept_with_the_day()
        {
            var days = new DayCycle(100f, new FeeSettings { Amount = 300f, EveryDays = 1 });
            PlayDay(days, 0f);
            var record = days.History[0];
            Assert.That(record.Collected, Is.True);
            Assert.That(record.CollectionDue, Is.EqualTo(300f));
            Assert.That(record.CollectionPaid, Is.LessThan(300f), "the balance didn't cover it");
        }

        [Test]
        public void A_dish_night_averages_its_stars()
        {
            var dish = new DishNight { Ordered = 3, StarSum = 7f, Rated = 2 };
            Assert.That(dish.Stars, Is.EqualTo(3.5f));
            Assert.That(new DishNight().Stars, Is.Null);
        }
    }
}
