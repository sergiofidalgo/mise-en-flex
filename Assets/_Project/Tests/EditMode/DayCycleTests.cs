using System;
using System.Linq;
using Game.Data;
using Game.Simulation.Days;
using Game.Simulation.Service;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode
{
    public class DayCycleTests
    {
        // A finished, empty service, summarised after taking no money.
        private static ServiceSummary EmptySummary() =>
            ServiceSummary.From(new ServiceSimulation(new ServiceSettings(), seatCount: 1));

        private static void PlayToReview(DayCycle days, ServiceSummary summary)
        {
            days.Advance(); // to Prep
            days.Advance(); // to Service
            days.FinishService(summary);
        }

        [Test]
        public void ADayRunsPlanPrepServiceReviewThenTheNextDay()
        {
            var days = new DayCycle();
            Assert.That(days.Day, Is.EqualTo(1));
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Plan));

            days.Advance();
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Prep));
            days.Advance();
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Service));
            days.FinishService(EmptySummary());
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Review));
            Assert.That(days.Day, Is.EqualTo(1), "still the same day until the Review ends");

            days.Advance();
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Plan));
            Assert.That(days.Day, Is.EqualTo(2));
        }

        [Test]
        public void AServiceOnlyEndsBeingFinished()
        {
            var days = new DayCycle();
            Assert.Throws<InvalidOperationException>(() => days.FinishService(EmptySummary()), "no service during Plan");
            days.Advance();
            days.Advance();
            Assert.Throws<InvalidOperationException>(() => days.Advance(), "Advance can't skip a running service");
        }

        [Test]
        public void MoneyCarriesFromDayToDay()
        {
            var settings = new ServiceSettings
            {
                ArrivalIntervalSeconds = 720f,
                ArrivalJitter = 0f,
                PatienceSeconds = 600f,
                Menu = new OrderMenu
                {
                    MainTypeId = "Steak",
                    MainPrice = 24f,
                    Choices = new[] { new OrderChoice(new Game.Simulation.Cooking.DonenessLevel("Rare", 45f, 48f), 1f) },
                },
            };
            var service = new ServiceSimulation(settings, seatCount: 1);
            service.Tick(721f);
            service.SitDown(service.Customers.Single());
            service.Tick(601f); // walks out: -12
            var lossDay = ServiceSummary.From(service);
            Assert.That(lossDay.Money, Is.EqualTo(-12f));

            var days = new DayCycle();
            PlayToReview(days, lossDay);
            Assert.That(days.Balance, Is.EqualTo(-12f), "it can go negative");
            days.Advance();
            PlayToReview(days, lossDay);
            Assert.That(days.Balance, Is.EqualTo(-24f));
            Assert.That(days.LastService, Is.SameAs(lossDay));
        }

        [Test]
        public void PhaseChangesAreAnnounced()
        {
            var days = new DayCycle();
            var seen = new System.Collections.Generic.List<DayPhase>();
            days.PhaseChanged += seen.Add;
            PlayToReview(days, EmptySummary());
            days.Advance();
            Assert.That(seen, Is.EqualTo(new[] { DayPhase.Prep, DayPhase.Service, DayPhase.Review, DayPhase.Plan }));
        }

        [Test]
        public void EveryoneHasToBeReady()
        {
            var ready = new ReadyCheck();
            Assert.That(ready.IsEveryoneReady(0), Is.False, "nobody here");
            Assert.That(ready.IsEveryoneReady(2), Is.False);

            ready.Toggle(0);
            Assert.That(ready.IsEveryoneReady(1));
            Assert.That(ready.IsEveryoneReady(2), Is.False);

            ready.Toggle(1);
            Assert.That(ready.IsEveryoneReady(2));

            ready.Toggle(1);
            Assert.That(ready.IsReady(1), Is.False, "pressing again takes it back");
            Assert.That(ready.ReadyCount, Is.EqualTo(1));

            ready.Clear();
            Assert.That(ready.ReadyCount, Is.EqualTo(0));
        }

        // Checks the seed rather than the arrivals: with jitter tuned to 0, arrivals rightly match.
        // TheSameSeedPlaysTheSameService covers a seed replaying the same service.
        [Test]
        public void EachDayHasItsOwnSeedAndTheSameDayRepeats()
        {
            var config = AssetDatabase.LoadAssetAtPath<ServiceConfig>("Assets/_Project/Data/Settings/ServiceConfig.asset");
            Assert.That(config.ToSettings(1).Seed, Is.EqualTo(config.ToSettings(1).Seed));
            Assert.That(config.ToSettings(2).Seed, Is.Not.EqualTo(config.ToSettings(1).Seed));
            Assert.That(config.ToSettings(3).Seed, Is.Not.EqualTo(config.ToSettings(2).Seed));
        }
    }
}
