using System;
using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Quality;
using Game.Simulation.Saving;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Delivery orders, couriers and boxes (decision 2026-11-12).</summary>
    public class DeliveryTests
    {
        private const float Minute = 60f;
        private const float Hour = 3600f;

        private static readonly DonenessLevel Medium = new DonenessLevel("Medium", 57f, 60f);

        private static ServiceSettings Settings(float orderEvery = 30f * Minute, float lead = 20f * Minute, float patience = 10f * Minute,
            float ride = 15f * Minute, float commission = 0.25f, string side = null) => new ServiceSettings
        {
            DurationSeconds = 6f * Hour,
            LastOrdersSeconds = 30f * Minute,
            ArrivalIntervalSeconds = 100f * Hour, // nobody at the door: these tests are about the app
            ArrivalJitter = 0f,
            RushMultiplier = 1f,
            Seed = 5,
            Menu = new OrderMenu
            {
                MainTypeId = "Steak",
                MainName = "Steak",
                MainPrice = 24f,
                Choices = new[] { new OrderChoice(Medium, 1f) },
                SideTypeId = side,
                SideName = side,
                SidePrice = 4f,
                SideChance = side != null ? 1f : 0f,
            },
            Delivery = new DeliverySettings
            {
                OrderIntervalSeconds = orderEvery,
                OrderJitter = 0f,
                CourierLeadSeconds = lead,
                CourierPatienceSeconds = patience,
                RideSeconds = ride,
                Commission = commission,
            },
        };

        private static ServiceSimulation Service(ServiceSettings settings, int slots)
        {
            var service = new ServiceSimulation(settings, seatCount: 0);
            service.Deliveries.OpenSlots(slots);
            return service;
        }

        private static void Run(ServiceSimulation service, float seconds)
        {
            for (var t = 0f; t < seconds; t += Minute)
                service.Tick(Minute);
        }

        private static Plate BoxOfFood()
        {
            var box = Plate.NewBox(3);
            box.Put(new Item("Steak"));
            return box;
        }

        private static DishScore Score(float overall, Order order, params string[] missing) =>
            new DishScore(Array.Empty<ItemScore>(), overall, order, missing);

        // Runs until the first order's courier stands at the counter.
        private static DeliveryOrder CourierWaiting(ServiceSimulation service)
        {
            Run(service, 30f * Minute);
            var order = service.Deliveries.Orders[0];
            Run(service, order.LeadSeconds);
            service.Deliveries.CourierAtCounter(order);
            return order;
        }

        [Test]
        public void WithoutADeliveryCounterNoOrdersCome()
        {
            var service = Service(Settings(), slots: 0);
            Run(service, 5f * Hour);
            Assert.That(service.Deliveries.Orders, Is.Empty);
        }

        [Test]
        public void AnOrderClaimsAFreePlaceAndTheCourierComesAfterTheLead()
        {
            var service = Service(Settings(), slots: 2);
            var pinged = new List<DeliveryOrder>();
            var due = new List<DeliveryOrder>();
            service.Deliveries.Ordered += pinged.Add;
            service.Deliveries.CourierDue += due.Add;

            Run(service, 30f * Minute);
            Assert.That(pinged, Has.Count.EqualTo(1));
            var order = pinged[0];
            Assert.That(order.State, Is.EqualTo(DeliveryState.Ordered));
            Assert.That(service.Deliveries.AtSlot(order.Slot), Is.SameAs(order));
            Assert.That(order.CountdownLeft, Is.EqualTo(1f).Within(0.06f));
            Assert.That(due, Is.Empty);

            Run(service, 10f * Minute);
            Assert.That(order.CountdownLeft, Is.EqualTo(0.5f).Within(0.06f));

            Run(service, 10f * Minute);
            Assert.That(due, Is.EqualTo(new[] { order }));
            Assert.That(order.State, Is.EqualTo(DeliveryState.CourierComing));
        }

        [Test]
        public void EveryPlaceTakenMeansNoMoreOrdersUntilOneIsFree()
        {
            // Couriers who never reach the counter: both places stay claimed.
            var service = Service(Settings(orderEvery: 10f * Minute), slots: 2);
            Run(service, 3f * Hour);
            Assert.That(service.Deliveries.Orders, Has.Count.EqualTo(2));
            Assert.That(service.Deliveries.Orders.Select(o => o.Slot), Is.EquivalentTo(new[] { 0, 1 }));
        }

        [Test]
        public void TheCourierPaysThePriceLessTheCommissionAndNoTip()
        {
            var service = Service(Settings(commission: 0.25f), slots: 1);
            var order = CourierWaiting(service);

            Assert.That(service.Deliveries.Collect(order, BoxOfFood(), Score(1f, order.Order)));
            Assert.That(order.State, Is.EqualTo(DeliveryState.OutForDelivery));
            Assert.That(order.Payment, Is.EqualTo(18f));
            Assert.That(service.Money, Is.EqualTo(18f));
            Assert.That(service.Tips, Is.EqualTo(0f));
            Assert.That(service.Deliveries.AtSlot(order.Slot), Is.Null, "its place is free for the next order");
        }

        [Test]
        public void TheCourierDoesNotLookInTheBox()
        {
            var service = Service(Settings(), slots: 1);
            var order = CourierWaiting(service);
            // A terrible dish pays the same at the counter; the customer has the last word.
            Assert.That(service.Deliveries.Collect(order, BoxOfFood(), Score(0.1f, order.Order)));
            Assert.That(order.Payment, Is.EqualTo(18f));
        }

        [Test]
        public void NothingIsTakenBeforeTheCourierIsAtTheCounter()
        {
            var service = Service(Settings(), slots: 1);
            Run(service, 30f * Minute);
            var order = service.Deliveries.Orders[0];
            Assert.That(service.Deliveries.Collect(order, BoxOfFood(), Score(1f, order.Order)), Is.False);

            Run(service, order.LeadSeconds);
            service.Deliveries.CourierAtCounter(order);
            Assert.That(service.Deliveries.Collect(order, Plate.NewBox(3), Score(1f, order.Order)), Is.False, "an empty box");
        }

        [Test]
        public void TheReviewComesAfterTheRideAndIsMarkedAsADelivery()
        {
            var service = Service(Settings(ride: 15f * Minute), slots: 1);
            var order = CourierWaiting(service);
            service.Deliveries.Collect(order, BoxOfFood(), Score(1f, order.Order));

            Run(service, 10f * Minute);
            Assert.That(service.Reviews, Is.Empty, "still on the road");

            Run(service, 6f * Minute);
            Assert.That(order.State, Is.EqualTo(DeliveryState.Delivered));
            Assert.That(service.Reviews, Has.Count.EqualTo(1));
            var review = service.Reviews[0];
            Assert.That(review.Delivery);
            Assert.That(review.Stars, Is.EqualTo(5f));
            Assert.That(review.Customer, Is.SameAs(order.Recipient));
            Assert.That(SaveConversions.ToRecord(review, 3).Delivery);
            Assert.That(SaveConversions.ToRecord(review, 3).Paid, Is.EqualTo(18f));
        }

        [Test]
        public void SomethingMissingIsRefundedWhenItArrives()
        {
            var service = Service(Settings(side: "Onion"), slots: 1);
            var order = CourierWaiting(service);
            service.Deliveries.Collect(order, BoxOfFood(), Score(0.6f, order.Order, "Onion"));
            Assert.That(service.Money, Is.EqualTo(21f), "(24 + 4) less a quarter");

            Run(service, 16f * Minute);
            Assert.That(order.Refund, Is.EqualTo(3f), "the side's 4, less the commission never paid on it");
            Assert.That(service.Money, Is.EqualTo(18f));
        }

        [Test]
        public void ACourierKeptWaitingLeavesAndTheOrderIsCancelled()
        {
            var service = Service(Settings(patience: 10f * Minute), slots: 1);
            var cancelled = new List<DeliveryOrder>();
            service.Deliveries.Cancelled += cancelled.Add;
            var order = CourierWaiting(service);

            Run(service, 5f * Minute);
            Assert.That(order.Patience, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(order.State, Is.EqualTo(DeliveryState.CourierWaiting));

            Run(service, 6f * Minute);
            Assert.That(cancelled, Is.EqualTo(new[] { order }));
            Assert.That(order.State, Is.EqualTo(DeliveryState.Cancelled));
            Assert.That(service.Money, Is.EqualTo(0f), "nothing paid, nothing fined");
            Assert.That(service.Deliveries.AtSlot(order.Slot), Is.Not.SameAs(order), "its place is free for the next order");

            Assert.That(service.Reviews, Has.Count.EqualTo(1));
            Assert.That(service.Reviews[0].Stars, Is.EqualTo(1f));
            Assert.That(service.Reviews[0].Delivery);
            Assert.That(service.Reviews[0].Line, Is.Not.Empty);
        }

        [Test]
        public void TheCouriersPatienceOnlyRunsAtTheCounter()
        {
            var service = Service(Settings(), slots: 1);
            Run(service, 30f * Minute);
            var order = service.Deliveries.Orders[0];
            Run(service, order.LeadSeconds + 30f * Minute); // pulled up, never reached the counter
            Assert.That(order.Patience, Is.EqualTo(1f));
        }

        [Test]
        public void AWaitAtTheCounterReachesTheReviewAsALateDelivery()
        {
            var service = Service(Settings(patience: 10f * Minute), slots: 1);
            var order = CourierWaiting(service);
            Run(service, 9f * Minute);
            service.Deliveries.Collect(order, BoxOfFood(), Score(1f, order.Order));
            Assert.That(order.Recipient.PatienceWhenServed, Is.EqualTo(0.1f).Within(0.02f));

            Run(service, 16f * Minute);
            Assert.That(service.Reviews[0].Topic, Is.EqualTo(ReviewTopic.SlowService));
        }

        [Test]
        public void NoNewOrdersAfterLastOrdersAndTheServiceWaitsForTheRoad()
        {
            var settings = Settings(orderEvery: 60f * Minute, lead: 20f * Minute, patience: 60f * Minute, ride: 15f * Minute);
            settings.DurationSeconds = 100f * Minute; // last orders at 70: one order, at 60
            var service = Service(settings, slots: 2);
            Run(service, 81f * Minute);
            Assert.That(service.Deliveries.Orders, Has.Count.EqualTo(1));
            var order = service.Deliveries.Orders[0];
            service.Deliveries.CourierAtCounter(order);
            Run(service, 14f * Minute);
            service.Deliveries.Collect(order, BoxOfFood(), Score(1f, order.Order));

            Run(service, 10f * Minute);
            Assert.That(service.IsClosed);
            Assert.That(service.Phase, Is.EqualTo(ServicePhase.Closed), "a delivery is still on the road");

            Run(service, 6f * Minute);
            Assert.That(service.Phase, Is.EqualTo(ServicePhase.Finished));
            Assert.That(service.Deliveries.Orders, Has.Count.EqualTo(1));
        }

        [Test]
        public void DeliveriesDoNotMoveTheGuestsAtTheDoor()
        {
            ServiceSettings WithGuests()
            {
                var settings = Settings(orderEvery: 20f * Minute);
                settings.ArrivalIntervalSeconds = 12f * Minute;
                settings.ArrivalJitter = 0.35f;
                return settings;
            }

            var without = Service(WithGuests(), slots: 0);
            var with = Service(WithGuests(), slots: 2);
            Run(without, 3f * Hour);
            Run(with, 3f * Hour);

            Assert.That(with.Deliveries.Orders, Is.Not.Empty);
            Assert.That(with.Customers.Select(c => c.ArrivedAt), Is.EqualTo(without.Customers.Select(c => c.ArrivedAt)));
            Assert.That(with.Customers.Select(c => c.Order.Doneness.Name), Is.EqualTo(without.Customers.Select(c => c.Order.Doneness.Name)));
        }

        [Test]
        public void ABoxIsAPlateThatStaysOutOfTheDiningRoom()
        {
            var box = BoxOfFood();
            Assert.That(box.IsBox);
            Assert.That(box.TypeId, Is.EqualTo(Plate.BoxTypeId));
            Assert.That(new PassCounter().CanAccept(box), Is.False, "nobody eats from a box at a table");
            Assert.That(new PlateStack(0, 3).CanAccept(Plate.NewBox(3)), Is.False, "not among the clean plates");
            Assert.That(new Plate(3).IsBox, Is.False);
        }

        [Test]
        public void ThePileOfBoxesNeverRunsOutAndBoxesFoodInHand()
        {
            var pile = new BoxPile(3);
            var hands = new ItemSlot();
            for (var i = 0; i < 50; i++)
                Assert.That(pile.Take(), Is.InstanceOf<Plate>());

            // Food in hand at the pile goes into a fresh box, and the chef holds the box.
            hands.Put(new Item("Steak"));
            Assert.That(ItemTransfer.Interact(hands, pile), Is.EqualTo(InteractionOutcome.PickedUp));
            var box = hands.Current as Plate;
            Assert.That(box, Is.Not.Null);
            Assert.That(box.IsBox);
            Assert.That(box.Items.Select(i => i.TypeId), Is.EqualTo(new[] { "Steak" }));

            // A full box doesn't go back; an empty one does; a plate never.
            Assert.That(pile.CanAccept(box), Is.False);
            Assert.That(pile.CanAccept(Plate.NewBox(3)));
            Assert.That(pile.CanAccept(new Plate(3)), Is.False);
        }

        [Test]
        public void ABoxComesBackFromASaveAsABox()
        {
            var records = new List<ItemRecord>();
            ItemRecords.Write(Plate.NewBox(3), station: 2, slot: 0, records);
            ItemRecords.Write(new Plate(3), station: 3, slot: 0, records);
            var back = ItemRecords.Read(records, new PlainFactory());
            Assert.That(((Plate)back[0].Item).IsBox);
            Assert.That(((Plate)back[1].Item).IsBox, Is.False);
        }

        [Test]
        public void FoodCarriedAwayCoolsAsItWouldSittingOutWhileTheKitchenStandsStill()
        {
            IngredientProperties Steak() => new IngredientProperties
            {
                TypeId = "Steak",
                HeatCapacity = 1050f,
                SurfaceFraction = 0.15f,
                SurfaceCoreConductance = 0.9f,
                AirConductance = 0.25f,
                StartTemperature = 60f,
                FreshnessLossPerHourAt20C = 1f / 24f,
            };

            var sim = new CookingSimulation(22f);
            var carried = sim.AddIngredient(Steak());
            var bystander = sim.AddIngredient(Steak());
            sim.CarryAway(carried, 240f);
            Assert.That(carried.AverageTemperature, Is.LessThan(59f));
            Assert.That(bystander.AverageTemperature, Is.EqualTo(60f), "nothing else moves");
            Assert.That(sim.ElapsedSeconds, Is.EqualTo(0d));

            // The same as sitting out for that long.
            var kitchen = new CookingSimulation(22f);
            var sitting = kitchen.AddIngredient(Steak());
            kitchen.Tick(240f);
            Assert.That(sitting.AverageTemperature, Is.EqualTo(carried.AverageTemperature).Within(1e-3f));

            // A shorter ride (a box's share of it) leaves it warmer.
            var boxed = new CookingSimulation(22f).AddIngredient(Steak());
            new CookingSimulation(22f).CarryAway(boxed, 240f * 0.35f);
            Assert.That(boxed.AverageTemperature, Is.GreaterThan(carried.AverageTemperature));
        }

        private sealed class PlainFactory : IItemFactory
        {
            public Ingredient Ingredient(string typeId) => null;
            public Pan Pan() => null;
            public Plate Plate() => new Plate(3);
            public Tray Tray() => new Tray(4, 1f);
            public FoodContainer Container() => new FoodContainer(4, 1f);
            public Item Plain(string typeId) => new Item(typeId);
            public void Discard(Item item) { }
        }
    }
}
