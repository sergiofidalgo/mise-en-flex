using System.Linq;
using Game.Simulation.Days;
using Game.Simulation.Saving;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Money out and the fee (Phase 5, decisions 2026-11-08): every movement of money is booked, the fee
    /// falls due every 5 finished days and can push the restaurant into debt, and replies nudge the rating.
    /// </summary>
    public class MoneyTests
    {
        private static ServiceSummary EmptySummary() =>
            ServiceSummary.From(new ServiceSimulation(new ServiceSettings(), seatCount: 1));

        private static DayCycle Days(float balance = 100f, float fee = 450f) =>
            new DayCycle(balance, new FeeSettings { Amount = fee, EveryDays = 5 });

        private static void PlayADay(DayCycle days, float wages = 0f)
        {
            days.Advance();
            days.Advance();
            days.FinishService(EmptySummary(), wages);
            days.Advance();
        }

        [Test]
        public void TheBooksAlwaysAddUpToTheBalance()
        {
            var days = Days(100f);
            days.Advance();
            days.Charge(LedgerLine.Ingredients, 40f);
            Assert.That(days.TrySpend(25f), Is.True);
            days.Receive(10f);

            Assert.That(days.Today.Of(LedgerLine.Ingredients), Is.EqualTo(40f));
            Assert.That(days.Today.Of(LedgerLine.Purchases), Is.EqualTo(25f));
            Assert.That(days.Today.Of(LedgerLine.Sales), Is.EqualTo(10f));
            Assert.That(days.Balance, Is.EqualTo(100f + days.Today.Net));
        }

        [Test]
        public void TheDeliveryIsChargedEvenIntoDebt()
        {
            var days = Days(10f);
            days.Charge(LedgerLine.Ingredients, 60f);

            Assert.That(days.Balance, Is.EqualTo(-50f));
            Assert.That(days.OwedToBoss, Is.EqualTo(50f));
        }

        [Test]
        public void TheFeeFallsDueEveryFiveFinishedDays()
        {
            var days = Days(1000f, 450f);
            for (var i = 0; i < 4; i++)
                PlayADay(days);
            Assert.That(days.Balance, Is.EqualTo(1000f), "nothing yet after four days");
            Assert.That(days.DaysUntilFee, Is.EqualTo(1));

            days.Advance();
            days.Advance();
            days.FinishService(EmptySummary());

            Assert.That(days.FeeCollectedToday, Is.True);
            Assert.That(days.Today.Of(LedgerLine.Fee), Is.EqualTo(450f));
            Assert.That(days.Balance, Is.EqualTo(550f));
            Assert.That(days.History.Count, Is.EqualTo(5));
            Assert.That(days.History.Last().Balance, Is.EqualTo(550f), "the day's record is after the fee");

            days.Advance();
            Assert.That(days.DaysUntilFee, Is.EqualTo(5), "a new period starts");
            Assert.That(days.Period.Out, Is.EqualTo(0f));
        }

        [Test]
        public void ABadPeriodEndsInDebtToTheBoss()
        {
            var days = Days(100f, 450f);
            for (var i = 0; i < 5; i++)
                PlayADay(days, wages: 30f);

            Assert.That(days.Balance, Is.EqualTo(100f - 150f - 450f));
            Assert.That(days.OwedToBoss, Is.EqualTo(500f));
        }

        [Test]
        public void ContinuingKeepsTheDaysTowardsTheFeeAndThePeriodsBooks()
        {
            var period = new Ledger();
            period.Add(LedgerLine.Payments, 120f);

            var days = DayCycle.Continuing(3, 50f, new FeeSettings { Amount = 450f, EveryDays = 5 }, 3, period);

            Assert.That(days.DaysUntilFee, Is.EqualTo(2));
            Assert.That(days.Period.Of(LedgerLine.Payments), Is.EqualTo(120f));
            Assert.That(days.Today.In, Is.EqualTo(0f));
        }

        [Test]
        public void AFittingReplyLiftsTheRatingAndAPoorOneLowersIt()
        {
            var settings = new ReplySettings { FittingNudge = 0.25f, PoorNudge = 0.25f };
            var complaint = new ReviewRecord { Stars = 1f, Topic = (int)ReviewTopic.SlowService };
            var praise = new ReviewRecord { Stars = 5f, Topic = (int)ReviewTopic.Praise };
            var other = new ReviewRecord { Stars = 2f, Topic = (int)ReviewTopic.Burnt };

            Assert.That(ReviewReplies.Reply(complaint, ReviewReply.Apologise, settings), Is.True);
            Assert.That(ReviewReplies.Reply(complaint, ReviewReply.Thank, settings), Is.False, "one reply per review");
            ReviewReplies.Reply(praise, ReviewReply.Thank, settings);
            ReviewReplies.Reply(other, ReviewReply.StandYourGround, settings);

            Assert.That(complaint.RatedStars, Is.EqualTo(1.25f));
            Assert.That(praise.RatedStars, Is.EqualTo(5f), "can't go above five");
            Assert.That(other.RatedStars, Is.EqualTo(1.75f), "standing your ground never fits");
            Assert.That(ReviewReplies.Rating(new[] { complaint, praise, other }), Is.EqualTo((1.25f + 5f + 1.75f) / 3f).Within(0.0001f));
        }
    }
}
