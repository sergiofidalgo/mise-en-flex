using Game.Simulation.Movement;
using NUnit.Framework;
using Vector2 = System.Numerics.Vector2;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The bin sits snug against whatever is beside its slot: walls or other stations (the user's
    /// note, 2026-11-08).
    /// </summary>
    public class SnugFitTests
    {
        private const float Slack = 0.2f;

        // The greybox building: outer walls a walker's radius beyond the room, and the low wall
        // between kitchen and dining room from z -3.1 to -2.9.
        private static FloorPlan Plan() => new FloorPlan(
            new Vector2(-3.6f, -6.6f), new Vector2(3.6f, 1.6f),
            new[] { (new Vector2(-2.3f, -3.1f), new Vector2(4f, -2.9f)) },
            stationHalfSize: 0.5f, walkerRadius: 0.3f, standDistance: 0.85f);

        private static Vector2 Offset(float x, float z, params Footprint[] neighbours) =>
            SnugFit.Offset(Plan(), new Vector2(x, z), 0.5f, Slack, neighbours);

        [Test]
        public void InTheOpenItStaysCentred() => Assert.That(Offset(0f, -1f), Is.EqualTo(Vector2.Zero));

        [Test]
        public void AgainstOneWallItGoesToThatWall() => Assert.That(Offset(3.4f, -1f), Is.EqualTo(new Vector2(Slack, 0f)));

        [Test]
        public void InACornerItGoesIntoTheCorner() => Assert.That(Offset(3.4f, 1.4f), Is.EqualTo(new Vector2(Slack, Slack)));

        [Test]
        public void AgainstTheWallToTheDiningRoomItGoesToThatWall() =>
            Assert.That(Offset(0f, -2.4f), Is.EqualTo(new Vector2(0f, -Slack)));

        [Test]
        public void BesideAStationItGoesToTheStation() =>
            Assert.That(Offset(0f, -1f, new Footprint(new Vector2(-1f, -1f))), Is.EqualTo(new Vector2(-Slack, 0f)));

        [Test]
        public void BetweenTwoStationsItStaysCentredBetweenThem() =>
            Assert.That(Offset(0f, -1f, new Footprint(new Vector2(-1f, -1f)), new Footprint(new Vector2(1f, -1f))), Is.EqualTo(Vector2.Zero));

        [Test]
        public void AStationAGapAwayDoesNotCount() =>
            Assert.That(Offset(0f, -1f, new Footprint(new Vector2(-1.5f, -1f))), Is.EqualTo(Vector2.Zero));
    }
}
