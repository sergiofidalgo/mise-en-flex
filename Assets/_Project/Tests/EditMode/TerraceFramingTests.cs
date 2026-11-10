using System.Numerics;
using Game.Simulation.Movement;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class TerraceFramingTests
    {
        // The office's terrace (OfficeBuilder): 35 m across, from the back rooms' wall to the rail.
        private static readonly Vector2 TerraceMin = new Vector2(-17.5f, 13.5f);
        private static readonly Vector2 TerraceMax = new Vector2(17.5f, 19.5f);
        private const float Slack = 0.5f;
        private const float Beyond = 20f;

        private static Vector3 Fit(params Vector2[] points) =>
            TerraceFraming.Fit(points, TerraceMin, TerraceMax, Beyond, Slack, 55f, 45f, 16f / 9f, 3f, 10f, 55f, 0.15f);

        [Test]
        public void SteppingOutTakesGoingPastTheDoorway()
        {
            Assert.That(TerraceFraming.OnTerrace(new Vector2(0f, 13.7f), TerraceMin, TerraceMax, wasOn: false, Slack), Is.False);
            Assert.That(TerraceFraming.OnTerrace(new Vector2(0f, 14.1f), TerraceMin, TerraceMax, wasOn: false, Slack), Is.True);
        }

        [Test]
        public void ComingBackInTakesGoingPastTheDoorway()
        {
            Assert.That(TerraceFraming.OnTerrace(new Vector2(0f, 13.2f), TerraceMin, TerraceMax, wasOn: true, Slack), Is.True);
            Assert.That(TerraceFraming.OnTerrace(new Vector2(0f, 12.9f), TerraceMin, TerraceMax, wasOn: true, Slack), Is.False);
        }

        [Test]
        public void OnePlayerOutsideIsEnough()
        {
            var points = new[] { new Vector2(0f, -10f), new Vector2(5f, 16f) };
            Assert.That(TerraceFraming.AnyoneOnTerrace(points, TerraceMin, TerraceMax, false, Slack), Is.True);
            Assert.That(TerraceFraming.AnyoneOnTerrace(new[] { new Vector2(0f, -10f) }, TerraceMin, TerraceMax, false, Slack), Is.False);
        }

        [Test]
        public void TheShotHoldsStillWhilePeopleWalkTheTerrace()
        {
            var atOneEnd = Fit(new Vector2(-17f, 14f));
            var atTheOther = Fit(new Vector2(17f, 19.4f), new Vector2(3f, 16f));

            Assert.That(Vector3.Distance(atOneEnd, atTheOther), Is.LessThan(0.001f));
        }

        [Test]
        public void ItPullsBackFurtherThanFollowingAChefDoes()
        {
            var chef = new Vector2(0f, 16f);
            var following = CameraFraming.Fit(new[] { chef }, 55f, 45f, 16f / 9f, 3f, 10f, 55f, screenTop: 0.15f);
            var vista = Fit(chef);

            Assert.That(vista.Y, Is.GreaterThan(following.Y * 2f));
        }

        [Test]
        public void ItLooksPastTheRailOverTheRoofs()
        {
            var vista = Fit(new Vector2(0f, 16f));
            var tilt = 55f * System.MathF.PI / 180f;
            // Where the middle of the screen meets the floor: past the rail.
            var lookingAt = vista.Z + vista.Y / System.MathF.Tan(tilt);

            Assert.That(lookingAt, Is.GreaterThan(TerraceMax.Y));
        }

        [Test]
        public void SomeoneStillInsideIsKeptInTheShot()
        {
            var alone = Fit(new Vector2(0f, 16f));
            var withSomeoneAtTheElevator = Fit(new Vector2(0f, 16f), new Vector2(0f, -11f));

            Assert.That(withSomeoneAtTheElevator.Y, Is.GreaterThan(alone.Y));
        }
    }
}
