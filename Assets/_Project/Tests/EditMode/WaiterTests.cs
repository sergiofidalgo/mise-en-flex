using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Service;
using Game.Simulation.Staff;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class WaiterTests
    {
        private static readonly DonenessLevel Rare = new DonenessLevel("Rare", 45f, 50f);
        private static readonly DonenessLevel WellDone = new DonenessLevel("Well done", 65f, 70f);

        private sealed class Place : IServingPlace
        {
            public int SeatIndex { get; set; }
            public PassCounter Slot { get; } = new PassCounter();
        }

        private CookingSimulation _cooking;
        private ServiceSimulation _service;
        private ReadyBell _bell;
        private ItemSlot _counter;
        private ItemSlot _spareCounter;
        private Place _place;
        private DirtyPile _pile;
        private Sink _sink;
        private StaffMember _member;
        private Waiter _waiter;
        private readonly List<Item> _gone = new List<Item>();

        private void Hire(float skill = 1f)
        {
            _member = new StaffMember(new Candidate("Ana", StaffRole.Waiter, "steady", skill, 1f, 40f));
            _waiter = new Waiter(_member, _bell, new[] { _place }, new[] { _counter, _spareCounter }, _pile, _sink, () => _service, _cooking,
                new StaffSettings { FatiguePerJob = 0f, FatigueMistakes = 0f, MoraleMistakeEffect = 0f }, 1);
            _waiter.ItemGone += _gone.Add;
        }

        [SetUp]
        public void SetUp()
        {
            _gone.Clear();
            _cooking = new CookingSimulation(22f);
            _service = new ServiceSimulation(new ServiceSettings
            {
                ArrivalIntervalSeconds = 60f,
                ArrivalJitter = 0f,
                RushMultiplier = 1f,
                DurationSeconds = 100000f,
                LastOrdersSeconds = 0f,
                PatienceSeconds = 1000f,
                EatSeconds = 100f,
                MaxQueue = 0,
                Menu = new OrderMenu { MainTypeId = "Steak", Choices = new[] { new OrderChoice(Rare, 1f) } },
            }, 1);
            _bell = new ReadyBell();
            _counter = new ItemSlot();
            _spareCounter = new ItemSlot();
            _place = new Place { SeatIndex = 0 };
            _pile = new DirtyPile(6);
            _sink = new Sink(2);
            Hire();
        }

        private Customer Seated()
        {
            _service.Tick(61f);
            var customer = _service.Customers.Last();
            _service.SitDown(customer);
            return customer;
        }

        private Plate Dish(float peakCore = 50f)
        {
            var steak = _cooking.AddIngredient(new IngredientProperties
            {
                TypeId = "Steak",
                HeatCapacity = 1050f,
                SurfaceFraction = 0.15f,
                DonenessLevels = new[] { Rare, WellDone },
                StartTemperature = peakCore,
            });
            var plate = new Plate(3);
            plate.Put(steak);
            return plate;
        }

        private Plate RingFor(Plate plate)
        {
            _counter.Put(plate);
            _bell.Ring(_counter);
            return plate;
        }

        [Test]
        public void WithNothingToDoTheyWait()
        {
            _waiter.Tick(1f);
            Assert.That(_waiter.Task, Is.EqualTo(WaiterTask.Idle));
            Assert.That(_waiter.Target, Is.Null);
        }

        [Test]
        public void APlateNobodyRangForStays()
        {
            Seated();
            _counter.Put(Dish());
            _waiter.Tick(1f);
            Assert.That(_waiter.Task, Is.EqualTo(WaiterTask.Idle), "it might still need its onions");
        }

        [Test]
        public void TheyCarryARungPlateToItsDiner()
        {
            Seated();
            var plate = RingFor(Dish());

            _waiter.Tick(1f);
            Assert.That(_waiter.Target, Is.SameAs(_counter));
            _waiter.Arrived();
            Assert.That(_waiter.Carried, Is.SameAs(plate));
            Assert.That(_counter.IsEmpty, Is.True);

            _waiter.Tick(1f);
            Assert.That(_waiter.Target, Is.SameAs(_place.Slot));
            _waiter.Arrived();
            Assert.That(_place.Slot.Current, Is.SameAs(plate));
            Assert.That(_waiter.Carried, Is.Null);
            Assert.That(_member.JobsToday, Is.EqualTo(1));
        }

        [Test]
        public void ADishNobodyOrderedStaysOnTheCounter()
        {
            Seated();
            RingFor(Dish(peakCore: 70f)); // well done, and they want it rare
            _waiter.Tick(1f);
            Assert.That(_waiter.Task, Is.EqualTo(WaiterTask.Idle));
            Assert.That(_bell.IsRung(_counter), Is.True, "still waiting");
        }

        [Test]
        public void NoDinerNoTrip()
        {
            RingFor(Dish());
            _waiter.Tick(1f);
            Assert.That(_waiter.Task, Is.EqualTo(WaiterTask.Idle));
        }

        [Test]
        public void ADirtyPlateGoesBackToThePile()
        {
            var dirty = new Plate(3);
            Clear(dirty);

            _waiter.Tick(1f);
            Assert.That(_waiter.Target, Is.SameAs(_place.Slot));
            _waiter.Arrived();
            Assert.That(_waiter.Carried, Is.SameAs(dirty));
            _waiter.Tick(1f);
            Assert.That(_waiter.Target, Is.SameAs(_pile));
            _waiter.Arrived();
            Assert.That(_pile.Count, Is.EqualTo(1));
            Assert.That(_waiter.Carried, Is.Null);
        }

        // Puts a finished, dirty plate at the place, as after a meal.
        private void Clear(Plate plate)
        {
            plate.Put(new Item("Crumbs"));
            _place.Slot.Put(plate);
            _place.Slot.StartEating();
            _place.Slot.FinishEating();
        }

        [Test]
        public void ServingComesBeforeClearing()
        {
            var other = new Place { SeatIndex = 1 };
            _service.OpenSeats(new[] { 1, 1 });
            _waiter = new Waiter(_member, _bell, new IServingPlace[] { other, _place }, new[] { _counter }, _pile, _sink, () => _service, _cooking, new StaffSettings(), 1);
            Seated();
            Clear2(other);
            RingFor(Dish());

            _waiter.Tick(1f);
            Assert.That(_waiter.Task, Is.EqualTo(WaiterTask.ToPlate));
        }

        private static void Clear2(Place place)
        {
            var plate = new Plate(3);
            plate.Put(new Item("Crumbs"));
            place.Slot.Put(plate);
            place.Slot.StartEating();
            place.Slot.FinishEating();
        }

        [Test]
        public void AFullPileWaits()
        {
            var full = new DirtyPile(0);
            _waiter = new Waiter(_member, _bell, new[] { _place }, new[] { _counter }, full, _sink, () => _service, _cooking, new StaffSettings(), 1);
            Clear2(_place);
            _waiter.Tick(1f);
            Assert.That(_waiter.Task, Is.EqualTo(WaiterTask.Idle), "nowhere to bring it");
        }

        [Test]
        public void IfTheDinerHasGoneThePlateGoesBackOnACounterAndRings()
        {
            var customer = Seated();
            var plate = RingFor(Dish());
            _waiter.Tick(1f);
            _waiter.Arrived();

            _service.Tick(1100f); // they lose patience on the way
            Assert.That(customer.State, Is.EqualTo(CustomerState.Leaving));

            _waiter.Tick(1f);
            Assert.That(_waiter.Task, Is.EqualTo(WaiterTask.ToCounter));
            _waiter.Arrived();
            Assert.That(_counter.Current, Is.SameAs(plate));
            Assert.That(_waiter.Carried, Is.Null);
            Assert.That(_bell.IsRung(_counter), Is.True);
        }

        [Test]
        public void IfAPlayerGetsThereFirstTheyLeaveIt()
        {
            Seated();
            RingFor(Dish());
            _waiter.Tick(1f);
            _counter.Take();
            _waiter.Arrived();
            Assert.That(_waiter.Task, Is.EqualTo(WaiterTask.Idle));
            Assert.That(_waiter.Carried, Is.Null);
        }

        [Test]
        public void ADroppedPlateBreaksAndTheFoodIsGone()
        {
            Hire(skill: 0f);
            Seated();
            var plate = RingFor(Dish());
            var steak = (Ingredient)plate.Items[0];

            _waiter.Tick(1f);
            _waiter.Arrived();

            Assert.That(_waiter.Carried, Is.Null);
            Assert.That(_cooking.Contains(steak), Is.False);
            Assert.That(_gone, Does.Contain(plate));
            Assert.That(_sink.Broken, Is.EqualTo(1), "replaced in the morning");
            Assert.That(_member.MistakesToday, Is.EqualTo(1));
        }

        [Test]
        public void LetGoWhileCarryingFoodItGoesOnACounter()
        {
            Seated();
            var plate = RingFor(Dish());
            _waiter.Tick(1f);
            _waiter.Arrived();

            _waiter.Release();
            Assert.That(_waiter.Carried, Is.Null);
            Assert.That(_counter.Current, Is.SameAs(plate));
        }

        [Test]
        public void TheBellCancelsWhenThePlateIsTaken()
        {
            RingFor(Dish());
            Assert.That(_bell.IsRung(_counter), Is.True);
            _counter.Take();
            Assert.That(_bell.IsRung(_counter), Is.False);
            Assert.That(_bell.Waiting, Is.Empty);
        }

        [Test]
        public void TheBellOnlyRingsForAFinishedPlate()
        {
            Assert.That(_bell.Ring(_counter), Is.False, "nothing there");
            _counter.Put(new Plate(3));
            Assert.That(_bell.Ring(_counter), Is.False, "nothing on it");
            _counter.Take();
            _counter.Put(Dish());
            Assert.That(_bell.Ring(_counter), Is.True);
            Assert.That(_bell.Ring(_counter), Is.False, "already rung");
        }
    }
}
