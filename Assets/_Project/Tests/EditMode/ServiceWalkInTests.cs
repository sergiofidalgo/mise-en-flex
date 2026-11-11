using System.Linq;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Customers walking in from the street (decision 2026-11-11): they appear approaching, and
    /// only at the door are they seated, queued or turned away and does their patience start.
    /// </summary>
    public class ServiceWalkInTests
    {
        private const float Minute = 60f;
        private const float Hour = 3600f;

        private static ServiceSimulation Service(int seats, int queue = 3, float patience = 30f * Minute) =>
            new ServiceSimulation(new ServiceSettings
            {
                PatienceSeconds = patience,
                QueuePatienceRate = 0.5f,
                DurationSeconds = 6f * Hour,
                ArrivalIntervalSeconds = 12f * Minute,
                ArrivalJitter = 0f,
                RushStart = 0.35f,
                RushEnd = 0.6f,
                RushMultiplier = 1f,
                EatSeconds = 15f * Minute,
                MaxQueue = queue,
                Seed = 7,
            }, seatCount: seats) { GuestsWalkIn = true };

        [Test]
        public void NewCustomersApproachAndAreNotAtTheDoorYet()
        {
            var service = Service(seats: 3);
            var appeared = 0;
            var arrived = 0;
            service.Appeared += _ => appeared++;
            service.Arrived += _ => arrived++;

            service.Tick(13f * Minute);

            var customer = service.Customers.Single();
            Assert.That(customer.State, Is.EqualTo(CustomerState.Approaching));
            Assert.That(customer.Seat, Is.EqualTo(-1), "No seat until they're at the door.");
            Assert.That(appeared, Is.EqualTo(1));
            Assert.That(arrived, Is.EqualTo(0));
        }

        [Test]
        public void PatienceStartsOnlyAtTheDoor()
        {
            var service = Service(seats: 3, patience: 30f * Minute);
            service.Tick(13f * Minute);
            var customer = service.Customers.Single();

            service.Tick(45f * Minute); // a long walk: longer than their whole patience

            Assert.That(customer.Patience, Is.EqualTo(1f));
            Assert.That(customer.SecondsInRestaurant, Is.EqualTo(0f));
            service.ReachDoor(customer);
            Assert.That(customer.State, Is.EqualTo(CustomerState.WalkingToSeat));
            Assert.That(customer.ArrivedAt, Is.EqualTo(service.Elapsed), "Arrival is counted at the door.");
        }

        [Test]
        public void AtTheDoorTheyAreSeatedQueuedOrTurnedAway()
        {
            var service = Service(seats: 1, queue: 1);
            service.Tick(13f * Minute + 24f * Minute); // three on their way
            var walkers = service.Customers.ToList();
            Assert.That(walkers.Count, Is.EqualTo(3));
            var arrived = 0;
            service.Arrived += _ => arrived++;

            foreach (var customer in walkers)
                service.ReachDoor(customer);

            Assert.That(walkers[0].State, Is.EqualTo(CustomerState.WalkingToSeat));
            Assert.That(walkers[1].State, Is.EqualTo(CustomerState.Queueing));
            Assert.That(walkers[2].State, Is.EqualTo(CustomerState.Leaving));
            Assert.That(walkers[2].WalkedOut, Is.True);
            Assert.That(service.LostCovers, Is.EqualTo(1));
            Assert.That(arrived, Is.EqualTo(3), "Arrived is raised at the door.");
        }

        [Test]
        public void ReachingTheDoorTwiceChangesNothing()
        {
            var service = Service(seats: 3);
            service.Tick(13f * Minute);
            var customer = service.Customers.Single();
            service.ReachDoor(customer);
            service.SitDown(customer);

            service.ReachDoor(customer);

            Assert.That(customer.State, Is.EqualTo(CustomerState.Waiting));
        }

        [Test]
        public void TheServiceWaitsForEveryoneStillOnTheirWay()
        {
            var service = Service(seats: 3);
            service.Tick(5f * Hour + 50f * Minute); // past last orders
            var walking = service.Customers.Where(c => c.State == CustomerState.Approaching).ToList();
            Assert.That(walking, Is.Not.Empty);

            service.Tick(20f * Minute); // closed

            Assert.That(service.Phase, Is.EqualTo(ServicePhase.Closed), "Not finished with people still in the street.");
        }

        [Test]
        public void WithoutWalkingInTheyAreAtTheDoorAtOnce()
        {
            var service = Service(seats: 3);
            service.GuestsWalkIn = false;
            service.Tick(13f * Minute);
            Assert.That(service.Customers.Single().State, Is.EqualTo(CustomerState.WalkingToSeat));
        }
    }
}
