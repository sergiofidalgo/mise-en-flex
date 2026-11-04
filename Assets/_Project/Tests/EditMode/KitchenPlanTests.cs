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
            new Vector2(4.6f, 3.6f), new Vector2(-4.4f, -2.4f), new Vector2(4.4f, 3.4f),
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
        public void ThePassOnlySlidesAlongItsRowAndDoesntTurn()
        {
            var plan = Plan();
            var pass = new object();
            plan.Add(pass, new Vector2(0f, -2.4f), rowLocked: true);
            Assert.That(plan.Snap(pass, new Vector2(1.2f, 1f), 0f).Y, Is.EqualTo(-2.4f));
            Assert.That(plan.CanRotate(pass), Is.False);
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
        public void MovingTakesEffect()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero);
            plan.Move(piece, new Vector2(2f, 2f), 0f);
            Assert.That(plan.PositionOf(piece), Is.EqualTo(new Vector2(2f, 2f)));
            Assert.That(plan.Pieces.Count(), Is.EqualTo(1));
        }
    }
}
