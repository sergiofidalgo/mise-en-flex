using System.Linq;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Prep;
using Game.Simulation.Staff;
using Game.Simulation.Storage;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class PrepAssistantTests
    {
        private CookingSimulation _sim;
        private ItemSlot _counter;
        private StorageUnit _fridge;
        private PrepCounter _board;
        private ItemRack _trays;
        private ItemSink _bin;
        private int _binned;
        private PrepAssistant _job;

        private static IngredientProperties Properties(string typeId, string cutsInto = null) => new IngredientProperties
        {
            TypeId = typeId,
            HeatCapacity = 500f,
            SurfaceFraction = 0.2f,
            StartTemperature = 20f,
            CutIntoTypeId = cutsInto,
            CutsNeeded = cutsInto != null ? 4 : 0,
            ChopSecondsPerSweep = 0.7f,
            SweetSpotPosition = 85f,
            SweetSpotGoodHalfWidth = 12f,
            SweetSpotOkHalfWidth = 28f,
        };

        private Ingredient Food(string typeId = "Steak") => _sim.AddIngredient(Properties(typeId));

        private void Kitchen(float skill = 1f, float speed = 1f, int trays = 1, StaffSettings settings = null)
        {
            _sim = new CookingSimulation(22f);
            _counter = new ItemSlot();
            _fridge = new StorageUnit(4f, new StorageUnit.Slot(6), new StorageUnit.Slot(6));
            _board = new PrepCounter(whole => _sim.Transform(whole, Properties("DicedOnion")));
            _trays = new ItemRack(trays, () => new Tray(4, 1.5f), item => item is Tray tray && tray.IsEmpty);
            _bin = new ItemSink();
            _binned = 0;
            _bin.Discarded += item =>
            {
                _binned++;
                _sim.Remove((Ingredient)item);
            };
            var member = new StaffMember(new Candidate("Ana", StaffRole.PrepAssistant, "steady", skill, speed, 40f));
            _job = new PrepAssistant(member, new[] { _counter }, new[] { _fridge }, _board, _trays, _bin, _sim, settings ?? new StaffSettings { FatiguePerJob = 0f, FatigueMistakes = 0f }, 1);
        }

        private FoodContainer SetOutContainer(string typeId, int portions)
        {
            var container = new FoodContainer(6, 0.5f);
            for (var i = 0; i < portions; i++)
                container.Put(Food(typeId));
            _counter.Put(container);
            return container;
        }

        // Walks them wherever they're going, as gameplay would.
        private void Work(int rounds = 40, float seconds = 60f)
        {
            for (var i = 0; i < rounds; i++)
            {
                _job.Tick(seconds);
                if (_job.Task == PrepTask.ToRack || _job.Task == PrepTask.ToStorage || _job.Task == PrepTask.ToBoard || _job.Task == PrepTask.ToContainer
                    || _job.Task == PrepTask.ToBin || _job.Task == PrepTask.ReturningTray)
                    _job.Arrived();
            }
        }

        [Test]
        public void TheyRefillALowContainerFromStorage()
        {
            Kitchen();
            var container = SetOutContainer("Steak", 1);
            _fridge.Stock("Steak", () => Food(), 6);

            Work();

            Assert.That(container.Items.Count, Is.GreaterThanOrEqualTo(3), "above half again");
            Assert.That(container.Items.All(i => i.TypeId == "Steak"));
            Assert.That(_job.Member.JobsToday, Is.GreaterThan(0));
        }

        [Test]
        public void ATrayIsOnlyOutForTheTrip()
        {
            Kitchen();
            SetOutContainer("Steak", 1);
            _fridge.Stock("Steak", () => Food(), 6);

            _job.Tick(1f);
            Assert.That(_job.Task, Is.EqualTo(PrepTask.ToRack));
            Assert.That(_job.Target, Is.SameAs(_trays));
            _job.Arrived();
            Assert.That(_job.Carried, Is.InstanceOf<Tray>());
            Assert.That(_trays.Count, Is.EqualTo(0));

            Work();

            Assert.That(_job.Tray, Is.Null, "it went back");
            Assert.That(_job.Carried, Is.Null);
            Assert.That(_trays.Count, Is.EqualTo(1));
        }

        [Test]
        public void WithNoTrayFreeTheyCarryByHand()
        {
            Kitchen(trays: 0);
            var container = SetOutContainer("Steak", 1);
            _fridge.Stock("Steak", () => Food(), 6);

            _job.Tick(1f);
            _job.Arrived();
            Assert.That(_job.Carried, Is.InstanceOf<Ingredient>());

            Work();
            Assert.That(container.Items.Count, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void TheyDontRestockStorage()
        {
            Kitchen();
            _fridge.Stock("Steak", () => Food(), 2);
            Work();
            Assert.That(_fridge.Slots[0].Count, Is.EqualTo(2), "nothing moved without a container asking");
            Assert.That(_job.Task, Is.EqualTo(PrepTask.Idle));
        }

        [Test]
        public void TheyLeaveEmptyAndFullEnoughContainersAlone()
        {
            Kitchen();
            SetOutContainer("Steak", 0);
            _fridge.Stock("Steak", () => Food(), 6);
            Work();
            Assert.That(_fridge.Slots[0].Count, Is.EqualTo(6), "an empty container isn't for anything yet");

            Kitchen();
            var half = SetOutContainer("Steak", 3);
            _fridge.Stock("Steak", () => Food(), 6);
            Work();
            Assert.That(half.Items, Has.Count.EqualTo(3), "half full is enough");
        }

        [Test]
        public void TheyDontOverfill()
        {
            Kitchen();
            var container = SetOutContainer("Steak", 2);
            _fridge.Stock("Steak", () => Food(), 12);
            Work();
            Assert.That(container.Items.Count, Is.LessThanOrEqualTo(6));
        }

        [Test]
        public void TheyCutWholeFoodWhenThatsAllThereIs()
        {
            Kitchen(speed: 1f);
            var container = SetOutContainer("DicedOnion", 1);
            _fridge.Stock("Onion", () => _sim.AddIngredient(Properties("Onion", "DicedOnion")), 3);

            Work();

            Assert.That(container.Items.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(container.Items.All(i => i.TypeId == "DicedOnion"));
            Assert.That(_fridge.Slots[0].Count, Is.LessThan(3));
            Assert.That(_board.Current, Is.Null, "they leave the board clear");
        }

        [Test]
        public void SkillDecidesHowEvenTheirCutsAre()
        {
            float Evenness(float skill)
            {
                Kitchen(skill: skill);
                SetOutContainer("DicedOnion", 1);
                _fridge.Stock("Onion", () => _sim.AddIngredient(Properties("Onion", "DicedOnion")), 1);
                var evenness = -1f;
                _board.CutFinished += product => evenness = product.CutEvenness ?? -1f;
                Work();
                Assert.That(evenness, Is.GreaterThanOrEqualTo(0f), "they cut it");
                return evenness;
            }

            Assert.That(Evenness(1f), Is.EqualTo(1f));
            Assert.That(Evenness(0.5f), Is.LessThan(1f));
        }

        [Test]
        public void ABusyBoardMakesThemWaitThenGiveUp()
        {
            Kitchen(settings: new StaffSettings { BoardPatienceSeconds = 120f, FatigueMistakes = 0f, FatiguePerJob = 0f });
            SetOutContainer("DicedOnion", 1);
            _fridge.Stock("Onion", () => _sim.AddIngredient(Properties("Onion", "DicedOnion")), 2);

            _job.Tick(1f); _job.Arrived(); // storage: food to cut goes by hand
            Assert.That(_job.Task, Is.EqualTo(PrepTask.ToBoard));
            Assert.That(_job.Tray, Is.Null);
            Assert.That(_job.InHand, Is.Not.Null);
            _board.Put(_sim.AddIngredient(Properties("Onion", "DicedOnion"))); // a chef got there first
            _job.Arrived();
            Assert.That(_job.Task, Is.EqualTo(PrepTask.WaitingForBoard));

            _job.Tick(121f);
            Assert.That(_job.Task, Is.EqualTo(PrepTask.Idle));
            Assert.That(_job.InHand, Is.Not.Null, "still holding the onion");

            Work(rounds: 2);
            Assert.That(_job.InHand, Is.Null, "whole food goes back");
            Assert.That(_fridge.Slots[0].Count, Is.EqualTo(2));
            Assert.That(_binned, Is.EqualTo(0));
        }

        [Test]
        public void TheyDontFetchFoodToCutWhileTheBoardIsBusy()
        {
            Kitchen();
            SetOutContainer("DicedOnion", 1);
            _fridge.Stock("Onion", () => _sim.AddIngredient(Properties("Onion", "DicedOnion")), 2);
            _board.Put(_sim.AddIngredient(Properties("Onion", "DicedOnion")));

            Work();

            Assert.That(_fridge.Slots[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void IfAChefTakesTheFoodOffTheBoardTheyStop()
        {
            Kitchen();
            SetOutContainer("DicedOnion", 1);
            _fridge.Stock("Onion", () => _sim.AddIngredient(Properties("Onion", "DicedOnion")), 1);
            _job.Tick(1f); _job.Arrived();
            _job.Arrived();
            Assert.That(_job.Task, Is.EqualTo(PrepTask.Cutting));

            _board.Take();
            _job.Tick(1f);
            Assert.That(_job.Task, Is.EqualTo(PrepTask.Idle));
        }

        [Test]
        public void TheyDropPortionsDependingOnSkill()
        {
            Kitchen(skill: 0f);
            var container = SetOutContainer("Steak", 1);
            _fridge.Stock("Steak", () => Food(), 4);
            var gone = 0;
            _job.ItemGone += _ => gone++;

            Work(rounds: 12);

            Assert.That(_job.Member.MistakesToday, Is.GreaterThan(0));
            Assert.That(gone, Is.EqualTo(_job.Member.MistakesToday));
            Assert.That(container.Items, Has.Count.EqualTo(1), "skill 0 drops everything");
        }

        [Test]
        public void IfTheContainerIsGoneWholeFoodGoesBack()
        {
            Kitchen();
            var container = SetOutContainer("Steak", 1);
            _fridge.Stock("Steak", () => Food(), 4);
            _job.Tick(1f); _job.Arrived(); // tray
            _job.Arrived(); // storage
            Assert.That(_job.Task, Is.EqualTo(PrepTask.ToContainer));

            container.Take();
            _counter.Take(); // a chef moved it
            _job.Arrived();
            Work();

            Assert.That(_fridge.Slots[0].Count, Is.EqualTo(4));
            Assert.That(_binned, Is.EqualTo(0));
            Assert.That(_job.Carried, Is.Null);
        }

        [Test]
        public void IfTheContainerIsGoneCutFoodGoesInTheBin()
        {
            Kitchen();
            var container = SetOutContainer("DicedOnion", 1);
            _fridge.Stock("Onion", () => _sim.AddIngredient(Properties("Onion", "DicedOnion")), 1);
            _job.Tick(1f); _job.Arrived(); // storage
            _job.Arrived(); // board
            _job.Tick(1000f); // cut
            Assert.That(_job.Task, Is.EqualTo(PrepTask.ToContainer));
            Assert.That(_job.InHand.TypeId, Is.EqualTo("DicedOnion"));

            container.Take();
            _counter.Take();
            _job.Arrived();
            _job.Tick(1f);
            Assert.That(_job.Task, Is.EqualTo(PrepTask.ToBin));
            _job.Arrived();

            Assert.That(_binned, Is.EqualTo(1));
            Assert.That(_job.Carried, Is.Null);
        }

        [Test]
        public void WholeFoodWithNoRoomInStorageIsBinned()
        {
            Kitchen();
            var container = SetOutContainer("Steak", 1);
            _fridge.Stock("Steak", () => Food(), 1);
            _job.Tick(1f); _job.Arrived();
            _job.Arrived();

            _fridge.Stock("Onion", () => Food("Onion"), 12); // storage fills up
            container.Take();
            _counter.Take();
            _job.Arrived();
            _job.Tick(1f);
            Assert.That(_job.Task, Is.EqualTo(PrepTask.ToBin));
        }

        [Test]
        public void BeingLetGoPutsTheTrayBack()
        {
            Kitchen();
            SetOutContainer("Steak", 1);
            _fridge.Stock("Steak", () => Food(), 4);
            _job.Tick(1f); _job.Arrived();
            _job.Arrived();
            Assert.That(_job.Tray.IsEmpty, Is.False);

            _job.Release();

            Assert.That(_job.Tray, Is.Null);
            Assert.That(_trays.Count, Is.EqualTo(1));
            Assert.That(_fridge.Slots[0].Count, Is.EqualTo(4));
        }

        [Test]
        public void EveryDayOffersBothRoles()
        {
            var templates = new[]
            {
                new Candidate("", StaffRole.Dishwasher, "a", 1f, 1f, 1f),
                new Candidate("", StaffRole.Dishwasher, "b", 1f, 1f, 1f),
                new Candidate("", StaffRole.Dishwasher, "c", 1f, 1f, 1f),
                new Candidate("", StaffRole.PrepAssistant, "d", 1f, 1f, 1f),
            };
            for (var day = 1; day < 30; day++)
            {
                var candidates = StaffMarket.CandidatesFor(day, 1, 3, new[] { "A", "B", "C" }, templates);
                Assert.That(candidates.Select(c => c.Role).Distinct().Count(), Is.EqualTo(2), $"day {day}");
            }
        }
    }
}
