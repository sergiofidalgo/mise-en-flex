using System.Linq;
using System.Numerics;
using Game.Simulation.Movement;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class KitchenPlanTests
    {
        private static readonly Vector2[] Nobody = new Vector2[0];

        private static KitchenPlan Plan() => new KitchenPlan(
            new Vector2(-4.4f, -2.4f), new Vector2(4.4f, 3.4f),
            snapRange: 0.15f, stationHalfSize: 0.5f, walkerRadius: 0.3f);

        private static void AssertNear(Vector2 actual, Vector2 expected) =>
            Assert.That((actual - expected).Length(), Is.LessThan(0.001f), $"{actual} should be {expected}");

        [Test]
        public void WithNothingNearbyItGoesExactlyWhereItsPut()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero);
            AssertNear(plan.Snap(piece, new Vector2(1.23f, -0.71f), 0f), new Vector2(1.23f, -0.71f));
        }

        [Test]
        public void ItSnapsAgainstANeighbourAndInLineWithIt()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, new Vector2(-3f, -1f));
            plan.Add(new object(), new Vector2(1f, 2f));

            AssertNear(plan.Snap(piece, new Vector2(2.08f, 2.1f), 0f), new Vector2(2f, 2f));
        }

        [Test]
        public void JustBeyondTheRangeItDoesntSnap()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, new Vector2(-3f, -1f));
            plan.Add(new object(), new Vector2(1f, 2f));

            AssertNear(plan.Snap(piece, new Vector2(2.2f, 2.3f), 0f), new Vector2(2.2f, 2.3f));
        }

        [Test]
        public void ItCanGoBackExactlyWhereItWas()
        {
            var plan = Plan();
            var hob = new object();
            var home = new Vector2(1.03f, 2.97f);
            plan.Add(hob, home);
            AssertNear(plan.Snap(hob, home + new Vector2(0.1f, -0.05f), 0f, home), home);
        }

        [Test]
        public void ItSnapsToTheWalls()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero);
            AssertNear(plan.Snap(piece, new Vector2(4.3f, 0f), 0f), new Vector2(4.4f, 0f));
        }

        [Test]
        public void TurnedPiecesSnapToNeighboursTurnedTheSameWay()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, new Vector2(-3f, -1f));
            var neighbour = new Footprint(new Vector2(1f, 1f), 45f);
            plan.Add(new object(), neighbour.Centre, 45f);

            var beside = neighbour.Centre + neighbour.Right * 1.05f;
            AssertNear(plan.Snap(piece, beside, 45f), neighbour.Centre + neighbour.Right);
        }

        [Test]
        public void ANeighbourTurnedAnotherWayDoesntPullIt()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, new Vector2(-3f, -1f));
            plan.Add(new object(), new Vector2(1f, 1f), 45f);
            AssertNear(plan.Snap(piece, new Vector2(2.3f, 1.05f), 0f), new Vector2(2.3f, 1.05f));
        }

        [Test]
        public void APieceGivenAnotherRoomStaysInIt()
        {
            var plan = Plan();
            var table = new object();
            plan.Add(table, new Vector2(0f, -5f));
            plan.GiveArea(table, new Vector2(-4.4f, -7.4f), new Vector2(4.4f, -4.2f));

            Assert.That(plan.Check(table, new Vector2(0f, -6f), 0f, Nobody), Is.EqualTo(Placement.Ok));
            Assert.That(plan.Check(table, new Vector2(0f, 1f), 0f, Nobody), Is.EqualTo(Placement.OutsideTheKitchen), "that's the kitchen");
        }

        [Test]
        public void AnEmptySpotIsFine()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero);
            Assert.That(plan.Check(piece, new Vector2(2f, 2f), 0f, Nobody), Is.EqualTo(Placement.Ok));
        }

        [Test]
        public void ItCantLeaveTheKitchen()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero);
            Assert.That(plan.Check(piece, new Vector2(6f, 0f), 0f, Nobody), Is.EqualTo(Placement.OutsideTheKitchen));
            Assert.That(plan.Check(piece, new Vector2(0f, -3.5f), 0f, Nobody), Is.EqualTo(Placement.OutsideTheKitchen), "that's the dining side");
        }

        [Test]
        public void ItCantLandOnAnotherStation()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero);
            plan.Add(new object(), new Vector2(2f, 2f));
            Assert.That(plan.Check(piece, new Vector2(2.5f, 2f), 0f, Nobody), Is.EqualTo(Placement.OnAnotherStation));
            Assert.That(plan.Check(piece, new Vector2(3f, 2f), 0f, Nobody), Is.EqualTo(Placement.Ok), "right beside it is fine");
        }

        [Test]
        public void ItKeepsClearOfABodysOwnRadius()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero);
            // 0.35 m from the piece's edge: clear of a 0.3 m walker, but not of a 0.45 m chef.
            var chef = new Vector2(2.85f, 2f);
            Assert.That(plan.Check(piece, new Vector2(2f, 2f), 0f, new[] { chef }), Is.EqualTo(Placement.Ok));
            Assert.That(plan.Check(piece, new Vector2(2f, 2f), 0f, new[] { (chef, 0.45f) }), Is.EqualTo(Placement.SomeoneInTheWay));
        }

        [Test]
        public void ItCantLandOnSomeone()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero);
            Assert.That(plan.Check(piece, new Vector2(2f, 2f), 0f, new[] { new Vector2(2.2f, 2f) }), Is.EqualTo(Placement.SomeoneInTheWay));
        }

        [Test]
        public void ALayoutCanCloseThingsOffAndPlayersFindOutByPlaying()
        {
            // A wall of counters right across the room, with a chef and a station on either side.
            var plan = Plan();
            for (var x = -4f; x <= 4f; x += 1f)
            {
                if (x != 0f)
                    plan.Add(new object(), new Vector2(x, 0f));
            }
            plan.Add(new object(), new Vector2(2f, 2.4f));
            var last = new object();
            plan.Add(last, new Vector2(0f, 3f));

            var chefs = new[] { new Vector2(0f, -2f), new Vector2(0f, 2f) };
            Assert.That(plan.Check(last, new Vector2(0f, 0f), 0f, chefs), Is.EqualTo(Placement.Ok));
        }

        [Test]
        public void AHobCanBeBoxedIn()
        {
            var plan = Plan();
            var hob = new object();
            var other = new object();
            plan.Add(hob, new Vector2(1f, 3.4f));
            plan.Add(other, new Vector2(1f, 1f));
            Assert.That(plan.Check(other, new Vector2(1f, 2.4f), 0f, Nobody), Is.EqualTo(Placement.Ok));
        }

        [Test]
        public void SomeoneRoundSlipsPastACorner()
        {
            var counter = new Footprint(Vector2.Zero);
            Assert.That(counter.Contains(new Vector2(0.75f, 0f), 0.5f, 0.3f), Is.True, "beside its edge");
            Assert.That(counter.Contains(new Vector2(0.75f, 0.75f), 0.5f, 0.3f), Is.False, "past its corner");
        }

        [Test]
        public void ATurnedStationTakesUpItsOwnShape()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, new Vector2(-3f, 1f));
            plan.Add(new object(), new Vector2(0f, 0f));

            // Square on, 1.1 m away would touch nothing; turned 45 degrees its corner reaches further.
            Assert.That(plan.Check(piece, new Vector2(1.1f, 0f), 0f, Nobody), Is.EqualTo(Placement.Ok));
            Assert.That(plan.Check(piece, new Vector2(1.1f, 0f), 45f, Nobody), Is.EqualTo(Placement.OnAnotherStation));
        }

        [Test]
        public void ATurnedStationCantPokeThroughAWall()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero);
            Assert.That(plan.Check(piece, new Vector2(4.4f, 0f), 0f, Nobody), Is.EqualTo(Placement.Ok));
            Assert.That(plan.Check(piece, new Vector2(4.4f, 0f), 45f, Nobody), Is.EqualTo(Placement.OutsideTheKitchen));
        }

        [Test]
        public void PeopleCanStandInTheCornersATurnedStationLeaves()
        {
            var turned = new Footprint(Vector2.Zero, 45f);
            Assert.That(turned.Contains(new Vector2(0.6f, 0.6f), 0.5f), Is.False, "beyond its edge");
            Assert.That(turned.Contains(new Vector2(0.6f, 0f), 0.5f), Is.True, "its point sticks out this far");
            Assert.That(new Footprint(Vector2.Zero).Contains(new Vector2(0.6f, 0f), 0.5f), Is.False, "square on, that's clear");
        }

        [Test]
        public void TouchingIsntOverlapping()
        {
            var a = new Footprint(Vector2.Zero);
            Assert.That(a.Overlaps(new Footprint(new Vector2(1f, 0f)), 0.5f), Is.False);
            Assert.That(a.Overlaps(new Footprint(new Vector2(0.9f, 0f)), 0.5f), Is.True);
        }

        [Test]
        public void TurningIsRememberedWithThePiece()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero, 45f);
            Assert.That(plan.DegreesOf(piece), Is.EqualTo(45f));
            plan.Move(piece, Vector2.One, 90f);
            Assert.That(plan.DegreesOf(piece), Is.EqualTo(90f));
        }

        [Test]
        public void PieceAtFindsWhatsUnderThePoint()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, new Vector2(1f, 1f));
            Assert.That(plan.PieceAt(new Vector2(1.3f, 0.8f)), Is.SameAs(piece));
            Assert.That(plan.PieceAt(new Vector2(3f, 3f)), Is.Null);
        }

        [Test]
        public void ARemovedPieceLeavesItsSpotFree()
        {
            var plan = Plan();
            var sold = new object();
            var other = new object();
            plan.Add(sold, new Vector2(1f, 1f));
            plan.Add(other, new Vector2(3f, 3f));
            plan.Remove(sold);

            Assert.That(plan.PieceAt(new Vector2(1f, 1f)), Is.Null);
            Assert.That(plan.Check(other, new Vector2(1f, 1f), 0f, Nobody), Is.EqualTo(Placement.Ok));
        }

        // A narrow piece: a metre wide, half a metre deep.
        private static readonly Vector2 Narrow = new Vector2(0.5f, 0.25f);

        [Test]
        public void ANarrowPieceTakesUpLessRoom()
        {
            var plan = Plan();
            var narrow = new object();
            plan.Add(narrow, new Vector2(0f, 0f), 0f, Narrow);
            plan.Add(new object(), new Vector2(0f, 1f));

            Assert.That(plan.Check(narrow, new Vector2(0f, 0.2f), 0f, Nobody), Is.EqualTo(Placement.Ok), "a square would touch");
            Assert.That(plan.Check(narrow, new Vector2(0f, 0.3f), 0f, Nobody), Is.EqualTo(Placement.OnAnotherStation));
            Assert.That(plan.PieceAt(new Vector2(0f, -0.4f)), Is.Null, "behind it");
        }

        [Test]
        public void ANarrowPieceTurnsLikeARectangle()
        {
            var plan = Plan();
            var narrow = new object();
            plan.Add(narrow, new Vector2(0f, 0f), 0f, Narrow);
            plan.Add(new object(), new Vector2(0f, 1f));

            Assert.That(plan.Check(narrow, new Vector2(0f, 0.2f), 90f, Nobody), Is.EqualTo(Placement.OnAnotherStation), "now it's long that way");
        }

        [Test]
        public void ANarrowPieceSnapsEdgeToEdge()
        {
            var plan = Plan();
            var narrow = new object();
            plan.Add(narrow, new Vector2(-3f, -1f), 0f, Narrow);
            plan.Add(new object(), new Vector2(1f, 2f));

            AssertNear(plan.Snap(narrow, new Vector2(1.05f, 1.2f), 0f), new Vector2(1f, 1.25f));
        }

        [Test]
        public void ANarrowPieceSitsWithItsEdgeAgainstTheWall()
        {
            var plan = Plan();
            var narrow = new object();
            plan.Add(narrow, new Vector2(0f, 0f), 0f, Narrow);

            // A standard piece's centre stops at 3.4, half a metre from the wall.
            AssertNear(plan.Snap(narrow, new Vector2(0f, 3.6f), 0f), new Vector2(0f, 3.65f));
        }

        [Test]
        public void MovingTakesEffect()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero);
            plan.Move(piece, new Vector2(2f, 2f), 0f);
            Assert.That(plan.PositionOf(piece), Is.EqualTo(new Vector2(2f, 2f)));
            Assert.That(plan.Pieces.Count(), Is.EqualTo(1));
        }

        // ---- Appliances stay on the premises (decisions 2026-11-11) ----
        // In Plan(), the kitchen's wall faces are x = ±4.9, z = -2.9 and 3.9. In TwoRooms(), the
        // dining room's are x = ±4.9, z = -7.9 and -3.1: the dividing wall runs from -3.1 to -2.9.

        private static KitchenPlan TwoRooms()
        {
            var plan = Plan();
            plan.AddRoom(Room.DiningRoom, new Vector2(-4.4f, -7.4f), new Vector2(4.4f, -3.6f));
            return plan;
        }

        // Every corner of the piece where it would land is between one room's wall faces.
        private static void AssertWithinWalls(Vector2 at, float degrees, Vector2 halfSize, float north, float south)
        {
            foreach (var corner in new Footprint(at, degrees, halfSize).Corners(0.5f))
            {
                Assert.That(corner.X, Is.InRange(-4.9f - 1e-3f, 4.9f + 1e-3f), $"corner {corner} pokes through a side wall");
                Assert.That(corner.Y, Is.InRange(south - 1e-3f, north + 1e-3f), $"corner {corner} pokes through a wall");
            }
        }

        [Test]
        public void ACarriedPieceStopsAgainstTheWallInsteadOfGoingIntoIt()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero);

            var landing = plan.Snap(piece, new Vector2(5.6f, 1f), 0f);
            AssertNear(landing, new Vector2(4.4f, 1f));
            Assert.That(plan.Check(piece, landing, 0f, Nobody), Is.EqualTo(Placement.Ok));
        }

        [TestCase(45f)]
        [TestCase(135f)]
        [TestCase(225f)]
        [TestCase(315f)]
        public void ATurnedPieceIsKeptInsideTheWalls(float degrees)
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero);

            // Turned, its corner reaches 0.71 m from its centre, past where a square one stops.
            var landing = plan.Snap(piece, new Vector2(4.4f, 3.4f), degrees);
            AssertWithinWalls(landing, degrees, Vector2.Zero, 3.9f, -2.9f);
            AssertNear(landing, new Vector2(4.9f - 0.7071f, 3.9f - 0.7071f));
            Assert.That(plan.Check(piece, landing, degrees, Nobody), Is.EqualTo(Placement.Ok));
        }

        [TestCase(0f)]
        [TestCase(45f)]
        [TestCase(90f)]
        [TestCase(135f)]
        [TestCase(180f)]
        [TestCase(225f)]
        [TestCase(270f)]
        [TestCase(315f)]
        public void ALongPieceStaysInsideTheWallsAtAnyOfTheEightTurns(float degrees)
        {
            var plan = Plan();
            var piece = new object();
            var half = new Vector2(1f, 0.4f);
            plan.Add(piece, Vector2.Zero, 0f, half);

            foreach (var push in new[] { new Vector2(9f, 0f), new Vector2(-9f, 0f), new Vector2(0f, 9f), new Vector2(0f, -2.9f), new Vector2(9f, 9f) })
            {
                var landing = plan.Snap(piece, push, degrees);
                AssertWithinWalls(landing, degrees, half, 3.9f, -2.9f);
                Assert.That(plan.Check(piece, landing, degrees, Nobody), Is.EqualTo(Placement.Ok), $"pushed to {push}");
            }
        }

        [Test]
        public void WithTheDiningRoomOpenAnApplianceCanGoThere()
        {
            var plan = TwoRooms();
            var hob = new object();
            plan.Add(hob, Vector2.Zero);

            Assert.That(plan.Check(hob, new Vector2(0f, -5f), 0f, Nobody), Is.EqualTo(Placement.Ok));
            Assert.That(plan.RoomAt(new Vector2(0f, -5f)), Is.EqualTo(Room.DiningRoom));
            Assert.That(plan.RoomAt(new Vector2(0f, 1f)), Is.EqualTo(Room.Kitchen));
            Assert.That(plan.RoomAt(new Vector2(0f, -3f)), Is.Null, "inside the dividing wall");
        }

        [Test]
        public void NothingStraddlesTheDividingWall()
        {
            var plan = TwoRooms();
            var hob = new object();
            plan.Add(hob, Vector2.Zero);

            Assert.That(plan.Check(hob, new Vector2(0f, -3f), 0f, Nobody), Is.EqualTo(Placement.OutsideTheKitchen));
            Assert.That(plan.Check(hob, new Vector2(0f, -2.6f), 0f, Nobody), Is.EqualTo(Placement.OutsideTheKitchen));
        }

        [TestCase(0f)]
        [TestCase(45f)]
        public void ACarriedPieceJumpsTheDividingWallAtItsMiddle(float degrees)
        {
            var plan = TwoRooms();
            var hob = new object();
            plan.Add(hob, Vector2.Zero);

            var kitchenSide = plan.Snap(hob, new Vector2(0.3f, -2.95f), degrees);
            AssertWithinWalls(kitchenSide, degrees, Vector2.Zero, 3.9f, -2.9f);
            Assert.That(plan.Check(hob, kitchenSide, degrees, Nobody), Is.EqualTo(Placement.Ok));

            var diningSide = plan.Snap(hob, new Vector2(0.3f, -3.05f), degrees);
            AssertWithinWalls(diningSide, degrees, Vector2.Zero, -3.1f, -7.9f);
            Assert.That(plan.Check(hob, diningSide, degrees, Nobody), Is.EqualTo(Placement.Ok));
        }

        [Test]
        public void PiecesOutsideTheirUsualRoomAreListed()
        {
            var plan = TwoRooms();
            var hobInTheKitchen = new object();
            var hobInTheDiningRoom = new object();
            var tableInTheDiningRoom = new object();
            var tableInTheKitchen = new object();
            plan.Add(hobInTheKitchen, new Vector2(-2f, 1f));
            plan.Add(hobInTheDiningRoom, new Vector2(-2f, -5f));
            plan.Add(tableInTheDiningRoom, new Vector2(2f, -5f));
            plan.SetUsualRoom(tableInTheDiningRoom, Room.DiningRoom);
            plan.Add(tableInTheKitchen, new Vector2(2f, 1f));
            plan.SetUsualRoom(tableInTheKitchen, Room.DiningRoom);

            Assert.That(plan.OutOfTheirRooms(), Is.EquivalentTo(new[] { hobInTheDiningRoom, tableInTheKitchen }));
            Assert.That(plan.UsualRoomOf(tableInTheKitchen), Is.EqualTo(Room.DiningRoom));
            Assert.That(plan.UsualRoomOf(hobInTheKitchen), Is.EqualTo(Room.Kitchen));
            Assert.That(plan.RoomOf(tableInTheKitchen), Is.EqualTo(Room.Kitchen));
        }

        [Test]
        public void ACarriedPieceSaysWhetherItWouldBeOutOfItsRoom()
        {
            var plan = TwoRooms();
            var hob = new object();
            plan.Add(hob, Vector2.Zero);

            Assert.That(plan.IsOutOfItsRoom(hob, new Vector2(0f, 1f)), Is.False);
            Assert.That(plan.IsOutOfItsRoom(hob, new Vector2(0f, -5f)), Is.True);
            Assert.That(plan.IsOutOfItsRoom(hob, new Vector2(0f, -3f)), Is.False, "nowhere isn't another room");
        }

        [Test]
        public void APieceWithItsOwnAreaIsNeverOutOfItsRoom()
        {
            var plan = TwoRooms();
            var door = new object();
            plan.Add(door, new Vector2(0f, -7.5f));
            plan.GiveArea(door, new Vector2(-6f, -9f), new Vector2(6f, 5f));

            Assert.That(plan.OutOfTheirRooms(), Is.Empty);
            Assert.That(plan.IsOutOfItsRoom(door, new Vector2(0f, 1f)), Is.False);
        }

        [Test]
        public void ARemovedPieceIsNoLongerListed()
        {
            var plan = TwoRooms();
            var hob = new object();
            plan.Add(hob, new Vector2(0f, -5f));
            plan.Remove(hob);
            Assert.That(plan.OutOfTheirRooms(), Is.Empty);
        }
    }
}
