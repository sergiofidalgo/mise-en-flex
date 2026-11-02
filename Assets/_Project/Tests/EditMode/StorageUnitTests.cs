using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Storage;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class StorageUnitTests
    {
        private const float Ambient = 22f;
        private const float Fridge = 4f;
        private const float Hour = 3600f;

        private CookingSimulation _sim;

        private Ingredient New(string typeId, float temperature = Ambient) =>
            _sim.AddIngredient(new IngredientProperties
            {
                TypeId = typeId,
                HeatCapacity = 1050f,
                SurfaceFraction = 0.15f,
                SurfaceCoreConductance = 0.9f,
                AirConductance = 0.25f,
                StartTemperature = temperature,
                FreshnessLossPerHourAt20C = 1f / 24f,
            });

        private StorageUnit NewUnit(float temperature) => new StorageUnit(
            temperature,
            new StorageUnit.Slot("Steak", 6),
            new StorageUnit.Slot("Onion", 4),
            new StorageUnit.Slot(null, 0),
            new StorageUnit.Slot(null, 0));

        [SetUp]
        public void CreateKitchen() => _sim = new CookingSimulation(Ambient);

        [Test]
        public void StockFillsUpToCapacity()
        {
            var unit = NewUnit(Fridge);
            Assert.That(unit.Stock(0, () => New("Steak", Fridge), 10), Is.EqualTo(6));
            Assert.That(unit.Slots[0].Count, Is.EqualTo(6));
        }

        [Test]
        public void TakingFromASlotGivesThatIngredientAndReducesStock()
        {
            var unit = NewUnit(Fridge);
            unit.Stock(1, () => New("Onion"), 3);

            Assert.That(unit.TryTake(1, out var onion), Is.True);
            Assert.That(onion.TypeId, Is.EqualTo("Onion"));
            Assert.That(unit.Slots[1].Count, Is.EqualTo(2));
        }

        [Test]
        public void EmptyOrUnusedSlotGivesNothing()
        {
            var unit = NewUnit(Fridge);
            Assert.That(unit.TryTake(0, out _), Is.False);
            Assert.That(unit.TryTake(3, out _), Is.False);
        }

        [Test]
        public void TakesTheOldestPortionFirst()
        {
            var unit = NewUnit(Fridge);
            var first = New("Steak", Fridge);
            var second = New("Steak", Fridge);
            unit.Put(first);
            unit.Put(second);

            unit.TryTake(0, out var taken);
            Assert.That(taken, Is.SameAs(first));
        }

        [Test]
        public void PutsBackIntoTheMatchingSlotWithRoom()
        {
            var unit = NewUnit(Fridge);
            var hands = new ItemSlot();
            hands.Put(New("Onion"));

            Assert.That(ItemTransfer.Interact(hands, unit), Is.EqualTo(InteractionOutcome.Placed));
            Assert.That(unit.Slots[1].Count, Is.EqualTo(1));
        }

        [Test]
        public void RefusesIngredientsWithoutASlot()
        {
            Assert.That(NewUnit(Fridge).CanAccept(New("Tomato")), Is.False);
        }

        [Test]
        public void RefusesWhenTheSlotIsFull()
        {
            var unit = NewUnit(Fridge);
            unit.Stock(1, () => New("Onion"), 4);
            Assert.That(unit.CanAccept(New("Onion")), Is.False);
        }

        [Test]
        public void FridgeKeepsStockColdAndShelfDoesNot()
        {
            var fridge = new StorageUnit(Fridge, new StorageUnit.Slot("Steak", 2));
            var shelf = new StorageUnit(Ambient, new StorageUnit.Slot("Steak", 2));
            fridge.Stock(0, () => New("Steak", Ambient), 1);  // starts warm, should chill
            shelf.Stock(0, () => New("Steak", Fridge), 1);    // starts cold, should warm

            _sim.Tick(4f * Hour);

            fridge.TryTake(0, out var chilled);
            shelf.TryTake(0, out var warmed);
            Assert.That(chilled.CoreTemperature, Is.LessThan(8f));
            Assert.That(warmed.CoreTemperature, Is.GreaterThan(Ambient - 4f));
        }

        [Test]
        public void FridgeKeepsFoodFresherThanAShelf()
        {
            var fridge = new StorageUnit(Fridge, new StorageUnit.Slot("Steak", 2));
            var shelf = new StorageUnit(Ambient, new StorageUnit.Slot("Steak", 2));
            fridge.Stock(0, () => New("Steak", Fridge), 1);
            shelf.Stock(0, () => New("Steak", Ambient), 1);

            _sim.Tick(12f * Hour);

            fridge.TryTake(0, out var cold);
            shelf.TryTake(0, out var warm);
            Assert.That(cold.Freshness, Is.GreaterThan(warm.Freshness + 0.15f));
        }

        [Test]
        public void TakenFoodIsBackInKitchenAir()
        {
            var unit = NewUnit(Fridge);
            unit.Stock(0, () => New("Steak", Fridge), 1);
            unit.TryTake(0, out var steak);
            Assert.That(steak.EnvironmentTemperature, Is.Null);
        }
    }
}
