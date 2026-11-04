using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Storage;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class StockTests
    {
        private const float Hour = 3600f;

        private static IngredientProperties Food(string typeId = "Steak") => new IngredientProperties
        {
            TypeId = typeId,
            HeatCapacity = 1050f,
            SurfaceFraction = 0.15f,
            StartTemperature = 4f,
            FreshnessLossPerHourAt20C = 1f / 24f,
        };

        private static CookingSimulation Sim() => new CookingSimulation(22f);

        [Test]
        public void ACrateHoldsOneKindUpToItsCapacity()
        {
            var sim = Sim();
            var crate = new Crate(2);
            Assert.That(crate.IsEmpty);
            crate.Put(sim.AddIngredient(Food()));
            Assert.That(crate.CanAccept(sim.AddIngredient(Food("Onion"))), Is.False, "one kind");
            Assert.That(crate.CanAccept(new Plate(3)), Is.False);
            crate.Put(sim.AddIngredient(Food()));
            Assert.That(crate.CanAccept(sim.AddIngredient(Food())), Is.False, "full");
        }

        [Test]
        public void ACrateEmptiesIntoStorageAcrossSlots()
        {
            var sim = Sim();
            var fridge = new StorageUnit(4f, new StorageUnit.Slot("Steak", 1), new StorageUnit.Slot("Steak", 1));
            var crate = new Crate(6);
            crate.Put(sim.AddIngredient(Food()));
            crate.Put(sim.AddIngredient(Food()));
            var hands = new ItemSlot();
            hands.Put(crate);

            Assert.That(ItemTransfer.Interact(hands, fridge), Is.EqualTo(InteractionOutcome.Combined));
            Assert.That(crate.IsEmpty);
            Assert.That(fridge.Slots[0].Count + fridge.Slots[1].Count, Is.EqualTo(2));
        }

        [Test]
        public void WhatDoesntFitStaysInTheCrate()
        {
            var sim = Sim();
            var fridge = new StorageUnit(4f, new StorageUnit.Slot("Steak", 1));
            var crate = new Crate(6);
            crate.Put(sim.AddIngredient(Food()));
            crate.Put(sim.AddIngredient(Food()));
            var hands = new ItemSlot();
            hands.Put(crate);

            ItemTransfer.Interact(hands, fridge);
            Assert.That(crate.Items, Has.Count.EqualTo(1));
            Assert.That(hands.Current, Is.SameAs(crate));
        }

        [Test]
        public void StockFitsOnlyUpToCapacity()
        {
            var sim = Sim();
            var storeroom = new StorageUnit(4f, new StorageUnit.Slot("Steak", 3));
            Assert.That(storeroom.Stock(0, () => sim.AddIngredient(Food()), 18), Is.EqualTo(3));
        }

        [Test]
        public void TheOldestComesOutFirst()
        {
            var sim = Sim();
            var fridge = new StorageUnit(4f, new StorageUnit.Slot("Steak", 3));
            var old = sim.AddIngredient(Food());
            fridge.Put(old);
            sim.Tick(Hour);
            fridge.Put(sim.AddIngredient(Food()));

            Assert.That(fridge.TryTake(0, out var taken));
            Assert.That(taken, Is.SameAs(old));
        }

        [Test]
        public void AColdNightAgesFoodLessThanAWarmOne()
        {
            var sim = Sim();
            var cold = sim.AddIngredient(Food());
            cold.EnvironmentTemperature = 4f;
            var warm = sim.AddIngredient(Food());

            sim.SkipClosedTime(18f * Hour);

            Assert.That(warm.Freshness, Is.LessThan(cold.Freshness));
            Assert.That(cold.Freshness, Is.LessThan(1f));
            Assert.That(cold.CoreTemperature, Is.EqualTo(4f));
            Assert.That(warm.CoreTemperature, Is.EqualTo(22f));
        }

        [Test]
        public void SkippingTheNightMatchesSteppingThroughIt()
        {
            // Stepped, once it's settled at fridge temperature, against worked out directly.
            var stepped = Sim();
            var a = stepped.AddIngredient(Food());
            a.EnvironmentTemperature = 4f;
            stepped.Tick(18f * Hour);

            var skipped = Sim();
            var b = skipped.AddIngredient(Food());
            b.EnvironmentTemperature = 4f;
            skipped.SkipClosedTime(18f * Hour);

            Assert.That(b.Freshness, Is.EqualTo(a.Freshness).Within(0.001f));
        }

        [Test]
        public void TheNightTurnsHobsOffAndCoolsPans()
        {
            var sim = Sim();
            var hob = sim.AddHob(new HobProperties { MaxPower = 2000f });
            var pan = sim.AddPan(new PanProperties { HeatCapacity = 750f, AirLossConductance = 7.7f, Capacity = 1 });
            hob.PlacePan(pan);
            hob.Dial = 1f;
            sim.Tick(120f);

            sim.SkipClosedTime(18f * Hour);

            Assert.That(hob.Dial, Is.EqualTo(0f));
            Assert.That(pan.Temperature, Is.EqualTo(22f));
        }

        [Test]
        public void TheSpoilageDialReachesTheNight()
        {
            var sim = new CookingSimulation(22f, spoilageMultiplier: 0f);
            var food = sim.AddIngredient(Food());
            sim.SkipClosedTime(18f * Hour);
            Assert.That(food.Freshness, Is.EqualTo(1f));
        }

        [Test]
        public void SpoiledStockGetsThrownOut()
        {
            var sim = Sim();
            var fridge = new StorageUnit(4f, new StorageUnit.Slot("Steak", 3));
            var old = sim.AddIngredient(Food());
            fridge.Put(old);
            sim.SkipClosedTime(200f * Hour);
            var fresh = sim.AddIngredient(Food());
            fridge.Put(fresh);

            var spoiled = fridge.RemoveSpoiled(0.1f);

            Assert.That(spoiled, Is.EquivalentTo(new[] { old }));
            Assert.That(fridge.Slots[0].Stock, Is.EquivalentTo(new[] { fresh }));
        }
    }
}
