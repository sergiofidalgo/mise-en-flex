using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Gameplay.Saving;
using Game.Simulation.Cooking;
using Game.Simulation.Days;
using Game.Simulation.Items;
using Game.Simulation.Saving;
using Game.Simulation.Staff;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Saves (Phase 5, decisions 2026-11-06 and 2026-11-08): what's in the kitchen comes back exactly as
    /// it was left, the file survives a round trip, an older file still loads, and a newer one is refused.
    /// </summary>
    public class SaveTests
    {
        private CookingSimulation _sim;
        private TestFactory _factory;

        [SetUp]
        public void CreateKitchen()
        {
            _sim = new CookingSimulation(22f);
            _factory = new TestFactory(_sim);
        }

        [Test]
        public void ASearedSteakInAPanComesBackExactlyAsItWas()
        {
            var pan = _factory.Pan();
            var steak = _factory.Ingredient("Steak");
            pan.Put(steak);
            var hob = _sim.AddHob(new HobProperties { MaxPower = 3000f });
            hob.PlacePan(pan);
            hob.Dial = 0.8f;
            for (var i = 0; i < 120; i++)
                _sim.Tick(1f);
            steak.Flip();
            steak.Seasoning = 0.4f;
            steak.CutEvenness = 0.7f;

            var records = new List<ItemRecord>();
            ItemRecords.Write(pan, 3, 1, records);
            var back = ItemRecords.Read(records, new TestFactory(new CookingSimulation(22f)));

            Assert.That(back, Has.Count.EqualTo(1), "only the pan is top level");
            Assert.That(back[0].Record.Station, Is.EqualTo(3));
            Assert.That(back[0].Record.Slot, Is.EqualTo(1));
            var panBack = (Pan)back[0].Item;
            Assert.That(panBack.Temperature, Is.EqualTo(pan.Temperature).Within(0.001f));
            var steakBack = panBack.Contents.Single();
            Assert.That(steakBack.Pan, Is.SameAs(panBack));
            Assert.That(steakBack.CoreTemperature, Is.EqualTo(steak.CoreTemperature).Within(0.001f));
            Assert.That(steakBack.SurfaceTemperature(IngredientSide.A), Is.EqualTo(steak.SurfaceTemperature(IngredientSide.A)).Within(0.001f));
            Assert.That(steakBack.PeakCoreTemperature, Is.EqualTo(steak.PeakCoreTemperature).Within(0.001f));
            Assert.That(steakBack.Browning(IngredientSide.A), Is.EqualTo(steak.Browning(IngredientSide.A)).Within(0.0001f));
            Assert.That(steakBack.Browning(IngredientSide.A), Is.GreaterThan(0f), "it was really seared");
            Assert.That(steakBack.DownSide, Is.EqualTo(steak.DownSide));
            Assert.That(steakBack.HasBeenFlipped, Is.True);
            Assert.That(steakBack.Freshness, Is.EqualTo(steak.Freshness).Within(0.0001f));
            Assert.That(steakBack.Seasoning, Is.EqualTo(0.4f));
            Assert.That(steakBack.CutEvenness, Is.EqualTo(0.7f));
        }

        [Test]
        public void PlatedFoodStaysPlatedAndDirtyPlatesStayDirty()
        {
            var plate = _factory.Plate();
            plate.Put(_factory.Ingredient("Steak"));
            var dirty = _factory.Plate();
            dirty.MarkDirty();

            var records = new List<ItemRecord>();
            ItemRecords.Write(plate, 0, 0, records);
            ItemRecords.Write(dirty, -1, 0, records);
            var back = ItemRecords.Read(records, _factory);

            var plateBack = (Plate)back[0].Item;
            Assert.That(((Ingredient)plateBack.Items.Single()).IsPlated, Is.True);
            Assert.That(plateBack.IsDirty, Is.False);
            Assert.That(((Plate)back[1].Item).IsDirty, Is.True);
            Assert.That(back[1].Record.Station, Is.EqualTo(-1), "loose: it was in someone's hands");
        }

        [Test]
        public void KnivesTraysAndContainersComeBackAsWhatTheyWere()
        {
            var container = _factory.Container();
            container.Put(_factory.Ingredient("Onion"));
            container.Put(_factory.Ingredient("Onion"));
            var records = new List<ItemRecord>();
            ItemRecords.Write(new Knife(), 1, 0, records);
            ItemRecords.Write(_factory.Tray(), 2, 0, records);
            ItemRecords.Write(container, 2, 1, records);

            var back = ItemRecords.Read(records, _factory).Select(entry => entry.Item).ToList();

            Assert.That(back[0], Is.InstanceOf<Knife>());
            Assert.That(back[1], Is.InstanceOf<Tray>());
            Assert.That(((FoodContainer)back[2]).Items, Has.Count.EqualTo(2));
        }

        [Test]
        public void FoodWhoseContainerIsGoneIsDiscardedNotLeftTicking()
        {
            var records = new List<ItemRecord>
            {
                new ItemRecord { Kind = ItemKind.Ingredient, TypeId = "Steak", Parent = 5 },
            };
            var sim = new CookingSimulation(22f);

            var back = ItemRecords.Read(records, new TestFactory(sim));

            Assert.That(back, Is.Empty);
            Assert.That(sim.Ingredients, Is.Empty);
        }

        [Test]
        public void TheFileRoundTripsThroughJson()
        {
            var directory = Path.Combine(Path.GetTempPath(), "MiseEnFlexSaveTest_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var file = new SaveFile(directory);
                var save = new CampaignSave { Day = 4, Balance = -40.5f, BrokenPlates = 2 };
                save.Stations.Add(new StationRecord { FurnitureId = "table", SceneName = "Table", X = 1.5f, Z = -2f, Degrees = 45f, Chairs = 3 });
                var steak = _factory.Ingredient("Steak");
                steak.Seasoning = 0.25f;
                ItemRecords.Write(steak, 0, 0, save.Items);
                save.Staff.Add(new StaffRecord { Name = "Ana", Role = 1, DailyWage = 30f, Morale = 0.7f });
                save.Reviews.Add(new ReviewRecord { Day = 4, Stars = 3.5f, Line = "Good steak." });

                file.Write(save);
                var back = file.Read();

                Assert.That(back.Day, Is.EqualTo(4));
                Assert.That(back.Balance, Is.EqualTo(-40.5f), "money owed survives");
                Assert.That(back.BrokenPlates, Is.EqualTo(2));
                Assert.That(back.Stations.Single().Degrees, Is.EqualTo(45f));
                Assert.That(back.Stations.Single().Chairs, Is.EqualTo(3));
                Assert.That(back.Items.Single().Ingredient.Seasoning, Is.EqualTo(0.25f));
                Assert.That(back.Staff.Single().Morale, Is.EqualTo(0.7f));
                Assert.That(back.Reviews.Single().Line, Is.EqualTo("Good steak."));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void AnOlderFileWithoutNewerFieldsStillLoads()
        {
            var save = JsonUtility.FromJson<CampaignSave>("{\"Version\":1,\"Day\":2,\"Balance\":10}");

            Assert.That(SaveMigration.Upgrade(save), Is.True);
            Assert.That(save.Day, Is.EqualTo(2));
            Assert.That(save.Stations, Is.Not.Null.And.Empty);
            Assert.That(save.Items, Is.Not.Null.And.Empty);
        }

        [Test]
        public void AFileFromANewerGameIsRefused()
        {
            var save = new CampaignSave { Version = CampaignSave.CurrentVersion + 1 };

            Assert.That(SaveMigration.Upgrade(save), Is.False);
        }

        [Test]
        public void ContinuingStartsTheMorningAfterWithTheSavedMoney()
        {
            var days = DayCycle.Continuing(4, -12f);

            Assert.That(days.Day, Is.EqualTo(5));
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Plan));
            Assert.That(days.Balance, Is.EqualTo(-12f));
        }

        [Test]
        public void StaffComeBackWithTheirMoraleButRested()
        {
            var member = new StaffMember(new Candidate("Ana", StaffRole.Dishwasher, "Quick hands.", 0.8f, 1.2f, 30f)) { Morale = 0.35f };

            var back = SaveConversions.ToMember(SaveConversions.ToRecord(member));

            Assert.That(back.Name, Is.EqualTo("Ana"));
            Assert.That(back.Role, Is.EqualTo(StaffRole.Dishwasher));
            Assert.That(back.Skill, Is.EqualTo(0.8f));
            Assert.That(back.DailyWage, Is.EqualTo(30f));
            Assert.That(back.Morale, Is.EqualTo(0.35f));
            Assert.That(back.Fatigue, Is.EqualTo(0f));
        }

        private sealed class TestFactory : IItemFactory
        {
            private readonly CookingSimulation _sim;

            public TestFactory(CookingSimulation sim) => _sim = sim;

            public Ingredient Ingredient(string typeId) => _sim.AddIngredient(new IngredientProperties
            {
                TypeId = typeId,
                HeatCapacity = 1000f,
                SurfaceFraction = 0.15f,
                SurfaceCoreConductance = 0.9f,
                PanContactConductance = 6f,
                AirConductance = 0.25f,
                BrowningStartTemperature = 140f,
                BrowningRate = 1f / 12600f,
                BurntBrowning = 2f,
                StartTemperature = 4f,
                FreshnessLossPerHourAt20C = 1f / 24f,
            });

            public Pan Pan() => _sim.AddPan(new PanProperties { HeatCapacity = 750f, AirLossConductance = 7.7f, Capacity = 2 });

            public Plate Plate() => new Plate(3);

            public Tray Tray() => new Tray(4, 1.5f);

            public FoodContainer Container() => new FoodContainer(4, 0.5f);

            public Item Plain(string typeId) => new Item(typeId);

            public void Discard(Item item)
            {
                if (item is Ingredient ingredient)
                    _sim.Remove(ingredient);
            }
        }
    }
}
