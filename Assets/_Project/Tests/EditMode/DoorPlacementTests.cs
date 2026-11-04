using System.Numerics;
using Game.Simulation.Movement;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class DoorPlacementTests
    {
        private static readonly Vector2 Min = new Vector2(-4.55f, -7.55f);
        private static readonly Vector2 Max = new Vector2(4.55f, -3.6f);

        private static (Vector2 Position, float Degrees, bool OnAWall) Fit(float x, float y) =>
            DoorPlacement.Fit(new Vector2(x, y), Min, Max, 1f, 0.7f);

        [Test]
        public void NearTheSouthWallItSnapsOnFacingNorth()
        {
            var door = Fit(1f, -7f);
            Assert.That(door.OnAWall, Is.True);
            Assert.That(door.Position, Is.EqualTo(new Vector2(1f, -7.55f)));
            Assert.That(door.Degrees, Is.EqualTo(0f));
        }

        [Test]
        public void NearTheWestWallItFacesEast()
        {
            var door = Fit(-4f, -5f);
            Assert.That(door.OnAWall, Is.True);
            Assert.That(door.Position, Is.EqualTo(new Vector2(-4.55f, -5f)));
            Assert.That(door.Degrees, Is.EqualTo(90f));
        }

        [Test]
        public void NearTheEastWallItFacesWest()
        {
            var door = Fit(4f, -5f);
            Assert.That(door.OnAWall, Is.True);
            Assert.That(door.Position, Is.EqualTo(new Vector2(4.55f, -5f)));
            Assert.That(door.Degrees, Is.EqualTo(270f));
        }

        [Test]
        public void InTheMiddleOfTheRoomItsNotOnAWall() => Assert.That(Fit(0f, -5f).OnAWall, Is.False);

        [Test]
        public void TheKitchenWallsDontCount()
        {
            Assert.That(Fit(0f, 3f).OnAWall, Is.False, "the middle of the kitchen");
            Assert.That(Fit(-4.5f, 1f).OnAWall, Is.False, "the kitchen's west wall");
            Assert.That(Fit(4.5f, 1f).OnAWall, Is.False, "the kitchen's east wall");
        }

        [Test]
        public void ItStaysClearOfTheCorners()
        {
            var door = Fit(3f, -7.5f);
            Assert.That(Fit(4.4f, -7.5f).Position.Y, Is.EqualTo(-7.55f).Or.EqualTo(-6.85f), "on one wall or the other");
            Assert.That(door.Position.X, Is.EqualTo(3f));
            Assert.That(Fit(4f, -7.55f).Position.X, Is.EqualTo(3.85f).Within(0.001f), "the width of the door");
        }
    }
}
