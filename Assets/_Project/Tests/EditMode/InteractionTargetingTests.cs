using System.Numerics;
using Game.Simulation.Interaction;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class InteractionTargetingTests
    {
        private static readonly TargetingSettings Settings = new TargetingSettings(
            range: 1.5f, halfAngleDegrees: 75f, stickiness: 0.2f);

        private static int Select(float facing, params Vector2[] candidates) =>
            InteractionTargeting.SelectBest(Vector2.Zero, facing, candidates, Settings);

        [Test]
        public void TargetsStationStraightAhead()
        {
            Assert.That(Select(0f, new Vector2(0f, 1f)), Is.EqualTo(0));
        }

        [Test]
        public void IgnoresStationBehind()
        {
            Assert.That(Select(0f, new Vector2(0f, -1f)), Is.EqualTo(-1));
        }

        [Test]
        public void IgnoresStationOutOfRange()
        {
            Assert.That(Select(0f, new Vector2(0f, 1.6f)), Is.EqualTo(-1));
        }

        [Test]
        public void ForgivesAStationWellOffToTheSide()
        {
            // 60 degrees off-centre is still inside the 75-degree cone.
            Assert.That(Select(0f, new Vector2(0.87f, 0.5f)), Is.EqualTo(0));
        }

        [Test]
        public void UsesFacingDirection()
        {
            // Facing right (90): the station on the right wins, the one ahead is outside the cone.
            Assert.That(Select(90f, new Vector2(0f, 1f), new Vector2(1f, 0f)), Is.EqualTo(1));
        }

        [Test]
        public void PrefersCloserStationAtSameAngle()
        {
            Assert.That(Select(0f, new Vector2(0f, 1.4f), new Vector2(0f, 0.9f)), Is.EqualTo(1));
        }

        [Test]
        public void PrefersCentredStationAtSameDistance()
        {
            Assert.That(Select(0f, new Vector2(0.7f, 0.7f), new Vector2(0f, 1f)), Is.EqualTo(1));
        }

        [Test]
        public void KeepsCurrentTargetWhenNearlyTied()
        {
            // Two counters side by side; the player faces almost exactly between them.
            var candidates = new[] { new Vector2(-0.5f, 1f), new Vector2(0.5f, 1f) };
            var facingSlightlyRight = 2f;

            var fresh = InteractionTargeting.SelectBest(Vector2.Zero, facingSlightlyRight, candidates, Settings);
            var sticky = InteractionTargeting.SelectBest(Vector2.Zero, facingSlightlyRight, candidates, Settings, currentIndex: 0);

            Assert.That(fresh, Is.EqualTo(1));
            Assert.That(sticky, Is.EqualTo(0));
        }

        [Test]
        public void StationOnTopOfPlayerCountsAsAhead()
        {
            Assert.That(Select(180f, Vector2.Zero), Is.EqualTo(0));
        }

        [Test]
        public void NoCandidatesMeansNoTarget()
        {
            Assert.That(Select(0f), Is.EqualTo(-1));
        }
    }
}
