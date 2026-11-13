using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Prep;
using Game.Simulation.Quality;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class ChopDialTests
    {
        private const float Sweep = 0.8f;   // seconds per sweep at base speed
        private const float Arc = 170f;     // width of the arc
        private const float Spot = 90f;     // sweet spot, degrees from the right end
        private const double T0 = 10.0;     // when the needle starts, at the right end
        private ChopDial _dial;

        [SetUp]
        public void CreateDial()
        {
            _dial = new ChopDial(Sweep, Arc, Spot, goodHalfWidth: 12f, okHalfWidth: 28f);
            _dial.Start(T0);
        }

        // The next real time the needle reaches `position`, from `now`.
        private double Next(float position, double now) => now + _dial.SecondsUntil(position, now);

        [Test]
        public void NeedleSweepsFromTheRightEndToTheLeftEnd()
        {
            Assert.That(_dial.NeedlePosition(T0), Is.EqualTo(0f).Within(0.01f), "starts at the right end");
            Assert.That(_dial.NeedlePosition(T0 + Sweep / 2f), Is.EqualTo(Arc / 2f).Within(0.01f), "halfway at the top");
            Assert.That(_dial.NeedlePosition(T0 + Sweep * 0.99f), Is.GreaterThan(Arc * 0.95f), "nearly at the left end");
        }

        // Feedback 2026-11-02: far right to far left and back, like a pendulum; no jump.
        [Test]
        public void AfterReachingTheLeftEndItSwingsBackToTheRight()
        {
            Assert.That(_dial.NeedlePosition(T0 + Sweep * 1.1f), Is.EqualTo(Arc * 0.9f).Within(0.01f), "on its way back");
            Assert.That(_dial.MovingLeft(T0 + Sweep * 1.1f), Is.False);
            Assert.That(_dial.NeedlePosition(T0 + Sweep * 2f), Is.EqualTo(0f).Within(0.01f), "back at the right end");
        }

        [Test]
        public void NeverJumpsAtEitherEnd()
        {
            const double tiny = 0.002;
            foreach (var end in new[] { T0 + Sweep, T0 + 2.0 * Sweep })
            {
                var before = _dial.NeedlePosition(end - tiny);
                var after = _dial.NeedlePosition(end + tiny);
                Assert.That(System.Math.Abs(after - before), Is.LessThan(2f), $"jump at t={end}");
            }
        }

        [Test]
        public void OnTheWayBackEarlyAndLateFollowTheDirectionOfTravel()
        {
            var speed = Arc / Sweep;
            // Coming back (left to right), the needle reaches Spot+20 before the spot: that's Early.
            var returning = T0 + (2.0 * Arc - (Spot + 20f)) / speed;
            Assert.That(_dial.MovingLeft(returning), Is.False);
            Assert.That(_dial.GradeAt(returning), Is.EqualTo(ChopGrade.Early));
            var pastIt = T0 + (2.0 * Arc - (Spot - 20f)) / speed;
            Assert.That(_dial.GradeAt(pastIt), Is.EqualTo(ChopGrade.Late));
        }

        [Test]
        public void RemembersEachCutsGradeAndWhereItLanded()
        {
            var first = Next(Spot, T0);
            _dial.Hit(first);
            _dial.Hit(Next(10f, first + 0.01));
            Assert.That(_dial.Grades, Is.EqualTo(new[] { ChopGrade.Good, ChopGrade.Miss }));
            Assert.That(_dial.LastHitPosition, Is.EqualTo(10f).Within(0.5f));
        }

        [Test]
        public void PressingInTheSweetSpotIsGood()
        {
            Assert.That(_dial.Hit(Next(Spot + 6f, T0)), Is.EqualTo(ChopGrade.Good));
        }

        [Test]
        public void JustBeforeTheSpotIsEarlyAndJustAfterIsLate()
        {
            Assert.That(_dial.GradeAt(Next(Spot - 20f, T0)), Is.EqualTo(ChopGrade.Early));
            Assert.That(_dial.GradeAt(Next(Spot + 20f, T0)), Is.EqualTo(ChopGrade.Late));
        }

        [Test]
        public void FarFromTheSpotIsAMiss()
        {
            Assert.That(_dial.GradeAt(Next(10f, T0)), Is.EqualTo(ChopGrade.Miss));
        }

        [Test]
        public void EvennessAveragesTheGrades()
        {
            var first = Next(Spot, T0);
            _dial.Hit(first);                              // good 1.0
            _dial.Hit(Next(Spot + 20f, first + 0.01));     // late 0.6
            Assert.That(_dial.Evenness, Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void DifferentIngredientsHaveTheirSpotInDifferentPlaces()
        {
            var other = new ChopDial(Sweep, Arc, sweetSpot: 150f, goodHalfWidth: 12f, okHalfWidth: 28f);
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
            _dial.Hit(Next(Spot + 20f, T0));
            Assert.That(_dial.SpeedMultiplier, Is.LessThan(1f).And.GreaterThan(0.85f));
        }

        [Test]
        public void AMissSlowsTheNeedleSignificantly()
        {
            _dial.Hit(Next(10f, T0));
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
                t = Next(10f, t + 0.01);
                _dial.Hit(t);
            }
            Assert.That(_dial.SpeedMultiplier, Is.GreaterThanOrEqualTo(0.5f - 1e-4f));
        }

        [Test]
        public void ChangingSpeedDoesntMakeTheNeedleJump()
        {
            var t = Next(Spot, T0);
            var before = _dial.NeedlePosition(t);
            _dial.Hit(t);
            Assert.That(_dial.NeedlePosition(t), Is.EqualTo(before).Within(0.01f));
        }
    }

    public class PrepCounterTests
    {
        private const float Sweep = 0.8f;
        private const float Spot = 110f;
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
            ChopSecondsPerSweep = Sweep, SweetSpotPosition = Spot, SweetSpotGoodHalfWidth = 12f, SweetSpotOkHalfWidth = 28f,
        });

        [SetUp]
        public void CreateCounter()
        {
            _sim = new CookingSimulation(22f);
            _counter = new PrepCounter(whole => _sim.Transform(whole, DicedOnion));
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
        public void TheFinishedCutStaysOnTheBoard()
        {
            _counter.Put(NewOnion());
            ChopToTheEnd();

            var diced = (Ingredient)_counter.Current;
            Assert.That(diced.TypeId, Is.EqualTo("DicedOnion"));
            Assert.That(diced.CutEvenness, Is.EqualTo(1f).Within(0.001f));
            Assert.That(_counter.CanChop, Is.False);
        }

        [Test]
        public void UnevenTimingGivesAnUnevenCut()
        {
            _counter.Put(NewOnion());
            ChopToTheEnd(offset: 20f); // every chop lands just past the sweet spot
            Assert.That(((Ingredient)_counter.Current).CutEvenness, Is.LessThan(0.8f));
        }

        [Test]
        public void AFinishedCutBlocksTheNextOneUntilMoved()
        {
            _counter.Put(NewOnion());
            ChopToTheEnd();
            Assert.That(_counter.CanAccept(NewOnion()), Is.False);

            var hands = new ItemSlot();
            Assert.That(ItemTransfer.Interact(hands, _counter), Is.EqualTo(InteractionOutcome.PickedUp));
            Assert.That(hands.Current.TypeId, Is.EqualTo("DicedOnion"));
            Assert.That(_counter.CanAccept(NewOnion()));
        }

        // A cut that cuts further (decision 2026-11-08): an onion into rings, the rings into dice.

        private static readonly IngredientProperties Rings = new IngredientProperties
        {
            TypeId = "OnionRings", HeatCapacity = 585f, SurfaceFraction = 0.25f, StartTemperature = 20f,
            CutIntoTypeId = "DicedOnion", CutsNeeded = 3,
            ChopSecondsPerSweep = Sweep, SweetSpotPosition = Spot, SweetSpotGoodHalfWidth = 12f, SweetSpotOkHalfWidth = 28f,
        };

        private PrepCounter ChainCounter() =>
            new PrepCounter(whole => _sim.Transform(whole, whole.TypeId == "Onion" ? Rings : DicedOnion));

        private static double ChopTimes(PrepCounter counter, int times, double t)
        {
            for (var i = 0; i < times; i++)
            {
                t += 0.01 + counter.Dial.SecondsUntil(Spot, t + 0.01);
                counter.Chop(t);
            }
            return t;
        }

        private Ingredient NewOnionForRings() => _sim.AddIngredient(new IngredientProperties
        {
            TypeId = "Onion", HeatCapacity = 585f, SurfaceFraction = 0.15f, StartTemperature = 20f,
            CutIntoTypeId = "OnionRings", CutsNeeded = 2,
            ChopSecondsPerSweep = Sweep, SweetSpotPosition = Spot, SweetSpotGoodHalfWidth = 12f, SweetSpotOkHalfWidth = 28f,
        });

        // Each cut is a stage (the user's call, 2026-11-13): the finished rings rest on the board
        // until they're moved, and dicing them means putting them back.

        [Test]
        public void AFinishedCutRestsUntilMoved()
        {
            var counter = ChainCounter();
            counter.Put(NewOnionForRings());
            var t = ChopTimes(counter, 2, 10.0);

            Assert.That(counter.Current.TypeId, Is.EqualTo("OnionRings"));
            Assert.That(counter.Resting, "the rings wait to be picked up");
            Assert.That(counter.CanChop, Is.False, "no cutting on into dice without moving them");
            Assert.That(counter.Chop(t + 1.0), Is.Null);
            Assert.That(counter.Current.TypeId, Is.EqualTo("OnionRings"), "still rings");
            Assert.That(counter.Take().TypeId, Is.EqualTo("OnionRings"), "taken off, they stay rings");
        }

        [Test]
        public void PuttingTheRingsBackCutsThemIntoDice()
        {
            var counter = ChainCounter();
            counter.Put(NewOnionForRings());
            var t = ChopTimes(counter, 2, 10.0);
            var rings = counter.Take();
            counter.Put(rings);

            Assert.That(counter.Resting, Is.False);
            Assert.That(counter.CanChop, "rings put back can be diced");
            Assert.That(counter.Cuts, Is.Zero, "a fresh count for the next cut");
            ChopTimes(counter, 3, t);

            Assert.That(counter.Current.TypeId, Is.EqualTo("DicedOnion"));
            Assert.That(counter.CanChop, Is.False);
        }

        [Test]
        public void StaffCutOnPastARestingStage()
        {
            var counter = ChainCounter();
            counter.Put(NewOnionForRings());
            Assert.That(counter.CutWithoutDial(1f).TypeId, Is.EqualTo("OnionRings"));
            Assert.That(counter.CanCutFurther, "staff aren't stopped by the rest");
            Assert.That(counter.CutWithoutDial(1f).TypeId, Is.EqualTo("DicedOnion"));
        }

        [Test]
        public void OnlyFoodToCutGoesOnTheBoard()
        {
            var diced = _sim.AddIngredient(DicedOnion);
            Assert.That(_counter.CanAccept(diced), Is.False);
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
            var diced = (Ingredient)_counter.Current;

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
