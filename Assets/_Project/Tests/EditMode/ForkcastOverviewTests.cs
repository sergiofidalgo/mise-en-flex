using System;
using Game.Simulation.Campaign;
using Game.Simulation.Days;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// forkcast after the playtest (the user's calls, 2026-11-10): the Start button's tap-or-hold A
    /// and the boss card.
    /// </summary>
    public class ForkcastOverviewTests
    {
        // ---- The Start button: a tap acts, a hold readies ----

        private static MenuPress.Result Run(StartHold hold, int player, bool down, float seconds, int frames)
        {
            var result = MenuPress.Result.None;
            for (var i = 0; i < frames; i++)
            {
                var r = hold.Update(player, down, seconds);
                if (r != MenuPress.Result.None)
                    result = r;
            }
            return result;
        }

        private static StartHold Armed(int player = 0)
        {
            var hold = new StartHold { HoldSeconds = 0.8f };
            hold.Update(player, false, 0.016f); // seen up once
            return hold;
        }

        [Test]
        public void A_quick_press_is_a_tap_on_release()
        {
            var hold = Armed();
            Assert.That(Run(hold, 0, true, 0.1f, 2), Is.EqualTo(MenuPress.Result.None), "nothing yet while it's down");
            Assert.That(hold.Update(0, false, 0.016f), Is.EqualTo(MenuPress.Result.Tap));
        }

        [Test]
        public void Holding_fills_the_button_and_counts_once()
        {
            var hold = Armed();
            hold.Update(0, true, 0f);
            Assert.That(Run(hold, 0, true, 0.2f, 2), Is.EqualTo(MenuPress.Result.None));
            Assert.That(hold.Progress(0), Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(hold.MostProgress, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(Run(hold, 0, true, 0.2f, 3), Is.EqualTo(MenuPress.Result.Hold));
            Assert.That(Run(hold, 0, true, 0.2f, 5), Is.EqualTo(MenuPress.Result.None), "still down: no second hold");
            Assert.That(hold.Update(0, false, 0.016f), Is.EqualTo(MenuPress.Result.None), "letting go after a hold isn't a tap");
        }

        [Test]
        public void The_press_that_joined_a_player_does_nothing()
        {
            var hold = new StartHold();
            Assert.That(Run(hold, 1, true, 0.5f, 4), Is.EqualTo(MenuPress.Result.None));
            Assert.That(hold.Update(1, false, 0.016f), Is.EqualTo(MenuPress.Result.None));
            hold.Update(1, true, 0.016f);
            Assert.That(hold.Update(1, false, 0.016f), Is.EqualTo(MenuPress.Result.Tap), "the next press counts");
        }

        [Test]
        public void Each_player_holds_on_their_own()
        {
            var hold = Armed(0);
            hold.Update(1, false, 0.016f);
            hold.Update(0, true, 0f);
            hold.Update(0, true, 0.4f);
            Assert.That(hold.Progress(0), Is.GreaterThan(0f));
            Assert.That(hold.Progress(1), Is.Zero);
        }

        [Test]
        public void A_disarmed_press_must_come_up_first()
        {
            var hold = Armed();
            hold.Update(0, true, 0f);
            hold.Disarm(0);
            Assert.That(Run(hold, 0, true, 0.5f, 4), Is.EqualTo(MenuPress.Result.None), "used by the reply wheel");
        }

        // ---- Ready: A puts you in, B takes it back (2026-11-11) ----

        [Test]
        public void Holding_again_keeps_you_ready_and_B_takes_it_back()
        {
            var ready = new ReadyCheck();
            ready.Set(1, true);
            ready.Set(1, true);
            Assert.That(ready.IsReady(1), Is.True, "a second hold doesn't un-ready");
            Assert.That(ready.ReadyCount, Is.EqualTo(1));
            ready.Set(1, false);
            Assert.That(ready.IsReady(1), Is.False);
            ready.Set(1, false);
            Assert.That(ready.ReadyCount, Is.Zero);
        }

        // ---- The boss card ----

        [Test]
        public void It_leads_with_the_collection_and_how_much_is_covered()
        {
            var card = BossCard.For(due: 600f, balance: 450f, daysUntilFee: 3, serviceStillToCome: true, BossStep.None, 1);
            Assert.That(card.Falls, Is.EqualTo(BossCard.When.InDays));
            Assert.That(card.DaysLeft, Is.EqualTo(3));
            Assert.That(card.Covered, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(card.Shortfall, Is.EqualTo(150f).Within(0.001f));
            Assert.That(card.IsShort, Is.True);
            Assert.That(card.Urgent, Is.False, "three days: still time");
        }

        [Test]
        public void Short_with_two_days_or_fewer_is_urgent()
        {
            Assert.That(BossCard.For(600f, 100f, 2, true, BossStep.None, 1).Urgent, Is.True);
            Assert.That(BossCard.For(600f, 100f, 1, false, BossStep.None, 1).Urgent, Is.True);
            Assert.That(BossCard.For(600f, 700f, 1, true, BossStep.None, 1).Urgent, Is.False, "covered: no alarm");
        }

        [Test]
        public void Covered_never_goes_past_full_or_below_empty()
        {
            Assert.That(BossCard.For(600f, 1200f, 3, true, BossStep.None, 1).Covered, Is.EqualTo(1f));
            Assert.That(BossCard.For(600f, -80f, 3, true, BossStep.None, 1).Covered, Is.EqualTo(0f));
            Assert.That(BossCard.For(600f, -80f, 3, true, BossStep.None, 1).Shortfall, Is.EqualTo(680f).Within(0.001f));
        }

        [Test]
        public void The_last_day_is_tonight_in_the_morning_and_tomorrow_after_service()
        {
            Assert.That(BossCard.For(600f, 0f, 1, serviceStillToCome: true, BossStep.None, 1).Falls, Is.EqualTo(BossCard.When.Tonight));
            Assert.That(BossCard.For(600f, 0f, 1, serviceStillToCome: false, BossStep.None, 1).Falls, Is.EqualTo(BossCard.When.Tomorrow));
        }

        [TestCase(600f, 0f, BossCard.Standing.Short)]
        [TestCase(600f, 599f, BossCard.Standing.Short)]
        [TestCase(600f, 600f, BossCard.Standing.JustCovered)]
        [TestCase(600f, 650f, BossCard.Standing.JustCovered)]
        [TestCase(600f, 670f, BossCard.Standing.Covered)]
        [TestCase(600f, 2000f, BossCard.Standing.Covered)]
        public void Covered_just_covered_or_short(float due, float balance, BossCard.Standing standing)
        {
            var card = BossCard.For(due, balance, 3, false, BossStep.None, 1);
            Assert.That(card.Stands, Is.EqualTo(standing));
            Assert.That(card.Spare, Is.EqualTo(Math.Max(0f, balance - due)).Within(0.001f));
        }

        [Test]
        public void Nothing_due_is_covered()
        {
            Assert.That(BossCard.For(0f, 0f, 3, false, BossStep.None, 1).Stands, Is.EqualTo(BossCard.Standing.Covered));
        }

        [TestCase(BossStep.None, 1, BossVisit.Warning)]
        [TestCase(BossStep.Warned, 1, BossVisit.Repossession)]
        [TestCase(BossStep.Repossessed, 1, BossVisit.StripToMinimum)]
        [TestCase(BossStep.Repossessed, 2, BossVisit.TakeRestaurant)]
        [TestCase(BossStep.RestaurantTaken, 2, BossVisit.Erasure)]
        [TestCase(BossStep.Erased, 1, BossVisit.Erasure)]
        public void Missing_it_brings_the_next_step(BossStep step, int restaurants, BossVisit next)
        {
            Assert.That(BossCard.For(600f, 0f, 3, true, step, restaurants).IfMissed, Is.EqualTo(next));
        }
    }
}
