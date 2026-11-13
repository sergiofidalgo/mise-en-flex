using System.Linq;
using Game.Simulation.Days;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The 4-3-2-1 before service (decision 2026-11-11): the doors are held, the first guests wait
    /// on the doorstep, and nobody comes in or loses patience before zero.
    /// </summary>
    public class ServiceCountdownTests
    {
        private const float Minute = 60f;
        private const float Hour = 3600f;

        private static ServiceSimulation Service(bool walkIn = true) =>
            new ServiceSimulation(new ServiceSettings
            {
                PatienceSeconds = 30f * Minute,
                QueuePatienceRate = 0.5f,
                DurationSeconds = 6f * Hour,
                ArrivalIntervalSeconds = 12f * Minute,
                ArrivalJitter = 0f,
                RushStart = 0.35f,
                RushEnd = 0.6f,
                RushMultiplier = 1f,
                EatSeconds = 15f * Minute,
                MaxQueue = 3,
                Seed = 7,
            }, seatCount: 3) { GuestsWalkIn = walkIn };

        [Test]
        public void CountsFourThreeTwoOneThenFinishesOnce()
        {
            var countdown = new ServiceCountdown();
            var finished = 0;
            countdown.Finished += () => finished++;
            countdown.Start(4f);

            var shown = new System.Collections.Generic.List<int>();
            for (var i = 0; i < 40; i++)
            {
                if (countdown.Running && (shown.Count == 0 || shown[shown.Count - 1] != countdown.Shown))
                    shown.Add(countdown.Shown);
                countdown.Tick(0.1f);
            }

            Assert.That(shown, Is.EqualTo(new[] { 4, 3, 2, 1 }));
            Assert.That(finished, Is.EqualTo(1));
            Assert.That(countdown.Running, Is.False);
        }

        [Test]
        public void ZeroLengthOpensAtOnce()
        {
            var countdown = new ServiceCountdown();
            var finished = 0;
            countdown.Finished += () => finished++;
            countdown.Start(0f);
            Assert.That(finished, Is.EqualTo(1));
            Assert.That(countdown.Running, Is.False);
        }

        [Test]
        public void HeldDoorsStopTheClockAndKeepTheFirstGuestsOnTheDoorstep()
        {
            var service = Service();
            service.HoldDoors();
            service.Tick(2f * Hour); // however long, the doors stay shut

            Assert.That(service.Elapsed, Is.EqualTo(0f), "The service clock waits for the doors.");
            Assert.That(service.Customers, Is.Not.Empty, "The first party sets off up the street at once.");
            Assert.That(service.Customers.All(c => c.State == CustomerState.Approaching));

            foreach (var customer in service.Customers.ToList())
                service.ReachDoor(customer);
            service.Tick(1f * Hour);

            Assert.That(service.Customers.All(c => c.State == CustomerState.Approaching), "Nobody comes in before zero.");
            Assert.That(service.Customers.All(c => c.AtDoor), "They wait on the doorstep.");
            Assert.That(service.Customers.All(c => c.Patience == 1f), "Waiting for the doors costs no patience.");
            Assert.That(service.Customers.Select(c => c.Party).Distinct().Count(), Is.EqualTo(1), "Only the first party comes early.");
        }

        [Test]
        public void OpeningTheDoorsLetsTheWaitingGuestsIn()
        {
            var service = Service();
            service.HoldDoors();
            service.Tick(1f);
            foreach (var customer in service.Customers.ToList())
                service.ReachDoor(customer);

            service.OpenDoors();

            Assert.That(service.DoorsHeld, Is.False);
            Assert.That(service.Customers.All(c => c.State == CustomerState.WalkingToSeat), "In they come, to their seats.");
            service.Tick(1f * Minute);
            Assert.That(service.Elapsed, Is.EqualTo(1f * Minute), "The clock starts with the doors.");
        }

        [Test]
        public void AGuestStillInTheStreetAtZeroComesInOnArriving()
        {
            var service = Service();
            service.HoldDoors();
            service.Tick(1f);
            service.OpenDoors();
            var first = service.Customers[0];
            Assert.That(first.State, Is.EqualTo(CustomerState.Approaching));

            foreach (var member in first.Party)
                service.ReachDoor(member);

            Assert.That(first.State, Is.EqualTo(CustomerState.WalkingToSeat));
        }

        [Test]
        public void WithoutAStreetNobodyComesBeforeZero()
        {
            var service = Service(walkIn: false);
            service.HoldDoors();
            service.Tick(2f * Hour);
            Assert.That(service.Customers, Is.Empty);

            service.OpenDoors();
            service.Tick(13f * Minute);
            Assert.That(service.Customers, Is.Not.Empty, "Arrivals start with the doors.");
        }
    }
}
