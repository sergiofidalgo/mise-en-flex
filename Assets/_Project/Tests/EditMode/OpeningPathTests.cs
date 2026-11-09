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
    }
}
