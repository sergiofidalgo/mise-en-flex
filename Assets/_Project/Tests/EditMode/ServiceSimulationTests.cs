using System.Linq;
using Game.Simulation.Items;
using Game.Simulation.Quality;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class ServiceSimulationTests
    {
        private const float Minute = 60f;
        private const float Hour = 3600f;

        private static ServiceSettings Settings(float interval = 12f * Minute, float jitter = 0f, float rush = 1f, int queue = 3) => new ServiceSettings
        {
            DurationSeconds = 6f * Hour,
            ArrivalIntervalSeconds = interval,
            ArrivalJitter = jitter,
            RushStart = 0.35f,
            RushEnd = 0.6f,
            RushMultiplier = rush,
            EatSeconds = 15f * Minute,
            MaxQueue = queue,
            Seed = 7,
        };

        private static Plate PlateWithFood()
        {
            var plate = new Plate(3);
            plate.Put(new Item("Steak"));
            return plate;
        }

        // Ticks in one-minute steps, like a frame at 60x, and sits everyone down who reached their seat.
        private static void Run(ServiceSimulation service, float seconds)
        {
            for (var t = 0f; t < seconds; t += Minute)
            {
                service.Tick(Minute);
                foreach (var c in service.Customers.Where(c => c.State == CustomerState.WalkingToSeat))
                    service.SitDown(c);
            }
        }

        [Test]
        public void CustomersArriveAtTheConfiguredRate()
        {
            var service = new ServiceSimulation(Settings(interval: 12f * Minute), seatCount: 30);
            service.Tick(2f * Hour);
            Assert.That(service.Customers.Count, Is.EqualTo(10));
        }

        [Test]
        public void TheRushBringsCustomersFaster()
        {
            var calm = new ServiceSimulation(Settings(rush: 1f), seatCount: 99);
            var rush = new ServiceSimulation(Settings(rush: 2.5f), seatCount: 99);
            calm.Tick(6f * Hour);
            rush.Tick(6f * Hour);
            Assert.That(rush.Customers.Count, Is.GreaterThan(calm.Customers.Count + 5));
        }

        [Test]
        public void NobodyArrivesAfterClosing()
        {
            var service = new ServiceSimulation(Settings(), seatCount: 99);
            service.Tick(6f * Hour);
            var count = service.Customers.Count;
            service.Tick(2f * Hour);
            Assert.That(service.IsClosed);
            Assert.That(service.Customers.Count, Is.EqualTo(count));
        }

        [Test]
        public void ArrivalsTakeAFreeSeat()
        {
            var service = new ServiceSimulation(Settings(), seatCount: 3);
            service.Tick(13f * Minute);
            var first = service.Customers.Single();
            Assert.That(first.State, Is.EqualTo(CustomerState.WalkingToSeat));
            Assert.That(service.AtSeat(first.Seat), Is.SameAs(first));
        }

        [Test]
        public void WhenTheStoolsAreFullTheyQueueThenWalkAway()
        {
            var service = new ServiceSimulation(Settings(queue: 2), seatCount: 1);
            Run(service, 4f * 12f * Minute + Minute); // four arrivals, nobody served

            Assert.That(service.Queue, Has.Count.EqualTo(2));
            Assert.That(service.LostCovers, Is.EqualTo(1));
        }

        [Test]
        public void OnlyASeatedWaitingCustomerCanBeServed()
        {
            var service = new ServiceSimulation(Settings(), seatCount: 3);
            service.Tick(13f * Minute);
            var customer = service.Customers.Single();

            Assert.That(service.Serve(customer.Seat, PlateWithFood()), Is.False, "still walking to the stool");
            service.SitDown(customer);
            Assert.That(service.Serve(customer.Seat, new Plate(3)), Is.False, "an empty plate isn't a meal");
            Assert.That(service.Serve(customer.Seat, PlateWithFood()), Is.True);
            Assert.That(customer.State, Is.EqualTo(CustomerState.Eating));
        }

        [Test]
        public void ServedCustomersKeepTheirDishScoreAsSatisfaction()
        {
            var service = new ServiceSimulation(Settings(), seatCount: 3);
            Assert.That(service.AverageStars, Is.Null);
            Run(service, 2f * 12f * Minute + Minute); // two seated customers
            var first = service.AtSeat(0);
            var second = service.AtSeat(1);

            var great = new DishScore(System.Array.Empty<ItemScore>(), 0.9f);
            var poor = new DishScore(System.Array.Empty<ItemScore>(), 0.4f);
            service.Serve(0, PlateWithFood(), great);
            service.Serve(1, PlateWithFood(), poor);

            Assert.That(first.Satisfaction, Is.SameAs(great));
            Assert.That(second.Satisfaction, Is.SameAs(poor));
            Assert.That(service.AverageStars, Is.EqualTo((4.5f + 2f) / 2f));
        }

        [Test]
        public void EmptySeatCantBeServed()
        {
            var service = new ServiceSimulation(Settings(), seatCount: 3);
            Assert.That(service.Serve(0, PlateWithFood()), Is.False);
        }

        [Test]
        public void AfterEatingTheyLeaveAndFreeTheSeat()
        {
            var service = new ServiceSimulation(Settings(), seatCount: 3);
            Run(service, 13f * Minute);
            var customer = service.Customers.Single();
            service.Serve(customer.Seat, PlateWithFood());

            service.Tick(16f * Minute);

            Assert.That(customer.State, Is.EqualTo(CustomerState.Leaving));
            Assert.That(service.AtSeat(customer.Seat), Is.Not.SameAs(customer));
            service.Left(customer);
            Assert.That(customer.State, Is.EqualTo(CustomerState.Gone));
        }

        [Test]
        public void AFreedSeatGoesToTheFirstInTheQueue()
        {
            var service = new ServiceSimulation(Settings(), seatCount: 1);
            Run(service, 2f * 12f * Minute + Minute); // second customer queues
            var seated = service.AtSeat(0);
            var queued = service.Queue.Single();

            service.Serve(0, PlateWithFood());
            service.Tick(16f * Minute);

            Assert.That(service.AtSeat(0), Is.SameAs(queued));
            Assert.That(queued.State, Is.EqualTo(CustomerState.WalkingToSeat));
            Assert.That(seated.State, Is.EqualTo(CustomerState.Leaving));
        }

        [Test]
        public void TheSameSeedPlaysTheSameService()
        {
            var a = new ServiceSimulation(Settings(jitter: 0.35f), seatCount: 99);
            var b = new ServiceSimulation(Settings(jitter: 0.35f), seatCount: 99);
            a.Tick(6f * Hour);
            b.Tick(6f * Hour);
            Assert.That(a.Customers.Count, Is.EqualTo(b.Customers.Count));
        }

        [Test]
        public void BigAndSmallTicksGiveTheSameArrivals()
        {
            var small = new ServiceSimulation(Settings(jitter: 0.35f), seatCount: 99);
            var big = new ServiceSimulation(Settings(jitter: 0.35f), seatCount: 99);
            for (var i = 0; i < 360; i++)
                small.Tick(Minute);
            big.Tick(6f * Hour);
            Assert.That(small.Customers.Count, Is.EqualTo(big.Customers.Count));
        }
    }
}
