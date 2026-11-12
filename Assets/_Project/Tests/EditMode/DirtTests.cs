using System;
using System.Linq;
using System.Numerics;
using Game.Simulation.Cleaning;
using Game.Simulation.Quality;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Dirt (decisions 2026-11-08 and 2026-11-10): it comes from what happened, piles up where
    /// things happen, is wiped away by tracing a shape, and costs a little in reviews.
    /// </summary>
    public class DirtTests
    {
        private DirtSettings _settings;
        private DirtMap _dirt;
        private MessMaker _mess;
        private readonly object _table = new object();
        private readonly object _otherTable = new object();

        [SetUp]
        public void Create()
        {
            _settings = new DirtSettings();
            _dirt = new DirtMap(_settings);
            _mess = new MessMaker(_dirt);
        }

        [Test]
        public void EachKindOfEventLeavesItsOwnDirtWhereItHappened()
        {
            var smudge = _mess.DinerFinished(DirtSurface.Table, _table, 0.2f, 0f);
            var spill = _mess.PlateBroken(3f, 4f);
            var scorch = _mess.PanBurnt(_otherTable, 0f, 0.1f);
            var foam = _mess.FireOut(-2f, 1f);

            Assert.That(smudge.Kind, Is.EqualTo(DirtKind.Smudge));
            Assert.That(smudge.Surface, Is.EqualTo(DirtSurface.Table));
            Assert.That(smudge.Host, Is.SameAs(_table));
            Assert.That(smudge.Amount, Is.EqualTo(_settings.DinerSmudge));

            Assert.That(spill.Surface, Is.EqualTo(DirtSurface.Floor));
            Assert.That(spill.Host, Is.Null);
            Assert.That((spill.X, spill.Z), Is.EqualTo((3f, 4f)));
            Assert.That(spill.Amount, Is.EqualTo(_settings.PlateSpill));

            Assert.That(scorch.Surface, Is.EqualTo(DirtSurface.Counter));
            Assert.That(scorch.Amount, Is.EqualTo(_settings.BurntScorch));
            Assert.That(foam.Kind, Is.EqualTo(DirtKind.Foam));
            Assert.That(_dirt.Spots.Count, Is.EqualTo(4));
        }

        [Test]
        public void ABurntPanWithNoCounterBesideItDirtiesTheFloor()
        {
            var scorch = _mess.PanBurnt(null, 1f, 1f);
            Assert.That(scorch.Surface, Is.EqualTo(DirtSurface.Floor));
        }

        [Test]
        public void ABusyFloorScuffsEveryFewGuests()
        {
            _settings.GuestsPerScuff = 6;
            var scuffs = Enumerable.Range(0, 18).Select(i => _mess.GuestWalked(i * 2f, 0f)).Count(s => s != null);
            Assert.That(scuffs, Is.EqualTo(3));
        }

        [Test]
        public void AChaoticServiceLeavesMoreThanATidyOne()
        {
            // Tidy: six diners at two tables, nothing dropped or burnt.
            for (var i = 0; i < 6; i++)
            {
                _mess.DinerFinished(DirtSurface.Table, i % 2 == 0 ? _table : _otherTable, 0f, 0f);
                _mess.GuestWalked(0f, i);
            }
            var tidy = _dirt.Total;

            var chaotic = new DirtMap(_settings);
            var mess = new MessMaker(chaotic);
            for (var i = 0; i < 6; i++)
            {
                mess.DinerFinished(DirtSurface.Table, i % 2 == 0 ? _table : _otherTable, 0f, 0f);
                mess.GuestWalked(0f, i);
            }
            mess.PlateBroken(2f, 2f);
            mess.PanBurnt(null, 5f, 5f);
            mess.FireOut(5f, 4f);

            // A dropped plate, a burnt pan and a fire put out double the morning's work.
            Assert.That(chaotic.Total, Is.GreaterThanOrEqualTo(tidy * 2f));
        }

        [Test]
        public void DirtCloseByOnTheSameSurfaceAddsToOneSpotUpToItsMost()
        {
            for (var i = 0; i < 10; i++)
                _mess.DinerFinished(DirtSurface.Table, _table, 0.05f * (i % 2), 0f);

            Assert.That(_dirt.Spots.Count, Is.EqualTo(1));
            Assert.That(_dirt.Spots[0].Amount, Is.EqualTo(_settings.MaxPerSpot));
        }

        [Test]
        public void DirtOnAnotherPieceOrFarAwayMakesItsOwnSpot()
        {
            _mess.DinerFinished(DirtSurface.Table, _table, 0f, 0f);
            _mess.DinerFinished(DirtSurface.Table, _otherTable, 0f, 0f);
            _mess.PlateBroken(0f, 0f);
            _mess.PlateBroken(2f, 0f);
            Assert.That(_dirt.Spots.Count, Is.EqualTo(4));
        }

        [Test]
        public void DifferentKindsInOnePlaceStayApartSoEachLooksLikeItself()
        {
            _mess.PanBurnt(null, 1f, 1f);
            _mess.FireOut(1.05f, 1f);
            Assert.That(_dirt.Spots.Select(s => s.Kind), Is.EquivalentTo(new[] { DirtKind.Scorch, DirtKind.Foam }));
        }

        [Test]
        public void WipingTakesItAwayBitByBitThenItGoes()
        {
            var spill = _mess.PlateBroken(0f, 0f);
            DirtSpot removed = null;
            _dirt.Removed += s => removed = s;

            Assert.That(_dirt.Clean(spill, 1f), Is.False);
            Assert.That(spill.Remaining, Is.EqualTo(2f / 3f).Within(1e-4f));
            Assert.That(_dirt.Clean(spill, 2f), Is.True);
            Assert.That(_dirt.Spots, Is.Empty);
            Assert.That(removed, Is.SameAs(spill));
        }

        [Test]
        public void SoldFurnitureTakesItsDirtWithIt()
        {
            _mess.DinerFinished(DirtSurface.Table, _table, 0f, 0f);
            _mess.DinerFinished(DirtSurface.Table, _otherTable, 0f, 0f);
            _mess.PlateBroken(0f, 0f);

            _dirt.RemoveOn(_table);

            Assert.That(_dirt.On(_table), Is.Empty);
            Assert.That(_dirt.Spots.Count, Is.EqualTo(2));
        }

        [Test]
        public void ASavedSpotComesBackPartlyWipedAsItWas()
        {
            var spot = _dirt.Restore(DirtKind.Spill, DirtSurface.Floor, null, 1f, 2f, 1.5f, 3f);
            Assert.That(spot.Remaining, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(_dirt.Restore(DirtKind.Spill, DirtSurface.Floor, null, 1f, 2f, 0f, 3f), Is.Null, "a clean spot isn't kept");
        }

        [Test]
        public void GuestsMindADirtyTableAndAMessyFloorUpToALimit()
        {
            Assert.That(_dirt.PenaltyFor(_table, 0f, 0f), Is.EqualTo(0f));

            _mess.DinerFinished(DirtSurface.Table, _table, 0f, 0f);
            var table = _dirt.PenaltyFor(_table, 0f, 0f);
            Assert.That(table, Is.EqualTo(_settings.TablePenalty).Within(1e-4f));
            Assert.That(_dirt.PenaltyFor(_otherTable, 0f, 0f), Is.EqualTo(0f), "someone else's table");

            _mess.PlateBroken(0.5f, 0.5f);
            Assert.That(_dirt.PenaltyFor(_table, 0f, 0f), Is.GreaterThan(table));
            Assert.That(_dirt.PenaltyFor(_otherTable, 10f, 10f), Is.EqualTo(0f), "far from the spill");

            for (var i = 0; i < 10; i++)
                _mess.PlateBroken(i * 0.4f - 1f, 0f);
            Assert.That(_dirt.PenaltyFor(_table, 0f, 0f), Is.EqualTo(_settings.MaxPenalty));
        }
    }

    /// <summary>Dirt in reviews (decision 2026-11-10): it costs a little, and a lot of it is what's remembered.</summary>
    public class DirtInReviewsTests
    {
        private static DishScore Good() => new DishScore(new ItemScore[0], 0.9f);

        [Test]
        public void DirtComesOffTheScoreAndSoTheStars()
        {
            var dirty = Good().WithDirt(0.2f, 0.05f);
            Assert.That(dirty.Overall, Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(dirty.Stars, Is.LessThan(Good().Stars));
            Assert.That(Good().WithDirt(0f, 0.05f).Overall, Is.EqualTo(0.9f));
        }

        [Test]
        public void ANoticeableMessIsWhatAGoodMealIsRememberedFor()
        {
            var settings = new ReviewSettings();
            Assert.That(ReviewWriter.ChooseTopic(Good().WithDirt(0.1f, 0.05f), 1f, settings), Is.EqualTo(ReviewTopic.Dirty));
            Assert.That(ReviewWriter.ChooseTopic(Good().WithDirt(0.02f, 0.05f), 1f, settings), Is.Not.EqualTo(ReviewTopic.Dirty), "a crumb isn't worth a review");
        }

        [Test]
        public void DirtIsRememberedBeforeASlowWait()
        {
            var settings = new ReviewSettings();
            // The dirt beats slow service: you sat at that table the whole time.
            Assert.That(ReviewWriter.ChooseTopic(Good().WithDirt(0.1f, 0.05f), 0f, settings), Is.EqualTo(ReviewTopic.Dirty));
        }
    }

    /// <summary>The cleaning game (decision 2026-11-11): spin the stick (cloth, mop) or sweep it (extinguisher).</summary>
    public class TraceGameTests
    {
        private const float Frame = 1f / 60f;
        private readonly TraceGameSettings _settings = new TraceGameSettings();

        // Spins the stick round at `turnsPerSecond` (negative: the other way), pushed `push` of the way.
        private static float Spin(TraceGame game, float seconds, float turnsPerSecond, float push = 0.9f)
        {
            var wiped = 0f;
            for (var t = 0f; t < seconds; t += Frame)
            {
                var angle = t * turnsPerSecond * Math.PI * 2.0;
                wiped += game.Tick(Frame, new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * push);
            }
            return wiped;
        }

        // Wags the stick side to side, `sweepsPerSecond` times there and back, `reach` of the way out.
        private static float Wag(TraceGame game, float seconds, float sweepsPerSecond, float reach = 0.9f)
        {
            var wiped = 0f;
            for (var t = 0f; t < seconds; t += Frame)
                wiped += game.Tick(Frame, new Vector2((float)(reach * Math.Sin(t * sweepsPerSecond * Math.PI * 2.0)), 0f));
            return wiped;
        }

        [TestCase(TraceShape.Circle)]
        public void SpinningQuicklyWipesAtTheToolsSpeed(TraceShape shape)
        {
            var game = new TraceGame(shape, _settings);
            var wiped = Spin(game, 3f, turnsPerSecond: 2.5f);
            var top = _settings.SpeedFor(shape) * 3f;
            Assert.That(wiped, Is.InRange(top * 0.9f, top + _settings.SpeedFor(shape) * Frame * 2f), "capped at the tool's speed");
            Assert.That(game.Loops, Is.EqualTo(wiped).Within(1e-3f));
            Assert.That(game.OnTrack, Is.True);
        }

        [Test]
        public void SpinningSlowlyWipesSlowlyEveryTurnCounts()
        {
            var game = new TraceGame(TraceShape.Circle, _settings);
            Assert.That(Spin(game, 4f, turnsPerSecond: 0.5f), Is.EqualTo(2f).Within(0.1f));
        }

        [Test]
        public void EitherWayRoundCleans()
        {
            Assert.That(Spin(new TraceGame(TraceShape.Circle, _settings), 3f, turnsPerSecond: -0.8f), Is.EqualTo(2.4f).Within(0.15f));
        }

        [Test]
        public void ChangingDirectionKeepsCleaningAfterTheTurnBack()
        {
            var game = new TraceGame(TraceShape.Circle, _settings);
            var one = Spin(game, 2f, 0.8f);
            var other = Spin(game, 3f, -0.8f);
            Assert.That(one, Is.GreaterThan(1.4f));
            Assert.That(other, Is.GreaterThan(1.4f), "the other way counts once it's clearly spinning that way");
        }

        [Test]
        public void ARestingOrBarelyPushedStickDoesNothing()
        {
            var game = new TraceGame(TraceShape.Circle, _settings);
            Assert.That(Spin(game, 4f, 1f, push: _settings.MinPush * 0.7f), Is.EqualTo(0f));
            Assert.That(game.OnTrack, Is.False);
        }

        [Test]
        public void WaggingTheStickBackAndForthDoesNotSpin()
        {
            // ±100° back and forth, twice a second, for five seconds.
            var game = new TraceGame(TraceShape.Circle, _settings);
            var wiped = 0f;
            for (var t = 0f; t < 5f; t += Frame)
            {
                var angle = Math.Sin(t * 4.0 * Math.PI) * (100.0 * Math.PI / 180.0);
                wiped += game.Tick(Frame, new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 0.9f);
            }
            Assert.That(wiped, Is.LessThan(0.35f), "only the first swing's ground counts");
        }

        [Test]
        public void SlammingTheStickAboutWipesFarLessThanSpinning()
        {
            var random = new Random(3);
            var game = new TraceGame(TraceShape.Circle, _settings);
            var wiped = 0f;
            for (var t = 0f; t < 5f; t += Frame)
            {
                var angle = random.NextDouble() * Math.PI * 2;
                wiped += game.Tick(Frame, new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)));
            }
            var spun = Spin(new TraceGame(TraceShape.Circle, _settings), 5f, 1f);
            Assert.That(wiped, Is.LessThan(spun * 0.5f));
        }

        [Test]
        public void SweepingSideToSidePutsOutAFire()
        {
            var game = new TraceGame(TraceShape.Sweep, _settings);
            var wiped = Wag(game, 4f, sweepsPerSecond: 0.6f);
            Assert.That(wiped, Is.EqualTo(0.6f * 4f).Within(0.35f), "a loop per sweep there and back");
        }

        [Test]
        public void TheMopSweepsSideToSideToo()
        {
            // The user's call (2026-11-12): the mop is swept like the extinguisher, a little slower.
            var mop = Wag(new TraceGame(TraceShape.MopSweep, _settings), 4f, sweepsPerSecond: 0.6f);
            Assert.That(mop, Is.EqualTo(0.6f * 4f).Within(0.35f));
            Assert.That(_settings.MopSpeed, Is.LessThan(_settings.SweepSpeed));
        }

        [Test]
        public void SweepingQuicklyIsCappedAtTheExtinguishersSpeed()
        {
            var wiped = Wag(new TraceGame(TraceShape.Sweep, _settings), 3f, sweepsPerSecond: 3f);
            Assert.That(wiped, Is.LessThanOrEqualTo(_settings.SweepSpeed * 3f + 0.05f));
            Assert.That(wiped, Is.GreaterThan(_settings.SweepSpeed * 3f * 0.6f));
        }

        [Test]
        public void JigglingInTheMiddleOrNotReachingTheSidesDoesNothingMuch()
        {
            var jiggle = Wag(new TraceGame(TraceShape.Sweep, _settings), 5f, sweepsPerSecond: 2f, reach: 0.2f);
            Assert.That(jiggle, Is.LessThan(0.2f));
        }

        [TestCase(TraceShape.Circle)]
        [TestCase(TraceShape.MopSweep)]
        [TestCase(TraceShape.Sweep)]
        public void EveryMotionFitsInsideTheStick(TraceShape shape)
        {
            for (var i = 0; i < 100; i++)
                Assert.That(TraceGame.PointAt(shape, i / 100f).Length(), Is.LessThanOrEqualTo(0.85f));
        }

        [TestCase(TraceShape.Circle)]
        [TestCase(TraceShape.MopSweep)]
        [TestCase(TraceShape.Sweep)]
        public void FollowingTheHintsPathCleans(TraceShape shape)
        {
            // As the play test's hand does: round the hint's path at a steady loop a second.
            var game = new TraceGame(shape, _settings);
            for (var t = 0f; t < 4f; t += Frame)
                game.Tick(Frame, TraceGame.PointAt(shape, t));
            Assert.That(game.Loops, Is.GreaterThan(_settings.SpeedFor(shape) * 4f * 0.6f));
        }
    }
}
