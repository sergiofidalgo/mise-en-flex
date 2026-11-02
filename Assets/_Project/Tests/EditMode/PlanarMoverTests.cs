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

        private static void Run(PlanarMover mover, Vector2 stick, float seconds)
        {
            for (var t = 0f; t < seconds - Step * 0.5f; t += Step)
                mover.Tick(Step, stick);
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
    }
}
