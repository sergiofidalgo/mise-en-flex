using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Interact rules with pans, plates, hobs and the bin (Phase 1, milestone 2).</summary>
    public class ContainerTransferTests
    {
        private CookingSimulation _sim;
        private ItemSlot _hands;
        private ItemSlot _counter;

        private static readonly IngredientProperties SteakProperties = new IngredientProperties
        {
            TypeId = "Steak",
            HeatCapacity = 1050f,
            SurfaceFraction = 0.15f,
            BurntBrowning = 2f,
            StartTemperature = 4f,
        };

        private Ingredient NewSteak() => _sim.AddIngredient(SteakProperties);

        private Pan NewPan() => _sim.AddPan(new PanProperties { HeatCapacity = 750f, AirLossConductance = 7.7f, Capacity = 2 });

        [SetUp]
        public void CreateKitchen()
        {
            _sim = new CookingSimulation(22f);
            _hands = new ItemSlot();
            _counter = new ItemSlot();
        }

        [Test]
        public void FoodGoesIntoAPanStandingOnACounter()
        {
            var pan = NewPan();
            _counter.Put(pan);
            var steak = NewSteak();
            _hands.Put(steak);

            Assert.That(ItemTransfer.Interact(_hands, _counter), Is.EqualTo(InteractionOutcome.Placed));
            Assert.That(pan.Contents, Does.Contain(steak));
            Assert.That(_hands.IsEmpty);
        }

        [Test]
        public void EmptyHandsTakeAPanWithItsFood()
        {
            var pan = NewPan();
            var steak = NewSteak();
            pan.Put(steak);
            _counter.Put(pan);

            ItemTransfer.Interact(_hands, _counter);

            Assert.That(_hands.Current, Is.SameAs(pan));
            Assert.That(pan.Contents, Does.Contain(steak));
            Assert.That(_counter.IsEmpty);
        }

        // Decision 2026-11-02: pick the pan up with its food, plate it, put the hot pan back on the hob.
        [Test]
        public void PanToPlateToHobRoundTrip()
        {
            var hob = _sim.AddHob(new HobProperties { MaxPower = 2000f });
            var hobSlot = new HobSlot(hob);
            var pan = NewPan();
            hobSlot.Put(pan);
            var steak = NewSteak();
            pan.Put(steak);
            var plate = new Plate(3);
            _counter.Put(plate);

            Assert.That(ItemTransfer.Interact(_hands, hobSlot), Is.EqualTo(InteractionOutcome.PickedUp), "take the pan off the hob");
            Assert.That(hob.Pan, Is.Null);

            Assert.That(ItemTransfer.Interact(_hands, _counter), Is.EqualTo(InteractionOutcome.Combined), "plate the food");
            Assert.That(plate.Items, Does.Contain(steak));
            Assert.That(_hands.Current, Is.SameAs(pan), "the empty pan stays in hand");

            Assert.That(ItemTransfer.Interact(_hands, hobSlot), Is.EqualTo(InteractionOutcome.Placed), "put the pan back");
            Assert.That(hob.Pan, Is.SameAs(pan));
        }

        [Test]
        public void EmptyPanIsPickedUpWhole()
        {
            var pan = NewPan();
            _counter.Put(pan);

            ItemTransfer.Interact(_hands, _counter);

            Assert.That(_hands.Current, Is.SameAs(pan));
        }

        [Test]
        public void PlateIsCarriedWholeWithItsFood()
        {
            var plate = new Plate(3);
            plate.Put(NewSteak());
            _counter.Put(plate);

            ItemTransfer.Interact(_hands, _counter);

            Assert.That(_hands.Current, Is.SameAs(plate));
            Assert.That(plate.Items, Has.Count.EqualTo(1));
        }

        [Test]
        public void HeldPlateScoopsFoodOffACounter()
        {
            var steak = NewSteak();
            _counter.Put(steak);
            var plate = new Plate(3);
            _hands.Put(plate);

            Assert.That(ItemTransfer.Interact(_hands, _counter), Is.EqualTo(InteractionOutcome.PickedUp));
            Assert.That(plate.Items, Does.Contain(steak));
            Assert.That(_counter.IsEmpty);
        }

        [Test]
        public void HeldPlateAtAHobPlatesFoodFromThePan()
        {
            var hob = _sim.AddHob(new HobProperties { MaxPower = 2000f });
            var hobSlot = new HobSlot(hob);
            var pan = NewPan();
            hobSlot.Put(pan);
            var steak = NewSteak();
            pan.Put(steak);
            var plate = new Plate(3);
            _hands.Put(plate);

            Assert.That(ItemTransfer.Interact(_hands, hobSlot), Is.EqualTo(InteractionOutcome.Combined));
            Assert.That(plate.Items, Does.Contain(steak));
            Assert.That(pan.Contents, Is.Empty);
            Assert.That(steak.Pan, Is.Null);
            Assert.That(hob.Pan, Is.SameAs(pan), "The pan stays on the hob");
        }

        [Test]
        public void HeldPanPoursOntoAPlateOnACounter()
        {
            var plate = new Plate(3);
            _counter.Put(plate);
            var pan = NewPan();
            pan.Put(NewSteak());
            pan.Put(NewSteak());
            _hands.Put(pan);

            Assert.That(ItemTransfer.Interact(_hands, _counter), Is.EqualTo(InteractionOutcome.Combined));
            Assert.That(plate.Items, Has.Count.EqualTo(2));
            Assert.That(pan.Contents, Is.Empty);
        }

        [Test]
        public void HobOnlyTakesAPan()
        {
            var hobSlot = new HobSlot(_sim.AddHob(new HobProperties { MaxPower = 2000f }));
            _hands.Put(NewSteak());

            Assert.That(ItemTransfer.Interact(_hands, hobSlot), Is.EqualTo(InteractionOutcome.Nothing));
        }

        [Test]
        public void PanTakenOffTheHobLeavesTheHeat()
        {
            var hob = _sim.AddHob(new HobProperties { MaxPower = 2000f });
            var hobSlot = new HobSlot(hob);
            var pan = NewPan();
            hobSlot.Put(pan);

            ItemTransfer.Interact(_hands, hobSlot);

            Assert.That(_hands.Current, Is.SameAs(pan));
            Assert.That(hob.Pan, Is.Null);
        }

        [Test]
        public void BinDiscardsASingleItem()
        {
            var bin = new ItemSink();
            var discarded = new List<Item>();
            bin.Discarded += discarded.Add;
            var steak = NewSteak();
            _hands.Put(steak);

            Assert.That(ItemTransfer.Interact(_hands, bin), Is.EqualTo(InteractionOutcome.Placed));
            Assert.That(discarded, Is.EqualTo(new[] { steak }));
            Assert.That(_hands.IsEmpty);
        }

        [Test]
        public void ScrapingAPlateIntoTheBinKeepsThePlate()
        {
            var bin = new ItemSink();
            var discarded = new List<Item>();
            bin.Discarded += discarded.Add;
            var plate = new Plate(3);
            plate.Put(NewSteak());
            _hands.Put(plate);

            Assert.That(ItemTransfer.Interact(_hands, bin), Is.EqualTo(InteractionOutcome.Combined));
            Assert.That(discarded, Has.Count.EqualTo(1));
            Assert.That(_hands.Current, Is.SameAs(plate));
            Assert.That(plate.Items, Is.Empty);
        }

        [Test]
        public void EmptyPlateAtTheBinDoesNothing()
        {
            _hands.Put(new Plate(3));
            Assert.That(ItemTransfer.Interact(_hands, new ItemSink()), Is.EqualTo(InteractionOutcome.Nothing));
        }

        [Test]
        public void PlatesDontStackOrHoldPans()
        {
            var plate = new Plate(3);
            Assert.That(plate.CanAccept(new Plate(3)), Is.False);
            Assert.That(plate.CanAccept(NewPan()), Is.False);
        }

        [Test]
        public void PlateRefusesFoodBeyondCapacity()
        {
            var plate = new Plate(2);
            plate.Put(NewSteak());
            plate.Put(NewSteak());
            Assert.That(plate.CanAccept(NewSteak()), Is.False);
        }
    }
}
