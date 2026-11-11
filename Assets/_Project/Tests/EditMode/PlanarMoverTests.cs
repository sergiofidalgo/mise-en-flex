using System.Numerics;
using Game.Simulation.Movement;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class PlanarMoverTests
    {
        private const float Step = 1f / 60f;
        private const float Tolerance = 1e-3f;

        private static readonly MovementSettings Snappy = new MovementSettings(
            maxSpeed: 5f, accelerationTime: 0.08f, decelerationTime: 0.06f, turnSpeedDegrees: 1080f);

        private static void Run(PlanarMover mover, Vector2 stick, float seconds, bool turnOnly = false)
        {
            for (var t = 0f; t < seconds - Step * 0.5f; t += Step)
                mover.Tick(Step, stick, turnOnly);
        }

        [Test]
        public void ReachesFullSpeedWithinAccelerationTime()
        {
            var mover = new PlanarMover(Snappy);
            Run(mover, new Vector2(0f, 1f), 0.1f);
            Assert.That(mover.Velocity.Length(), Is.EqualTo(5f).Within(Tolerance));
        }

        [Test]
        public void IsStillAcceleratingHalfwayThroughAccelerationTime()
        {
            var mover = new PlanarMover(Snappy);
            Run(mover, new Vector2(0f, 1f), 0.04f);
            Assert.That(mover.Velocity.Length(), Is.GreaterThan(1f).And.LessThan(4f));
        }

        [Test]
        public void StopsWithinDecelerationTimeAfterRelease()
        {
            var mover = new PlanarMover(Snappy);
            Run(mover, new Vector2(1f, 0f), 0.2f);
            Run(mover, Vector2.Zero, 0.07f);
            Assert.That(mover.Velocity.Length(), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void DiagonalIsNotFasterThanStraight()
        {
            var mover = new PlanarMover(Snappy);
            Run(mover, new Vector2(1f, 1f), 0.2f);
            Assert.That(mover.Velocity.Length(), Is.EqualTo(5f).Within(Tolerance));
        }

        [Test]
        public void HalfStickGivesHalfSpeed()
        {
            var mover = new PlanarMover(Snappy);
            Run(mover, new Vector2(0.5f, 0f), 0.2f);
            Assert.That(mover.Velocity.Length(), Is.EqualTo(2.5f).Within(Tolerance));
        }

        [Test]
        public void ZeroRampTimesAreInstant()
        {
            var mover = new PlanarMover(new MovementSettings(5f, 0f, 0f, 1080f));
            mover.Tick(Step, new Vector2(0f, 1f));
            Assert.That(mover.Velocity.Length(), Is.EqualTo(5f).Within(Tolerance));
            mover.Tick(Step, Vector2.Zero);
            Assert.That(mover.Velocity.Length(), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ResultDoesNotDependOnFrameRate()
        {
            var at60 = new PlanarMover(Snappy);
            var at30 = new PlanarMover(Snappy);
            for (var i = 0; i < 4; i++) at60.Tick(1f / 60f, new Vector2(1f, 0f));
            for (var i = 0; i < 2; i++) at30.Tick(1f / 30f, new Vector2(1f, 0f));
            Assert.That(at30.Velocity.X, Is.EqualTo(at60.Velocity.X).Within(Tolerance));
        }

        [Test]
        public void FacesStickDirection()
        {
            var mover = new PlanarMover(Snappy);
            Run(mover, new Vector2(1f, 0f), 0.2f); // right = 90 degrees
            Assert.That(mover.FacingDegrees, Is.EqualTo(90f).Within(Tolerance));
        }

        [Test]
        public void TurnIsLimitedByTurnSpeed()
        {
            var mover = new PlanarMover(Snappy);
            mover.Tick(Step, new Vector2(0f, -1f)); // asks for 180; one frame allows 18
            Assert.That(System.Math.Abs(PlanarMover.DeltaDegrees(0f, mover.FacingDegrees)), Is.EqualTo(18f).Within(Tolerance));
        }

        [Test]
        public void TurnsTheShortWayAcrossZero()
        {
            var mover = new PlanarMover(Snappy, facingDegrees: 350f);
            mover.Tick(Step, new Vector2(0.17f, 1f)); // ~10 degrees: should cross 0, not swing round
            Assert.That(PlanarMover.DeltaDegrees(350f, mover.FacingDegrees), Is.GreaterThan(0f));
        }

        [Test]
        public void KeepsFacingWhenStickIsReleased()
        {
            var mover = new PlanarMover(Snappy);
            Run(mover, new Vector2(-1f, 0f), 0.2f); // left = 270
            Run(mover, Vector2.Zero, 0.2f);
            Assert.That(mover.FacingDegrees, Is.EqualTo(270f).Within(Tolerance));
        }

        // ---- Playtest round, 2026-11-11: gentler turning and a facing assist ----

        private static readonly MovementSettings Assisted = new MovementSettings(
            maxSpeed: 5f, accelerationTime: 0.08f, decelerationTime: 0.06f, turnSpeedDegrees: 600f,
            lightPushTurnShare: 0.35f, assistRange: 1.3f, assistHalfAngleDegrees: 40f, assistTurnSpeedDegrees: 300f);

        [Test]
        public void ALightPushTurnsMoreGentlyThanAFullOne()
        {
            var light = new PlanarMover(Assisted);
            var full = new PlanarMover(Assisted);
            light.Tick(Step, new Vector2(0.2f, 0f));
            full.Tick(Step, new Vector2(1f, 0f));
            Assert.That(full.FacingDegrees, Is.EqualTo(10f).Within(Tolerance), "600°/s at full push");
            Assert.That(light.FacingDegrees, Is.EqualTo(600f * (0.35f + 0.65f * 0.2f) * Step).Within(Tolerance));
            Assert.That(light.FacingDegrees, Is.LessThan(full.FacingDegrees * 0.5f));
        }

        [Test]
        public void ALightNudgeDoesntSwingTheChefRound()
        {
            // A quick sideways nudge, a tenth of a second at a fifth of a push.
            var mover = new PlanarMover(Assisted);
            Run(mover, new Vector2(0.2f, 0f), 0.1f);
            Assert.That(mover.FacingDegrees, Is.LessThan(30f));
        }

        [Test]
        public void PlantedTheStickTurnsOnTheSpotWithoutWalking()
        {
            var mover = new PlanarMover(Assisted);
            Run(mover, new Vector2(1f, 0f), 0.3f, turnOnly: true);
            Assert.That(mover.Velocity.Length(), Is.Zero);
            Assert.That(mover.FacingDegrees, Is.EqualTo(90f).Within(Tolerance));
        }

        [Test]
        public void PlantingStopsAWalkingChef()
        {
            var mover = new PlanarMover(Assisted);
            Run(mover, new Vector2(0f, 1f), 0.2f);
            Run(mover, new Vector2(0f, 1f), 0.1f, turnOnly: true);
            Assert.That(mover.Velocity.Length(), Is.Zero.Within(Tolerance));
        }

        [Test]
        public void AStoppedChefTurnsToAStationRoughlyInFront()
        {
            var mover = new PlanarMover(Assisted); // facing +Y
            Run(mover, Vector2.Zero, 0.1f);
            var station = new Vector2(0.6f, 0.8f); // 1 m away, about 37° to the right
            for (var i = 0; i < 60; i++)
                mover.Assist(Step, Vector2.Zero, station);
            Assert.That(mover.FacingDegrees, Is.EqualTo(36.87f).Within(0.05f));
        }

        [Test]
        public void TheAssistLeavesStationsOutsideItsConeOrRange()
        {
            var mover = new PlanarMover(Assisted);
            Run(mover, Vector2.Zero, 0.1f);
            Assert.That(mover.Assist(Step, Vector2.Zero, new Vector2(1f, 0.2f)), Is.False, "off to the side");
            Assert.That(mover.Assist(Step, Vector2.Zero, new Vector2(0.2f, 2f)), Is.False, "too far");
            Assert.That(mover.FacingDegrees, Is.Zero);
        }

        [Test]
        public void TheAssistNeverFightsTheStick()
        {
            var mover = new PlanarMover(Assisted);
            mover.Tick(Step, new Vector2(0f, 1f));
            Assert.That(mover.Assist(Step, Vector2.Zero, new Vector2(0.6f, 0.8f)), Is.False, "steering");
            Run(mover, new Vector2(0f, 1f), 0.2f);
            mover.Tick(Step, Vector2.Zero);
            Assert.That(mover.IsSettled, Is.False, "still sliding to a stop");
        }

        [Test]
        public void TheRealTuningTurnsGentlyAndAssists()
        {
            var movement = UnityEditor.AssetDatabase.LoadAssetAtPath<Game.Data.TuningConfig>("Assets/_Project/Data/Settings/TuningConfig.asset").Movement;
            Assert.That(movement.TurnSpeedDegrees, Is.LessThan(1080f), "slower than before the playtest");
            Assert.That(movement.LightPushTurnShare, Is.LessThan(1f));
            Assert.That(movement.AssistRange, Is.GreaterThan(0f));
            Assert.That(movement.AssistHalfAngleDegrees, Is.GreaterThan(0f).And.LessThan(90f));
        }
    }
}
