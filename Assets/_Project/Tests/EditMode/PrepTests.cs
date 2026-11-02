using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Prep;
using Game.Simulation.Quality;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class ChopRhythmTests
    {
        private const float Beat = 0.3f;
        private ChopRhythm _rhythm;

        [SetUp]
        public void CreateRhythm() => _rhythm = new ChopRhythm(Beat, goodWindow: 0.07f, okWindow: 0.12f);

        [Test]
        public void FirstChopSetsTheBeat() => Assert.That(_rhythm.Hit(10.0), Is.EqualTo(ChopGrade.Good));

        [Test]
        public void OnTheBeatIsGood()
        {
            _rhythm.Hit(10.0);
            Assert.That(_rhythm.Hit(10.0 + Beat + 0.03), Is.EqualTo(ChopGrade.Good));
        }

        [Test]
        public void SlightlyEarlyAndLateAreGradedAsSuch()
        {
            _rhythm.Hit(10.0);
            Assert.That(_rhythm.Hit(10.0 + Beat - 0.1), Is.EqualTo(ChopGrade.Early));
            Assert.That(_rhythm.Hit(10.0 + 2 * Beat + 0.1), Is.EqualTo(ChopGrade.Late));
        }

        [Test]
        public void HalfwayBetweenBeatsIsAMiss()
        {
            _rhythm.Hit(10.0);
            Assert.That(_rhythm.Hit(10.0 + Beat * 1.5), Is.EqualTo(ChopGrade.Miss));
        }

        [Test]
        public void ALongPauseRestartsTheBeatWithOneMiss()
        {
            _rhythm.Hit(10.0);
            Assert.That(_rhythm.Hit(13.0), Is.EqualTo(ChopGrade.Miss));
            Assert.That(_rhythm.Hit(13.0 + Beat), Is.EqualTo(ChopGrade.Good), "the new beat starts at the chop after the pause");
        }

        [Test]
        public void EvennessAveragesTheGrades()
        {
            _rhythm.Hit(10.0);              // good 1.0
            _rhythm.Hit(10.0 + Beat + 0.1); // late 0.6
            Assert.That(_rhythm.Evenness, Is.EqualTo(0.8f).Within(0.001f));
        }
    }

    public class PrepCounterTests
    {
        private const float Beat = 0.3f;
        private CookingSimulation _sim;
        private PrepCounter _counter;

        private static readonly IngredientProperties DicedOnion = new IngredientProperties
        {
            TypeId = "DicedOnion", HeatCapacity = 585f, SurfaceFraction = 0.3f, StartTemperature = 20f,
        };

        private Ingredient NewOnion() => _sim.AddIngredient(new IngredientProperties
        {
            TypeId = "Onion", HeatCapacity = 585f, SurfaceFraction = 0.15f, StartTemperature = 20f,
            CutIntoTypeId = "DicedOnion", CutsNeeded = 4, ChopInterval = Beat,
        });

        [SetUp]
        public void CreateCounter()
        {
            _sim = new CookingSimulation(22f);
            _counter = new PrepCounter(slotCapacity: 2, whole => _sim.Transform(whole, DicedOnion), goodWindow: 0.07f, okWindow: 0.15f);
        }

        private void ChopToTheEnd(double start = 10.0, double offBeat = 0.0)
        {
            for (var i = 0; i < 4; i++)
                _counter.Chop(start + i * Beat + (i > 0 ? offBeat : 0.0));
        }

        [Test]
        public void WholeOnionGoesOnTheBoard()
        {
            var onion = NewOnion();
            _counter.Put(onion);
            Assert.That(_counter.Current, Is.SameAs(onion));
            Assert.That(_counter.CanChop);
        }

        [Test]
        public void ChoppingToTheEndPutsDicedOnionInASlot()
        {
            _counter.Put(NewOnion());
            ChopToTheEnd();

            Assert.That(_counter.Current, Is.Null);
            Assert.That(_counter.Slots[0].TypeId, Is.EqualTo("DicedOnion"));
            Assert.That(_counter.Slots[0].Items[0].CutEvenness, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void UnevenRhythmGivesAnUnevenCut()
        {
            _counter.Put(NewOnion());
            ChopToTheEnd(offBeat: 0.12); // every chop after the first is late
            Assert.That(_counter.Slots[0].Items[0].CutEvenness, Is.LessThan(0.8f));
        }

        [Test]
        public void SameIngredientStacksInOneSlot()
        {
            _counter.Put(NewOnion());
            ChopToTheEnd(10.0);
            _counter.Put(NewOnion());
            ChopToTheEnd(20.0);
            Assert.That(_counter.Slots[0].Items, Has.Count.EqualTo(2));
            Assert.That(_counter.Slots[1].Items, Is.Empty);
        }

        [Test]
        public void WhenNoSlotCanTakeItTheCutStaysOnTheBoard()
        {
            // Fill all four slots with other prepared food.
            for (var i = 0; i < PrepCounter.SlotCount; i++)
                _counter.Put(_sim.AddIngredient(new IngredientProperties { TypeId = $"Prepped{i}", HeatCapacity = 100f, SurfaceFraction = 0.2f }));

            _counter.Put(NewOnion());
            ChopToTheEnd();

            Assert.That(_counter.Current?.TypeId, Is.EqualTo("DicedOnion"));
        }

        [Test]
        public void TakingFromASlotGivesAPortionAndFreesItWhenEmpty()
        {
            _counter.Put(NewOnion());
            ChopToTheEnd();

            Assert.That(_counter.TryTakeFromSlot(0, out var portion), Is.True);
            Assert.That(portion.TypeId, Is.EqualTo("DicedOnion"));
            Assert.That(_counter.Slots[0].TypeId, Is.Null, "an empty slot is free for any ingredient");
        }

        [Test]
        public void PreparedFoodGoesBackIntoItsSlot()
        {
            _counter.Put(NewOnion());
            ChopToTheEnd();
            _counter.TryTakeFromSlot(0, out var portion);

            var hands = new ItemSlot();
            hands.Put(portion);
            Assert.That(ItemTransfer.Interact(hands, _counter), Is.EqualTo(InteractionOutcome.Placed));
            Assert.That(_counter.Slots[0].Items, Does.Contain(portion));
        }

        [Test]
        public void NothingToChopWithAnEmptyBoard()
        {
            Assert.That(_counter.Chop(10.0), Is.Null);
        }

        [Test]
        public void TakingTheOnionOffResetsTheCut()
        {
            _counter.Put(NewOnion());
            _counter.Chop(10.0);
            _counter.Chop(10.0 + Beat);
            var onion = _counter.Take();
            _counter.Put(onion);
            Assert.That(_counter.Cuts, Is.EqualTo(0));
        }

        [Test]
        public void DicingKeepsTemperatureAndFreshnessAndRemovesTheWholeOnion()
        {
            var onion = NewOnion();
            _counter.Put(onion);
            ChopToTheEnd();
            var diced = _counter.Slots[0].Items[0];

            Assert.That(diced.CoreTemperature, Is.EqualTo(onion.AverageTemperature).Within(0.01f));
            Assert.That(diced.Freshness, Is.EqualTo(onion.Freshness));
            Assert.That(_sim.Contains(onion), Is.False, "the whole onion left the simulation");
        }
    }

    public class RestAndCutScoringTests
    {
        private CookingSimulation _sim;

        [SetUp]
        public void CreateKitchen() => _sim = new CookingSimulation(22f);

        private Ingredient CookedSteak()
        {
            var hob = _sim.AddHob(new HobProperties { MaxPower = 2000f });
            var pan = _sim.AddPan(new PanProperties { HeatCapacity = 750f, AirLossConductance = 7.7f, Capacity = 1 });
            hob.PlacePan(pan);
            hob.Dial = 1f;
            _sim.Tick(150f);
            var steak = _sim.AddIngredient(new IngredientProperties
            {
                TypeId = "Steak", HeatCapacity = 1050f, SurfaceFraction = 0.15f, SurfaceCoreConductance = 0.9f,
                PanContactConductance = 6f, AirConductance = 0.25f, EvaporationConductance = 25f,
                BrowningStartTemperature = 140f, BrowningRate = 1f / 12600f, BurntBrowning = 2f, StartTemperature = 4f,
                DonenessLevels = new[] { new DonenessLevel("Rare", 45f), new DonenessLevel("Medium-rare", 52f) },
                RestTargetSeconds = 180f,
            });
            pan.Put(steak);
            _sim.Tick(180f);
            steak.Flip();
            _sim.Tick(180f);
            pan.Take();
            return steak;
        }

        [Test]
        public void RestTimeCountsOnlyOutOfThePan()
        {
            var steak = CookedSteak();
            Assert.That(steak.SecondsOutOfPan, Is.EqualTo(0f).Within(0.6f));
            _sim.Tick(60f);
            Assert.That(steak.SecondsOutOfPan, Is.EqualTo(60f).Within(0.6f));
        }

        [Test]
        public void ServingStraightFromThePanScoresNoRest()
        {
            var item = DishScorer.ScoreItem(CookedSteak(), new ScoringSettings());
            Assert.That(item.Rest, Is.LessThan(0.05f));
        }

        [Test]
        public void RestingThreeMinutesScoresFullRest()
        {
            var steak = CookedSteak();
            _sim.Tick(180f);
            Assert.That(DishScorer.ScoreItem(steak, new ScoringSettings()).Rest, Is.EqualTo(1f));
        }

        [Test]
        public void RawSteakIsntScoredOnRest()
        {
            var raw = _sim.AddIngredient(new IngredientProperties
            {
                TypeId = "Steak", HeatCapacity = 1050f, SurfaceFraction = 0.15f, StartTemperature = 4f,
                DonenessLevels = new[] { new DonenessLevel("Rare", 45f) }, RestTargetSeconds = 180f,
            });
            Assert.That(DishScorer.ScoreItem(raw, new ScoringSettings()).Rest, Is.Null);
        }

        [Test]
        public void CutEvennessIsScored()
        {
            var diced = _sim.AddIngredient(new IngredientProperties { TypeId = "DicedOnion", HeatCapacity = 585f, SurfaceFraction = 0.3f });
            diced.CutEvenness = 0.5f;
            Assert.That(DishScorer.ScoreItem(diced, new ScoringSettings()).Cut, Is.EqualTo(0.5f));
        }
    }
}
