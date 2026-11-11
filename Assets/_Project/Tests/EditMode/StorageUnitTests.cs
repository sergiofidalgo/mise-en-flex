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
            new StorageUnit.Slot(6),
            new StorageUnit.Slot(4),
            new StorageUnit.Slot(0),
            new StorageUnit.Slot(0));

        [SetUp]
        public void CreateKitchen() => _sim = new CookingSimulation(Ambient);

        [Test]
        public void StockFillsASlotThenTheNext()
        {
            var unit = NewUnit(Fridge);
            Assert.That(unit.Stock("Steak", () => New("Steak", Fridge), 7), Is.EqualTo(7));
            Assert.That(unit.Slots[0].Count, Is.EqualTo(6));
            Assert.That(unit.Slots[1].Count, Is.EqualTo(1));
        }

        [Test]
        public void StockStopsWhenThereIsNoRoom()
        {
            var unit = NewUnit(Fridge);
            Assert.That(unit.Stock("Steak", () => New("Steak", Fridge), 20), Is.EqualTo(10));
        }

        [Test]
        public void FullOnlyWhenEverySlotItHasIsAtCapacity()
        {
            var unit = NewUnit(Fridge);
            unit.Stock("Steak", () => New("Steak", Fridge), 6);
            unit.Stock("Onion", () => New("Onion", Fridge), 3);
            Assert.That(unit.IsFull, Is.False); // one onion's room left; the unused slots don't count

            unit.Stock("Onion", () => New("Onion", Fridge), 1);
            Assert.That(unit.IsFull, Is.True);

            unit.TryTake(1, out _);
            Assert.That(unit.IsFull, Is.False);
        }

        [Test]
        public void AUnitWithNoSlotsIsNeverFull()
        {
            Assert.That(new StorageUnit(Fridge, new StorageUnit.Slot(0)).IsFull, Is.False);
        }

        [Test]
        public void TakingFromASlotGivesThatIngredientAndReducesStock()
        {
            var unit = NewUnit(Fridge);
            unit.Stock("Onion", () => New("Onion"), 3);

            Assert.That(unit.TryTake(0, out var onion), Is.True);
            Assert.That(onion.TypeId, Is.EqualTo("Onion"));
            Assert.That(unit.Slots[0].Count, Is.EqualTo(2));
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
        public void PutsBackWithTheSameIngredientBeforeUsingAFreeSlot()
        {
            var unit = NewUnit(Fridge);
            unit.Put(New("Steak"));
            unit.Put(New("Onion"));
            var hands = new ItemSlot();
            hands.Put(New("Onion"));

            Assert.That(ItemTransfer.Interact(hands, unit), Is.EqualTo(InteractionOutcome.Placed));
            Assert.That(unit.Slots[1].Count, Is.EqualTo(2));
        }

        [Test]
        public void AnyIngredientFitsWhileASlotIsFree()
        {
            var unit = NewUnit(Fridge);
            unit.Put(New("Steak"));
            Assert.That(unit.CanAccept(New("Tomato")), Is.True);
            unit.Put(New("Tomato"));
            Assert.That(unit.CanAccept(New("Onion")), Is.False, "both slots are taken by something else");
        }

        [Test]
        public void AnEmptiedSlotIsFreeForAnythingAgain()
        {
            var unit = new StorageUnit(Fridge, new StorageUnit.Slot(1));
            unit.Put(New("Steak"));
            unit.TryTake(0, out _);
            Assert.That(unit.Slots[0].TypeId, Is.Null);
            Assert.That(unit.CanAccept(New("Onion")));
        }

        [Test]
        public void RefusesWhenItsSlotsAreFull()
        {
            var unit = NewUnit(Fridge);
            unit.Stock("Onion", () => New("Onion"), 10);
            Assert.That(unit.CanAccept(New("Onion")), Is.False);
        }

        [Test]
        public void MeatOnAShelfSpoilsFasterThanInAFridge()
        {
            var shelf = new StorageUnit(Ambient, new StorageUnit.Slot(1));
            var fridge = new StorageUnit(Fridge, new StorageUnit.Slot(1));
            var onShelf = New("Steak", Fridge);
            var inFridge = New("Steak", Fridge);
            shelf.Put(onShelf);
            fridge.Put(inFridge);

            _sim.SkipClosedTime(18f * Hour);

            Assert.That(onShelf.Freshness, Is.LessThan(inFridge.Freshness - 0.5f));
        }

        [Test]
        public void FridgeKeepsStockColdAndShelfDoesNot()
        {
            var fridge = new StorageUnit(Fridge, new StorageUnit.Slot(2));
            var shelf = new StorageUnit(Ambient, new StorageUnit.Slot(2));
            fridge.Stock("Steak", () => New("Steak", Ambient), 1);  // starts warm, should chill
            shelf.Stock("Steak", () => New("Steak", Fridge), 1);    // starts cold, should warm

            _sim.Tick(4f * Hour);

            fridge.TryTake(0, out var chilled);
            shelf.TryTake(0, out var warmed);
            Assert.That(chilled.CoreTemperature, Is.LessThan(8f));
            Assert.That(warmed.CoreTemperature, Is.GreaterThan(Ambient - 4f));
        }

        [Test]
        public void FridgeKeepsFoodFresherThanAShelf()
        {
            var fridge = new StorageUnit(Fridge, new StorageUnit.Slot(2));
            var shelf = new StorageUnit(Ambient, new StorageUnit.Slot(2));
            fridge.Stock("Steak", () => New("Steak", Fridge), 1);
            shelf.Stock("Steak", () => New("Steak", Ambient), 1);

            _sim.Tick(12f * Hour);

            fridge.TryTake(0, out var cold);
            shelf.TryTake(0, out var warm);
            Assert.That(cold.Freshness, Is.GreaterThan(warm.Freshness + 0.15f));
        }

        [Test]
        public void TakenFoodIsBackInKitchenAir()
        {
            var unit = NewUnit(Fridge);
            unit.Stock("Steak", () => New("Steak", Fridge), 1);
            unit.TryTake(0, out var steak);
            Assert.That(steak.EnvironmentTemperature, Is.Null);
        }

        // Cooked here means a doneness level reached: it starts above the level's peak core temperature.
        private Ingredient Cooked(string typeId) =>
            _sim.AddIngredient(new IngredientProperties
            {
                TypeId = typeId,
                HeatCapacity = 1050f,
                SurfaceFraction = 0.15f,
                SurfaceCoreConductance = 0.9f,
                AirConductance = 0.25f,
                StartTemperature = 55f,
                FreshnessLossPerHourAt20C = 1f / 24f,
                DonenessLevels = new[] { new DonenessLevel("Rare", 45f) },
            });

        [Test]
        public void CookedFoodGetsItsOwnSlotInsteadOfHidingBehindRawStock()
        {
            var unit = NewUnit(Fridge);
            unit.Stock("Steak", () => New("Steak", Fridge), 3);
            var cooked = Cooked("Steak");

            Assert.That(cooked.HasBeenCooked, Is.True);
            Assert.That(unit.CanAccept(cooked), Is.True);
            unit.Put(cooked);

            Assert.That(unit.SlotOf(cooked), Is.EqualTo(1));
            Assert.That(unit.Slots[0].HoldsCooked, Is.False);
            Assert.That(unit.Slots[1].HoldsCooked, Is.True);
            Assert.That(unit.TryTake(1, out var back), Is.True);
            Assert.That(back, Is.SameAs(cooked));
        }

        [Test]
        public void CookedFoodIsRefusedWhenOnlyRawSlotsHaveRoom()
        {
            var unit = new StorageUnit(Fridge, new StorageUnit.Slot(6));
            unit.Stock("Steak", () => New("Steak", Fridge), 2);

            Assert.That(unit.CanAccept(Cooked("Steak")), Is.False);
        }

        [Test]
        public void DeliveriesNeverJoinACookedSlot()
        {
            var unit = new StorageUnit(Fridge, new StorageUnit.Slot(6), new StorageUnit.Slot(6));
            unit.Put(Cooked("Steak"));

            Assert.That(unit.Stock("Steak", () => New("Steak", Fridge), 3), Is.EqualTo(3));
            Assert.That(unit.Slots[0].Count, Is.EqualTo(1));
            Assert.That(unit.Slots[1].Count, Is.EqualTo(3));
        }
    }
}
