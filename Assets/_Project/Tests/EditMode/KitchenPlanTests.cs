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
            gridSize: 0.5f, stationHalfSize: 0.5f, walkerRadius: 0.3f, standDistance: 0.85f);

        [Test]
        public void PiecesSnapToTheGrid()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero);
            Assert.That(plan.Snap(piece, new Vector2(1.2f, -0.7f)), Is.EqualTo(new Vector2(1f, -0.5f)));
        }

        [Test]
        public void ThePassOnlySlidesAlongItsRow()
        {
            var plan = Plan();
            var pass = new object();
            plan.Add(pass, new Vector2(0f, -2.4f), rowLocked: true);
            Assert.That(plan.Snap(pass, new Vector2(1.2f, 1f)).Y, Is.EqualTo(-2.4f));
        }

        [Test]
        public void AnEmptySpotIsFine()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero);
            Assert.That(plan.Check(piece, new Vector2(2f, 2f), Nobody), Is.EqualTo(Placement.Ok));
        }

        [Test]
        public void ItCantLeaveTheKitchen()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero);
            Assert.That(plan.Check(piece, new Vector2(6f, 0f), Nobody), Is.EqualTo(Placement.OutsideTheKitchen));
            Assert.That(plan.Check(piece, new Vector2(0f, -3.5f), Nobody), Is.EqualTo(Placement.OutsideTheKitchen), "that's the dining side");
        }

        [Test]
        public void ItCantLandOnAnotherStation()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero);
            plan.Add(new object(), new Vector2(2f, 2f));
            Assert.That(plan.Check(piece, new Vector2(2.5f, 2f), Nobody), Is.EqualTo(Placement.OnAnotherStation));
            Assert.That(plan.Check(piece, new Vector2(3f, 2f), Nobody), Is.EqualTo(Placement.Ok), "right beside it is fine");
        }

        [Test]
        public void ItCantLandOnSomeone()
        {
            var plan = Plan();
            var piece = new object();
            plan.Add(piece, Vector2.Zero);
            Assert.That(plan.Check(piece, new Vector2(2f, 2f), new[] { new Vector2(2.2f, 2f) }), Is.EqualTo(Placement.SomeoneInTheWay));
        }

        // A wall of counters right across the room, with one piece left to close the gap.
        private static (KitchenPlan Plan, object Last) AlmostAWall()
        {
            var plan = Plan();
            for (var x = -4f; x <= 4f; x += 1f)
            {
                if (x != 0f)
                    plan.Add(new object(), new Vector2(x, 0f));
            }
            var last = new object();
            plan.Add(last, new Vector2(0f, 3f));
            return (plan, last);
        }

        [Test]
        public void ItCantCutAStationOff()
        {
            var (plan, last) = AlmostAWall();
            plan.Add(new object(), new Vector2(2f, -2.4f)); // on the far side, facing the wall

            Assert.That(plan.Check(last, new Vector2(0f, 0f), Nobody), Is.EqualTo(Placement.BlocksAStation));
        }

        [Test]
        public void ItCantWallSomeoneIn()
        {
            var (plan, last) = AlmostAWall();
            Assert.That(plan.Check(last, new Vector2(0f, 0f), Nobody), Is.EqualTo(Placement.Ok), "nobody's cut off");
            Assert.That(plan.Check(last, new Vector2(0f, 0f), new[] { new Vector2(0f, -2f) }), Is.EqualTo(Placement.BlocksAStation), "a chef would be");
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
            plan.Move(piece, new Vector2(2f, 2f));
            Assert.That(plan.PositionOf(piece), Is.EqualTo(new Vector2(2f, 2f)));
            Assert.That(plan.Pieces.Count(), Is.EqualTo(1));
        }
    }
}
