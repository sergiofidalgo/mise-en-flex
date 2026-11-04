using System;
using System.Numerics;
using Game.Simulation.Movement;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class SteeringTests
    {
        private const float Step = 2f / 60f; // walking pace at 60 fps
        private const float Closest = Steering.BodyDistance - Step;

        private static bool OpenFloor(Vector2 _) => true;

        [Test]
        public void WithNobodyAroundTheyWalkStraightThere()
        {
            var next = Steering.Step(Vector2.Zero, new Vector2(1f, 0f), Step, Array.Empty<Vector2>(), OpenFloor);
            Assert.That(next.X, Is.EqualTo(Step).Within(0.0001f));
            Assert.That(next.Y, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void TheyDontOvershoot()
        {
            var target = new Vector2(0.01f, 0f);
            Assert.That((Steering.Step(Vector2.Zero, target, Step, Array.Empty<Vector2>(), OpenFloor) - target).Length(), Is.LessThan(0.0001f));
        }

        [Test]
        public void HeadOnTheyPassEachOther()
        {
            var a = new Vector2(-3f, 0f);
            var b = new Vector2(3f, 0f);
            var goalA = new Vector2(3f, 0f);
            var goalB = new Vector2(-3f, 0f);

            for (var i = 0; i < 1200; i++)
            {
                a = Steering.Step(a, goalA, Step, new[] { b }, OpenFloor);
                b = Steering.Step(b, goalB, Step, new[] { a }, OpenFloor);
                Assert.That((a - b).Length(), Is.GreaterThan(Closest), $"they overlapped on frame {i}");
            }

            Assert.That((a - goalA).Length(), Is.LessThan(0.1f));
            Assert.That((b - goalB).Length(), Is.LessThan(0.1f));
        }

        [Test]
        public void TheyWalkAroundSomeoneStandingStill()
        {
            var walker = new Vector2(-3f, 0f);
            var goal = new Vector2(3f, 0f);
            var standing = new[] { Vector2.Zero };

            for (var i = 0; i < 600; i++)
            {
                walker = Steering.Step(walker, goal, Step, standing, OpenFloor);
                Assert.That(walker.Length(), Is.GreaterThan(Closest));
            }
            Assert.That((walker - goal).Length(), Is.LessThan(0.1f));
        }

        [Test]
        public void TheyNeverStepOntoFurniture()
        {
            bool Floor(Vector2 p) => p.Y > -0.05f;
            var walker = new Vector2(-3f, 0f);
            var standing = new[] { new Vector2(0f, 0f) };

            for (var i = 0; i < 600; i++)
            {
                walker = Steering.Step(walker, new Vector2(3f, 0f), Step, standing, Floor);
                Assert.That(Floor(walker));
            }
        }

        [Test]
        public void WithNoWayThroughTheyWait()
        {
            var walker = new Vector2(-0.61f, 0f);
            var next = Steering.Step(walker, new Vector2(3f, 0f), Step, new[] { Vector2.Zero }, _ => false);
            Assert.That(next, Is.EqualTo(walker));
        }

        // The pushing rule.
        private const float Body = 0.7f;

        private static (Vector2 A, Vector2 B) Walk(Vector2 a, Vector2 moveA, Vector2 b, Vector2 moveB, int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                a += moveA;
                b += moveB;
                var (shiftA, shiftB) = PushSolver.Separate(a, moveA, b, moveB, Body);
                a += shiftA;
                b += shiftB;
            }
            return (a, b);
        }

        [Test]
        public void FarApartNobodyMoves()
        {
            var (shiftA, shiftB) = PushSolver.Separate(Vector2.Zero, new Vector2(0.03f, 0f), new Vector2(2f, 0f), Vector2.Zero, Body);
            Assert.That(shiftA, Is.EqualTo(Vector2.Zero));
            Assert.That(shiftB, Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void WalkingIntoSomeoneIdleMovesThemAlong()
        {
            var (a, b) = Walk(Vector2.Zero, new Vector2(0.03f, 0f), new Vector2(0.7f, 0f), Vector2.Zero, 60);

            Assert.That(a.X, Is.EqualTo(1.8f).Within(0.01f), "the pusher keeps walking");
            Assert.That(b.X, Is.EqualTo(2.5f).Within(0.01f), "the idle one goes along");
            Assert.That((b - a).Length(), Is.EqualTo(Body).Within(0.001f));
        }

        [Test]
        public void EqualPushesAgainstEachOtherGoNowhere()
        {
            var (a, b) = Walk(Vector2.Zero, new Vector2(0.03f, 0f), new Vector2(0.7f, 0f), new Vector2(-0.03f, 0f), 120);

            Assert.That(a.X, Is.EqualTo(0f).Within(0.01f));
            Assert.That(b.X, Is.EqualTo(0.7f).Within(0.01f));
        }

        [Test]
        public void TheStrongerPushWins()
        {
            var (a, b) = Walk(Vector2.Zero, new Vector2(0.03f, 0f), new Vector2(0.7f, 0f), new Vector2(-0.01f, 0f), 60);

            Assert.That(a.X, Is.GreaterThan(0.1f));
            Assert.That(b.X, Is.GreaterThan(0.8f));
        }

        [Test]
        public void WalkingAwayIsntPushing()
        {
            var (shiftA, _) = PushSolver.Separate(Vector2.Zero, new Vector2(-0.03f, 0f), new Vector2(0.6f, 0f), Vector2.Zero, Body);
            Assert.That(shiftA.X, Is.LessThan(0f), "split evenly: no one's pushing");
        }

        [Test]
        public void ExactlyOnTopOfEachOtherTheySeparate()
        {
            var (shiftA, shiftB) = PushSolver.Separate(Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, Body);
            Assert.That((shiftB - shiftA).Length(), Is.EqualTo(Body).Within(0.001f));
        }
    }
}
