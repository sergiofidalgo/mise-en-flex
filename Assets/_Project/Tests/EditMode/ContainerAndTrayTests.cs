using Game.Data;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Storage;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode
{
    public class ContainerAndTrayTests
    {
        private const float Hour = 3600f;
        private CookingSimulation _sim;

        [SetUp]
        public void CreateKitchen() => _sim = new CookingSimulation(22f);

        private static IngredientProperties Properties(string typeId) => new IngredientProperties
        {
            TypeId = typeId,
            HeatCapacity = 1050f,
            SurfaceFraction = 0.15f,
            StartTemperature = 22f,
            FreshnessLossPerHourAt20C = 1f / 24f,
        };

        private Ingredient Food(string typeId = "Steak") => _sim.AddIngredient(Properties(typeId));

        private static FoodContainer Container(int capacity = 10) => new FoodContainer(capacity, 0.5f);

        private static Tray NewTray() => new Tray(4, 1.5f);

        // A counter holding the given item.
        private static ItemSlot Counter(Item item = null)
        {
            var counter = new ItemSlot();
            if (item != null)
                counter.Put(item);
            return counter;
        }

        private static ItemSlot Hands(Item item = null) => Counter(item);

        [Test]
        public void AContainerHoldsOneKindUpToItsCapacity()
        {
            var container = Container(capacity: 2);
            Assert.That(container.HoldsTypeId, Is.Null);
            container.Put(Food());
            Assert.That(container.HoldsTypeId, Is.EqualTo("Steak"));
            Assert.That(container.CanAccept(Food("Onion")), Is.False, "only the same ingredient");
            Assert.That(container.CanAccept(new Plate(3)), Is.False);
            container.Put(Food());
            Assert.That(container.CanAccept(Food()), Is.False, "full");
        }

        [Test]
        public void AnEmptiedContainerTakesAnything()
        {
            var container = Container();
            container.Put(Food());
            container.Take();
            Assert.That(container.CanAccept(Food("Onion")));
        }

        [Test]
        public void ContainersHoldTenByDesign()
        {
            var equipment = AssetDatabase.LoadAssetAtPath<EquipmentTier>("Assets/_Project/Data/Settings/EquipmentTier_Basic.asset");
            Assert.That(equipment.ContainerCapacity, Is.EqualTo(10));
            Assert.That(equipment.TrayCapacity, Is.EqualTo(4));
        }

        [Test]
        public void EmptyHandsTakeOnePortionAndLeaveTheContainer()
        {
            var container = Container();
            var first = Food();
            container.Put(first);
            container.Put(Food());
            var counter = Counter(container);
            var hands = Hands();

            Assert.That(ItemTransfer.Interact(hands, counter), Is.EqualTo(InteractionOutcome.PickedUp));
            Assert.That(hands.Current, Is.SameAs(first), "oldest first");
            Assert.That(counter.Current, Is.SameAs(container));
        }

        [Test]
        public void AnEmptyContainerCanBeMoved()
        {
            var container = Container();
            var hands = Hands();
            ItemTransfer.Interact(hands, Counter(container));
            Assert.That(hands.Current, Is.SameAs(container));

            var other = Counter();
            Assert.That(ItemTransfer.Interact(hands, other), Is.EqualTo(InteractionOutcome.Placed));
            Assert.That(other.Current, Is.SameAs(container), "it fills a counter");
        }

        [Test]
        public void FoodInHandGoesIntoAMatchingContainer()
        {
            var container = Container();
            var counter = Counter(container);
            Assert.That(ItemTransfer.Interact(Hands(Food()), counter), Is.EqualTo(InteractionOutcome.Placed));
            Assert.That(container.Items, Has.Count.EqualTo(1));

            var onion = Hands(Food("Onion"));
            Assert.That(ItemTransfer.Interact(onion, counter), Is.EqualTo(InteractionOutcome.Nothing));
            Assert.That(onion.Current, Is.Not.Null);
        }

        [Test]
        public void APlateTakesOnePortionAtATime()
        {
            var container = Container();
            container.Put(Food());
            container.Put(Food());
            var plate = new Plate(3);
            var hands = Hands(plate);

            Assert.That(ItemTransfer.Interact(hands, Counter(container)), Is.EqualTo(InteractionOutcome.PickedUp));
            Assert.That(plate.Items, Has.Count.EqualTo(1));
            Assert.That(container.Items, Has.Count.EqualTo(1));
        }

        [Test]
        public void ATrayHoldsAnyMixUpToFour()
        {
            var tray = NewTray();
            tray.Put(Food());
            tray.Put(Food("Onion"));
            tray.Put(new Plate(3));
            var dirty = new Plate(3);
            dirty.MarkDirty();
            tray.Put(dirty);

            Assert.That(tray.Items, Has.Count.EqualTo(4));
            Assert.That(tray.CanAccept(Food()), Is.False, "full");
        }

        [Test]
        public void ATrayCarriesNoOtherCarriers()
        {
            var tray = NewTray();
            Assert.That(tray.CanAccept(NewTray()), Is.False);
            Assert.That(tray.CanAccept(Container()), Is.False);
            Assert.That(tray.CanAccept(_sim.AddPan(new PanProperties { HeatCapacity = 750f, AirLossConductance = 7.7f, Capacity = 1 })), Is.False);
        }

        [Test]
        public void ATrayFillsACounter()
        {
            var counter = Counter();
            var hands = Hands(NewTray());
            Assert.That(ItemTransfer.Interact(hands, counter), Is.EqualTo(InteractionOutcome.Placed));
            Assert.That(counter.Current, Is.InstanceOf<Tray>());

            var plate = Hands(new Plate(3));
            Assert.That(ItemTransfer.Interact(plate, counter), Is.EqualTo(InteractionOutcome.Placed), "onto the tray");
            Assert.That(((Tray)counter.Current).Items, Has.Count.EqualTo(1));
        }

        [Test]
        public void ATrayMovesFoodIntoAContainer()
        {
            var tray = NewTray();
            tray.Put(Food());
            tray.Put(Food());
            var container = Container();

            Assert.That(ItemTransfer.Interact(Hands(tray), Counter(container)), Is.EqualTo(InteractionOutcome.Combined));
            Assert.That(tray.IsEmpty);
            Assert.That(container.Items, Has.Count.EqualTo(2));
        }

        [Test]
        public void AnEmptyTrayLoadsUpFromAContainer()
        {
            var container = Container();
            for (var i = 0; i < 6; i++)
                container.Put(Food());
            var tray = NewTray();

            Assert.That(ItemTransfer.Interact(Hands(tray), Counter(container)), Is.EqualTo(InteractionOutcome.Combined));
            Assert.That(tray.Items, Has.Count.EqualTo(4));
            Assert.That(container.Items, Has.Count.EqualTo(2));
        }

        [Test]
        public void ATrayRestocksAFridge()
        {
            var fridge = new StorageUnit(4f, new StorageUnit.Slot(5));
            var tray = NewTray();
            tray.Put(Food());
            tray.Put(Food());

            Assert.That(ItemTransfer.Interact(Hands(tray), fridge), Is.EqualTo(InteractionOutcome.Combined));
            Assert.That(tray.IsEmpty);
            Assert.That(fridge.Slots[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void ATrayPicksUpPlatesFromThePlateSources()
        {
            var tray = NewTray();
            var hands = Hands(tray);
            var stack = new PlateStack(2, 3);

            Assert.That(ItemTransfer.Interact(hands, stack), Is.EqualTo(InteractionOutcome.PickedUp));
            Assert.That(tray.Items, Has.Count.EqualTo(1));
            Assert.That(stack.Count, Is.EqualTo(1));

            var pass = new PassCounter();
            var plate = new Plate(3);
            plate.Put(Food());
            pass.Put(plate);
            pass.StartEating();
            pass.FinishEating();
            Assert.That(ItemTransfer.Interact(hands, pass), Is.EqualTo(InteractionOutcome.PickedUp), "clears a dirty plate");
            Assert.That(tray.Items, Has.Count.EqualTo(2));
        }

        [Test]
        public void ATrayServesAPlateOntoThePass()
        {
            var tray = NewTray();
            var plate = new Plate(3);
            plate.Put(Food());
            tray.Put(plate);
            var pass = new PassCounter();

            Assert.That(ItemTransfer.Interact(Hands(tray), pass), Is.EqualTo(InteractionOutcome.Combined));
            Assert.That(pass.Current, Is.SameAs(plate));
        }

        [Test]
        public void ChoosingFromATrayKeepsTheOrderIfItCantGo()
        {
            var tray = NewTray();
            var steak = Food();
            var onion = Food("Onion");
            tray.Put(steak);
            tray.Put(onion);

            var chosen = tray.TakeAt(0);
            Assert.That(chosen, Is.SameAs(steak));
            tray.InsertAt(0, chosen);
            Assert.That(tray.Items[0], Is.SameAs(steak));
            Assert.That(tray.Items[1], Is.SameAs(onion));
        }

        [Test]
        public void AgeingRunsFridgeThenContainerThenOpenThenTray()
        {
            var inFridge = Food();
            inFridge.EnvironmentTemperature = 4f;
            var inContainer = Food();
            Container().Put(inContainer);
            var inTheOpen = Food();
            var onTray = Food();
            NewTray().Put(onTray);

            _sim.SkipClosedTime(6f * Hour);

            Assert.That(inFridge.Freshness, Is.GreaterThan(inContainer.Freshness));
            Assert.That(inContainer.Freshness, Is.GreaterThan(inTheOpen.Freshness));
            Assert.That(inTheOpen.Freshness, Is.GreaterThan(onTray.Freshness));
        }

        [Test]
        public void ComingOutStopsTheEffect()
        {
            var container = Container();
            var food = Food();
            container.Put(food);
            Assert.That(food.FreshnessFactor, Is.EqualTo(0.5f));
            container.Take();
            Assert.That(food.FreshnessFactor, Is.EqualTo(1f));

            var tray = NewTray();
            tray.Put(food);
            Assert.That(food.FreshnessFactor, Is.EqualTo(1.5f));
            tray.Take();
            Assert.That(food.FreshnessFactor, Is.EqualTo(1f));
        }

        [Test]
        public void TheRealFactorsKeepThatOrder()
        {
            var equipment = AssetDatabase.LoadAssetAtPath<EquipmentTier>("Assets/_Project/Data/Settings/EquipmentTier_Basic.asset");
            Assert.That(equipment.ContainerFreshnessFactor, Is.LessThan(1f));
            Assert.That(equipment.TrayFreshnessFactor, Is.GreaterThan(1f));
        }

        [Test]
        public void ACleanPlateGoesBackOnTheStack()
        {
            var stack = new PlateStack(0, 3);
            var hands = Hands(new Plate(3));
            Assert.That(ItemTransfer.Interact(hands, stack), Is.EqualTo(InteractionOutcome.Placed));
            Assert.That(hands.IsEmpty);
            Assert.That(stack.Count, Is.EqualTo(1));
        }

        [Test]
        public void EmptyTraysAndContainersGoBackOnTheirRacks()
        {
            var trays = new ItemRack(0, () => NewTray(), item => item is Tray tray && tray.IsEmpty);
            var containers = new ItemRack(0, () => Container(), item => item is FoodContainer container && container.IsEmpty);

            var hands = Hands(NewTray());
            Assert.That(ItemTransfer.Interact(hands, trays), Is.EqualTo(InteractionOutcome.Placed));
            Assert.That(hands.IsEmpty);

            hands = Hands(Container());
            Assert.That(ItemTransfer.Interact(hands, containers), Is.EqualTo(InteractionOutcome.Placed));
            Assert.That(hands.IsEmpty);
        }

        [Test]
        public void AnIngredientGoesBackInStorage()
        {
            var fridge = new StorageUnit(4f, new StorageUnit.Slot(5));
            var hands = Hands(Food());
            Assert.That(ItemTransfer.Interact(hands, fridge), Is.EqualTo(InteractionOutcome.Placed));
            Assert.That(hands.IsEmpty);
            Assert.That(fridge.Slots[0].Count, Is.EqualTo(1));
        }

        [Test]
        public void ATrayReturnsWhatFitsWhereverItIsOnTheTray()
        {
            var fridge = new StorageUnit(4f, new StorageUnit.Slot(5));
            var tray = NewTray();
            var plate = new Plate(3);
            tray.Put(Food());
            tray.Put(Food());
            tray.Put(plate); // on top of the steaks, and the fridge doesn't take plates

            Assert.That(ItemTransfer.Interact(Hands(tray), fridge), Is.EqualTo(InteractionOutcome.Combined));
            Assert.That(fridge.Slots[0].Count, Is.EqualTo(2));
            Assert.That(tray.Items, Is.EqualTo(new Item[] { plate }));
        }

        [Test]
        public void ATrayReturnsCleanPlatesBeforeTakingMore()
        {
            var stack = new PlateStack(1, 3);
            var tray = NewTray();
            tray.Put(new Plate(3));
            tray.Put(Food()); // on top of the plate
            var hands = Hands(tray);

            Assert.That(ItemTransfer.Interact(hands, stack), Is.EqualTo(InteractionOutcome.Combined));
            Assert.That(stack.Count, Is.EqualTo(2));
            Assert.That(tray.Items, Has.Count.EqualTo(1));

            Assert.That(ItemTransfer.Interact(hands, stack), Is.EqualTo(InteractionOutcome.PickedUp), "nothing left to return");
            Assert.That(tray.Items, Has.Count.EqualTo(2));
        }

        [Test]
        public void RacksTakeBackOnlyEmptyOnesOfTheirKind()
        {
            var rack = new ItemRack(1, () => NewTray(), item => item is Tray tray && tray.IsEmpty);
            var full = NewTray();
            full.Put(new Plate(3));

            Assert.That(rack.CanAccept(full), Is.False);
            Assert.That(rack.CanAccept(Container()), Is.False);
            var taken = rack.Take();
            Assert.That(rack.CanTake, Is.False);
            rack.Put(taken);
            Assert.That(rack.Count, Is.EqualTo(1));
        }
    }
}
