using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Prep;
using Game.Simulation.Quality;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class ChopDialTests
    {
        private const float Turn = 0.8f;   // seconds per revolution at base speed
        private const float Spot = 90f;    // sweet spot at three o'clock
        private const double T0 = 10.0;    // when the needle starts, at the top
        private ChopDial _dial;

        [SetUp]
        public void CreateDial()
        {
            _dial = new ChopDial(Turn, Spot, goodHalfWidth: 15f, okHalfWidth: 35f);
            _dial.Start(T0);
        }

        // The next real time the needle points at `angle`, from `now`.
        private double Next(float angle, double now) => now + _dial.SecondsUntil(angle, now);

        [Test]
        public void NeedleSweepsClockwiseFromTheTop()
        {
            Assert.That(_dial.NeedleAngle(T0), Is.EqualTo(0f).Within(0.01f));
            Assert.That(_dial.NeedleAngle(T0 + Turn / 2f), Is.EqualTo(180f).Within(0.01f));
        }

        [Test]
        public void PressingInTheSweetSpotIsGood()
        {
            Assert.That(_dial.Hit(Next(Spot + 10f, T0)), Is.EqualTo(ChopGrade.Good));
        }

        [Test]
        public void JustBeforeTheSpotIsEarlyAndJustAfterIsLate()
        {
            Assert.That(_dial.GradeAt(Next(Spot - 25f, T0)), Is.EqualTo(ChopGrade.Early));
            Assert.That(_dial.GradeAt(Next(Spot + 25f, T0)), Is.EqualTo(ChopGrade.Late));
        }

        [Test]
        public void FarFromTheSpotIsAMiss()
        {
            Assert.That(_dial.GradeAt(Next(Spot + 180f, T0)), Is.EqualTo(ChopGrade.Miss));
        }

        [Test]
        public void ASpotNearTheTopWrapsAroundCorrectly()
        {
            var dial = new ChopDial(Turn, sweetSpot: 5f, goodHalfWidth: 15f, okHalfWidth: 35f);
            dial.Start(T0);
            Assert.That(dial.GradeAt(T0 + dial.SecondsUntil(355f, T0)), Is.EqualTo(ChopGrade.Good), "10 degrees before a spot at 5 degrees");
        }

        [Test]
        public void EvennessAveragesTheGrades()
        {
            var first = Next(Spot, T0);
            _dial.Hit(first);                              // good 1.0
            _dial.Hit(Next(Spot + 25f, first + 0.01));     // late 0.6
            Assert.That(_dial.Evenness, Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void DifferentIngredientsHaveTheirSpotInDifferentPlaces()
        {
            var other = new ChopDial(Turn, sweetSpot: 270f, goodHalfWidth: 15f, okHalfWidth: 35f);
            other.Start(T0);
            var time = Next(Spot, T0);
            Assert.That(_dial.GradeAt(time), Is.EqualTo(ChopGrade.Good));
            Assert.That(other.GradeAt(time), Is.EqualTo(ChopGrade.Miss));
        }

        // Decision 2026-11-02: Good speeds the needle up progressively.
        [Test]
        public void GoodCutsSpeedTheNeedleUpProgressively()
        {
            var t = T0;
            var speeds = new float[3];
            for (var i = 0; i < 3; i++)
            {
                t = Next(Spot, t + 0.01);
                _dial.Hit(t);
                speeds[i] = _dial.SpeedMultiplier;
            }
            Assert.That(speeds[0], Is.GreaterThan(1f));
            Assert.That(speeds[1], Is.GreaterThan(speeds[0]));
            Assert.That(speeds[2], Is.GreaterThan(speeds[1]));
        }

        [Test]
        public void EarlyOrLateSlowsTheNeedleSubtly()
        {
            _dial.Hit(Next(Spot + 25f, T0));
            Assert.That(_dial.SpeedMultiplier, Is.LessThan(1f).And.GreaterThan(0.85f));
        }

        [Test]
        public void AMissSlowsTheNeedleSignificantly()
        {
            _dial.Hit(Next(Spot + 180f, T0));
            Assert.That(_dial.SpeedMultiplier, Is.LessThanOrEqualTo(0.8f));
        }

        [Test]
        public void SpeedStaysWithinLimits()
        {
            var t = T0;
            for (var i = 0; i < 20; i++)
            {
                t = Next(Spot, t + 0.01);
                _dial.Hit(t);
            }
            Assert.That(_dial.SpeedMultiplier, Is.LessThanOrEqualTo(2f + 1e-4f));

            for (var i = 0; i < 20; i++)
            {
                t = Next(Spot + 180f, t + 0.01);
                _dial.Hit(t);
            }
            Assert.That(_dial.SpeedMultiplier, Is.GreaterThanOrEqualTo(0.5f - 1e-4f));
        }

        [Test]
        public void ChangingSpeedDoesntMakeTheNeedleJump()
        {
            var t = Next(Spot, T0);
            var before = _dial.NeedleAngle(t);
            _dial.Hit(t);
            Assert.That(_dial.NeedleAngle(t), Is.EqualTo(before).Within(0.01f));
        }
    }

    public class PrepCounterTests
    {
        private const float Turn = 0.8f;
        private const float Spot = 120f;
        private CookingSimulation _sim;
        private PrepCounter _counter;

        private static readonly IngredientProperties DicedOnion = new IngredientProperties
        {
            TypeId = "DicedOnion", HeatCapacity = 585f, SurfaceFraction = 0.3f, StartTemperature = 20f,
        };

        private Ingredient NewOnion() => _sim.AddIngredient(new IngredientProperties
        {
            TypeId = "Onion", HeatCapacity = 585f, SurfaceFraction = 0.15f, StartTemperature = 20f,
            CutIntoTypeId = "DicedOnion", CutsNeeded = 4,
            ChopSecondsPerTurn = Turn, SweetSpotAngle = Spot, SweetSpotGoodHalfWidth = 15f, SweetSpotOkHalfWidth = 35f,
        });

        [SetUp]
        public void CreateCounter()
        {
            _sim = new CookingSimulation(22f);
            _counter = new PrepCounter(slotCapacity: 2, whole => _sim.Transform(whole, DicedOnion));
        }

        // Presses each time the needle comes round to `offset` degrees from the sweet spot.
        private void ChopToTheEnd(double start = 10.0, float offset = 0f)
        {
            var t = start;
            for (var i = 0; i < 4; i++)
            {
                t += 0.01 + _counter.Dial.SecondsUntil(Spot + offset, t + 0.01);
                _counter.Chop(t);
            }
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
        public void UnevenTimingGivesAnUnevenCut()
        {
            _counter.Put(NewOnion());
            ChopToTheEnd(offset: 25f); // every chop lands just past the sweet spot
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
            _counter.Chop(11.0);
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
