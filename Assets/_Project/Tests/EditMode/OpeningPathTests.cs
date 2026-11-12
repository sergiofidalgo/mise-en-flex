using Game.Presentation.Office;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class OpeningPathTests
    {
        private static readonly CameraPose[] Poses =
        {
            new CameraPose(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 10f)),
            new CameraPose(new Vector3(10f, 5f, 0f), new Vector3(10f, 5f, 10f)),
            new CameraPose(new Vector3(20f, 0f, 5f), new Vector3(0f, 0f, 0f)),
        };

        [Test]
        public void PassesThroughEveryPose()
        {
            Assert.That(Vector3.Distance(OpeningPath.Sample(Poses, 0f).Position, Poses[0].Position), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(OpeningPath.Sample(Poses, 0.5f).Position, Poses[1].Position), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(OpeningPath.Sample(Poses, 1f).Position, Poses[2].Position), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(OpeningPath.Sample(Poses, 1f).Target, Poses[2].Target), Is.LessThan(1e-4f));
        }

        [Test]
        public void MovesWithoutJumps()
        {
            var last = OpeningPath.Sample(Poses, 0f).Position;
            for (var t = 0.01f; t <= 1f; t += 0.01f)
            {
                var now = OpeningPath.Sample(Poses, t).Position;
                Assert.That(Vector3.Distance(now, last), Is.LessThan(1.5f));
                last = now;
            }
        }

        [Test]
        public void EvenSamplingKeepsOnePaceThroughLongAndShortStretches()
        {
            var uneven = new[]
            {
                new CameraPose(new Vector3(0f, 0f, 0f), Vector3.forward),
                new CameraPose(new Vector3(0f, 0f, 90f), Vector3.forward),
                new CameraPose(new Vector3(0f, 0f, 100f), Vector3.forward),
            };
            // 90 m then 10 m: nine tenths of the way along is the middle pose.
            Assert.That(Vector3.Distance(OpeningPath.SampleEven(uneven, 0.9f).Position, uneven[1].Position), Is.LessThan(1e-3f));
            float slowest = float.MaxValue, fastest = 0f;
            var last = OpeningPath.SampleEven(uneven, 0f).Position;
            for (var t = 0.02f; t <= 1.0001f; t += 0.02f)
            {
                var now = OpeningPath.SampleEven(uneven, t).Position;
                var step = Vector3.Distance(now, last);
                slowest = Mathf.Min(slowest, step);
                fastest = Mathf.Max(fastest, step);
                last = now;
            }
            Assert.That(fastest / slowest, Is.LessThan(3f), "no sudden dash or crawl");
        }

        [Test]
        public void WalksAlongThePointsAndFacesTheWayItGoes()
        {
            var walk = new[] { new Vector3(0f, 0f, 0f), new Vector3(4f, 0f, 0f), new Vector3(4f, 0f, 3f) };
            Assert.That(OpeningPath.Length(walk), Is.EqualTo(7f).Within(1e-4f));
            var at = OpeningPath.PointAlong(walk, 2f, out var heading);
            Assert.That(Vector3.Distance(at, new Vector3(2f, 0f, 0f)), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(heading, Vector3.right), Is.LessThan(1e-4f));
            at = OpeningPath.PointAlong(walk, 5f, out heading);
            Assert.That(Vector3.Distance(at, new Vector3(4f, 0f, 1f)), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(heading, Vector3.forward), Is.LessThan(1e-4f), "turned the corner");
            Assert.That(Vector3.Distance(OpeningPath.PointAlong(walk, 50f, out _), walk[2]), Is.LessThan(1e-4f), "stops at the end");
        }

        [Test]
        public void EasesInAndOut()
        {
            Assert.That(OpeningPath.Ease(0f), Is.EqualTo(0f));
            Assert.That(OpeningPath.Ease(1f), Is.EqualTo(1f));
            Assert.That(OpeningPath.Ease(0.5f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(OpeningPath.Ease(0.1f), Is.LessThan(0.1f), "slow to start");
            Assert.That(OpeningPath.Ease(0.9f), Is.GreaterThan(0.9f), "slow to stop");
        }

        [Test]
        public void HandlesOneOrNoPoses()
        {
            Assert.That(OpeningPath.Sample(new[] { Poses[1] }, 0.7f).Position, Is.EqualTo(Poses[1].Position));
            Assert.That(OpeningPath.Sample(System.Array.Empty<CameraPose>(), 0.5f).Position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void TheLiftRidesLockedToTheCameraWithNoLag()
        {
            // Mid-climb, however fast the camera goes, the cab is exactly its lead away.
            Assert.That(OpeningPath.LiftHeight(-60f, -1.1f, -119.85f, 0f, 4f), Is.EqualTo(-61.1f).Within(1e-4f));
            Assert.That(OpeningPath.LiftHeight(-20f, -1.1f, -119.85f, 0f, 4f), Is.EqualTo(-21.1f).Within(1e-4f));
        }

        [Test]
        public void TheLiftWaitsAtTheStreetAndStopsAtTheTop()
        {
            Assert.That(OpeningPath.LiftHeight(-119.5f, -1.1f, -119.85f, 0f, 4f), Is.EqualTo(-119.85f), "never under the street");
            Assert.That(OpeningPath.LiftHeight(40f, -1.1f, -119.85f, 0f, 4f), Is.EqualTo(0f), "never over its top stop");
        }

        [Test]
        public void TheLiftEasesIntoItsTopStop()
        {
            // Rising steadily past the top, the cab never goes down, never jumps, and slows to nothing.
            var last = OpeningPath.LiftHeight(-12f, -1.1f, -119.85f, 0f, 4f);
            var lastStep = float.MaxValue;
            for (var camera = -11.9f; camera <= 8f; camera += 0.1f)
            {
                var now = OpeningPath.LiftHeight(camera, -1.1f, -119.85f, 0f, 4f);
                var step = now - last;
                Assert.That(step, Is.GreaterThanOrEqualTo(-1e-5f), $"went down at {camera}");
                Assert.That(step, Is.LessThanOrEqualTo(lastStep + 1e-4f), $"sped up at {camera}");
                Assert.That(step, Is.LessThanOrEqualTo(0.1f + 1e-4f), $"outran the camera at {camera}");
                last = now;
                lastStep = step;
            }
            Assert.That(last, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(lastStep, Is.EqualTo(0f).Within(1e-4f), "at rest by the end");
        }

        [Test]
        public void TheClimbEndsOnTheLandingLookingTheFloorsWay()
        {
            var landing = Quaternion.Euler(55f, 0f, 0f);
            var start = OpeningPath.Climb(Poses, landing, 0.86f, 0f, out var first);
            Assert.That(Vector3.Distance(start, Poses[0].Position), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(first, Poses[0].Rotation), Is.LessThan(0.01f), "the curve's own look until the turn");
            var end = OpeningPath.Climb(Poses, landing, 0.86f, 1f, out var last);
            Assert.That(Vector3.Distance(end, Poses[Poses.Length - 1].Position), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(last, landing), Is.LessThan(0.01f));
        }

        [Test]
        public void TheClimbsPaceStartsGentlyNeverGoesBackAndStopsLate()
        {
            Assert.That(OpeningPath.ClimbEase(0f), Is.EqualTo(0f));
            Assert.That(OpeningPath.ClimbEase(0.5f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(OpeningPath.ClimbEase(1f), Is.EqualTo(1f).Within(1e-5f));
            var last = 0f;
            var lastStep = 0f;
            for (var t = 0.01f; t <= 1f; t += 0.01f)
            {
                var now = OpeningPath.ClimbEase(t);
                Assert.That(now, Is.GreaterThanOrEqualTo(last), $"went back at {t}");
                // No jolt where the two halves meet.
                if (t > 0.02f)
                    Assert.That(Mathf.Abs(now - last - lastStep), Is.LessThan(0.002f), $"jolted at {t}");
                lastStep = now - last;
                last = now;
            }
            // Later than the smoothstep: further behind it near the end, so it arrives with more pace.
            Assert.That(OpeningPath.ClimbEase(0.9f), Is.LessThan(OpeningPath.Ease(0.9f)));
        }

        [Test]
        public void TheLiftReachesTheTopExactlyAsTheClimbEnds()
        {
            // A camera that rises steadily from the street to 40 m over the floor through the climb.
            float Camera(float progress) => Mathf.Lerp(-118f, 40f, progress);
            float Lift(float progress) => OpeningPath.LiftClimb(Camera(progress), progress, -1.1f, -119.85f, 0f, 4f, 14f, 0.5f);
            Assert.That(Lift(0.3f), Is.EqualTo(Camera(0.3f) - 1.1f).Within(1e-3f), "beside the camera on the way up");
            Assert.That(Lift(0.9f), Is.LessThan(-0.1f), "not there before the end");
            Assert.That(Lift(1f), Is.EqualTo(0f).Within(1e-4f), "there at the end");
            var last = Lift(0f);
            for (var progress = 0.005f; progress <= 1f; progress += 0.005f)
            {
                var now = Lift(progress);
                Assert.That(now, Is.GreaterThanOrEqualTo(last - 1e-4f), $"went down at {progress}");
                Assert.That(now, Is.LessThanOrEqualTo(1e-4f), $"overshot at {progress}");
                last = now;
            }
        }
    }
}
