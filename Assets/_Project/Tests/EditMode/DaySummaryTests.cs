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
        public void The_most_ordered_meal_counts_a_dish_however_it_was_cooked()
        {
            var dishes = new[]
            {
                new DishNight { Dish = "steak, rare", Ordered = 2 },
                new DishNight { Dish = "steak, medium", Ordered = 3 },
                new DishNight { Dish = "omelette, soft", Ordered = 4 },
            };
            Assert.That(DaySummary.MostOrdered(dishes), Is.EqualTo("steak"), "5 steaks beat 4 omelettes");
        }

        [Test]
        public void The_most_ordered_meal_goes_by_recipe_when_there_is_one_and_is_null_with_none()
        {
            var dishes = new[]
            {
                new DishNight { RecipeId = "grill.steak", Dish = "steak, rare", Ordered = 1 },
                new DishNight { RecipeId = "grill.steak", Dish = "steak, medium", Ordered = 1 },
                new DishNight { RecipeId = "grill.chop", Dish = "pork chop, medium", Ordered = 1 },
            };
            Assert.That(DaySummary.MostOrdered(dishes), Is.EqualTo("grill.steak"));
            Assert.That(DaySummary.MostOrdered(new DishNight[0]), Is.Null);
            Assert.That(DaySummary.MainOf("steak, medium"), Is.EqualTo("steak"));
            Assert.That(DaySummary.MainOf("steak"), Is.EqualTo("steak"));
        }

        [TestCase(0f, 200f, DaySummary.TipMood.None)]
        [TestCase(2f, 200f, DaySummary.TipMood.Polite)]
        [TestCase(10f, 200f, DaySummary.TipMood.Liked)]
        [TestCase(30f, 200f, DaySummary.TipMood.Liked)]
        [TestCase(40f, 200f, DaySummary.TipMood.Loved)]
        [TestCase(5f, 0f, DaySummary.TipMood.Loved)]
        public void The_tips_caption_follows_how_generous_they_were(float tips, float meals, DaySummary.TipMood mood)
        {
            Assert.That(DaySummary.TipsMood(tips, meals), Is.EqualTo(mood));
        }

        // ---- The day a save records (the user's report, 2026-11-11: "start day 4" landed on day 5) ----

        [Test]
        public void The_last_finished_day_is_today_only_in_review()
        {
            var days = new DayCycle(500f);
            Assert.That(days.LastFinishedDay, Is.Zero, "day 1's plan: nothing finished");
            days.Advance();
            Assert.That(days.LastFinishedDay, Is.Zero, "prep");
            days.Advance();
            Assert.That(days.LastFinishedDay, Is.Zero, "service");
            days.FinishService(Night());
            Assert.That(days.LastFinishedDay, Is.EqualTo(1), "review: day 1 is done");
            days.Advance();
            Assert.That(days.Day, Is.EqualTo(2));
            Assert.That(days.LastFinishedDay, Is.EqualTo(1), "the next morning: still day 1");
        }

        [TestCase(4, 3, 3)]
        [TestCase(3, 3, 3)]
        [TestCase(5, 3, 3)]
        public void A_save_ahead_of_its_history_heals_to_the_last_finished_day(int saved, int lastRecorded, int healed)
        {
            var save = new Game.Simulation.Saving.RestaurantSave { Day = saved };
            for (var day = 1; day <= lastRecorded; day++)
                save.History.Add(new DayRecord { Day = day });
            Assert.That(save.FinishedDay, Is.EqualTo(healed));
        }

        [Test]
        public void A_save_without_history_keeps_its_day()
        {
            Assert.That(new Game.Simulation.Saving.RestaurantSave { Day = 0 }.FinishedDay, Is.Zero);
        }

        [Test]
        public void After_a_relaunch_the_summary_start_and_the_hold_all_agree()
        {
            // The user's save: day 3 finished, but saved as day 4 from the next morning's summary.
            var save = new Game.Simulation.Saving.RestaurantSave { Day = 4 };
            for (var day = 1; day <= 3; day++)
                save.History.Add(new DayRecord { Day = day });
            var days = DayCycle.Continuing(save.FinishedDay, 500f, history: save.History);
            Assert.That(DaySummary.Of(days).Day, Is.EqualTo(3), "the summary is day 3's");
            var starts = days.LastFinishedDay + 1;
            Assert.That(starts, Is.EqualTo(4), "Start says day 4");
            var steps = ForkcastScreen.StepsToPrep(days.Phase);
            for (var i = 0; i < steps; i++)
                days.Advance();
            Assert.That(days.Day, Is.EqualTo(starts), "and holding it lands on day 4");

            // Saving on that morning's summary records day 3 again, never 4.
            var morning = DayCycle.Continuing(3, 500f, history: save.History);
            Assert.That(morning.LastFinishedDay, Is.EqualTo(3));
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
