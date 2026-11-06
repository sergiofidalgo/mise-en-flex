using System.Linq;
using Game.Simulation.Items;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class CustomerIndicatorTests
    {
        private static ServiceSimulation Service(int seats = 1) => new ServiceSimulation(new ServiceSettings
        {
            ArrivalIntervalSeconds = 60f,
            ArrivalJitter = 0f,
            RushMultiplier = 1f,
            DurationSeconds = 100000f,
            LastOrdersSeconds = 0f,
            PatienceSeconds = 1000f,
            EatSeconds = 100f,
            MaxQueue = 3,
        }, seats);

        private static Plate Meal()
        {
            var plate = new Plate(3);
            plate.Put(new Item("Steak"));
            return plate;
        }

        [Test]
        public void ItFollowsAVisitFromStartToEnd()
        {
            var service = Service();
            service.Tick(61f);
            var customer = service.Customers[0];
            Assert.That(CustomerIndicator.KindOf(customer), Is.EqualTo(CustomerIndicatorKind.Someone), "walking in");

            service.SitDown(customer);
            Assert.That(CustomerIndicator.KindOf(customer), Is.EqualTo(CustomerIndicatorKind.WaitingForFood));

            service.Serve(customer.Seat, Meal());
            Assert.That(CustomerIndicator.KindOf(customer), Is.EqualTo(CustomerIndicatorKind.Eating));

            service.Tick(101f);
            Assert.That(CustomerIndicator.KindOf(customer), Is.EqualTo(CustomerIndicatorKind.Leaving));
        }

        [Test]
        public void SomeoneQueueingIsJustSomeone()
        {
            var service = Service();
            service.Tick(61f);
            service.Tick(60f);
            Assert.That(CustomerIndicator.KindOf(service.Customers.Last()), Is.EqualTo(CustomerIndicatorKind.Someone));
        }

        [Test]
        public void SomeoneWhoWalkedOutIsUnhappy()
        {
            var service = Service();
            service.Tick(61f);
            var customer = service.Customers[0];
            service.GiveUp(customer);
            Assert.That(CustomerIndicator.MoodOf(customer, 4f, 2.5f), Is.EqualTo(CustomerMood.Unhappy));
        }
    }
}
