using Game.Presentation.Office.Ambience;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class AmbientMotionTests
    {
        [Test]
        public void WaveSwingsBothWaysOncePerPeriod()
        {
            Assert.That(AmbientMotion.Wave(0f, 4f, 0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(AmbientMotion.Wave(1f, 4f, 0f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(AmbientMotion.Wave(3f, 4f, 0f), Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(AmbientMotion.Wave(4f, 4f, 0f), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void PhaseShiftsWhereInTheCycleItIs()
        {
            Assert.That(AmbientMotion.Wave(0f, 4f, 0.25f), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void PulseBumpsDuringItsDutyAndRestsAfter()
        {
            Assert.That(AmbientMotion.Pulse(0f, 10f, 0.2f, 0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(AmbientMotion.Pulse(1f, 10f, 0.2f, 0f), Is.EqualTo(1f).Within(1e-5f), "peaks halfway through its duty");
            Assert.That(AmbientMotion.Pulse(5f, 10f, 0.2f, 0f), Is.EqualTo(0f), "rests for the rest of the period");
            Assert.That(AmbientMotion.Pulse(11f, 10f, 0.2f, 0f), Is.EqualTo(1f).Within(1e-4f), "and repeats");
        }

        [Test]
        public void BlinkIsOnForItsFractionOfEachPeriod()
        {
            Assert.That(AmbientMotion.BlinkOn(0.1f, 1f, 0.2f, 0f), Is.True);
            Assert.That(AmbientMotion.BlinkOn(0.5f, 1f, 0.2f, 0f), Is.False);
            Assert.That(AmbientMotion.BlinkOn(1.1f, 1f, 0.2f, 0f), Is.True);
        }

        [Test]
        public void LoopKeepsToItsRadiiAndComesBack()
        {
            var centre = new Vector3(5f, 10f, -3f);
            var radii = new Vector2(8f, 4f);
            for (var t = 0f; t < 20f; t += 0.37f)
            {
                var p = AmbientMotion.Loop(centre, radii, 1f, t, 20f, 0f, false) - centre;
                var onEllipse = (p.x * p.x) / (radii.x * radii.x) + (p.z * p.z) / (radii.y * radii.y);
                Assert.That(onEllipse, Is.EqualTo(1f).Within(1e-3f));
                Assert.That(Mathf.Abs(p.y), Is.LessThanOrEqualTo(1f + 1e-4f));
            }
            var start = AmbientMotion.Loop(centre, radii, 1f, 0f, 20f, 0f, false);
            var lap = AmbientMotion.Loop(centre, radii, 1f, 20f, 20f, 0f, false);
            Assert.That(Vector3.Distance(start, lap), Is.LessThan(1e-3f));
        }

        [Test]
        public void FireIsRestlessButNeverOut()
        {
            var min = float.MaxValue;
            var max = float.MinValue;
            for (var t = 0f; t < 30f; t += 0.05f)
            {
                var f = AmbientMotion.Fire(t, 3f);
                min = Mathf.Min(min, f);
                max = Mathf.Max(max, f);
            }
            Assert.That(min, Is.GreaterThanOrEqualTo(0.65f));
            Assert.That(max, Is.LessThanOrEqualTo(1.15f));
            Assert.That(max - min, Is.GreaterThan(0.1f), "it moves");
        }

        [Test]
        public void TelevisionHoldsEachShotThenJumps()
        {
            Assert.That(AmbientMotion.Television(1.01f, 0.2f, 1f), Is.EqualTo(AmbientMotion.Television(1.19f, 0.2f, 1f)));
            for (var t = 0f; t < 10f; t += 0.2f)
            {
                var b = AmbientMotion.Television(t, 0.2f, 1f);
                Assert.That(b, Is.InRange(0.35f, 1f));
            }
        }

        [Test]
        public void FaultyTubeIsMostlyOnButStutters()
        {
            int on = 0, samples = 0;
            for (var t = 0f; t < 120f; t += 0.01f, samples++)
                if (AmbientMotion.FaultyOn(t, 6f, 2f))
                    on++;
            Assert.That(on / (float)samples, Is.GreaterThan(0.85f), "mostly on");
            Assert.That(on, Is.LessThan(samples), "but it does go off");
        }

        [Test]
        public void DrivingRunsTheLaneThenStartsAgain()
        {
            Assert.That(AmbientMotion.Lane(0f, 20f, 10f, false, 0f, 0f).Along, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(AmbientMotion.Lane(1f, 20f, 10f, false, 0f, 0f).Along, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(AmbientMotion.Lane(2.5f, 20f, 10f, false, 0f, 0f).Along, Is.EqualTo(5f).Within(1e-3f), "came round again");
        }

        [Test]
        public void WalkingGoesThereWaitsAndComesBack()
        {
            // 10 m at 2 m/s: 5 s each way, 1 s pause at each end.
            var (along, forward) = AmbientMotion.Lane(2.5f, 10f, 2f, true, 1f, 0f);
            Assert.That(along, Is.EqualTo(5f).Within(1e-3f));
            Assert.That(forward, Is.True);
            Assert.That(AmbientMotion.Lane(5.5f, 10f, 2f, true, 1f, 0f).Along, Is.EqualTo(10f), "waiting at the far end");
            (along, forward) = AmbientMotion.Lane(8.5f, 10f, 2f, true, 1f, 0f);
            Assert.That(along, Is.EqualTo(5f).Within(1e-3f));
            Assert.That(forward, Is.False, "on the way back");
            for (var t = 0f; t < 30f; t += 0.13f)
                Assert.That(AmbientMotion.Lane(t, 10f, 2f, true, 1f, 0.3f).Along, Is.InRange(0f, 10f));
        }

        [Test]
        public void ClockwiseGoesTheOtherWay()
        {
            var a = AmbientMotion.Loop(Vector3.zero, Vector2.one, 0f, 1f, 8f, 0f, false);
            var b = AmbientMotion.Loop(Vector3.zero, Vector2.one, 0f, 1f, 8f, 0f, true);
            Assert.That(a.z, Is.GreaterThan(0f));
            Assert.That(b.z, Is.LessThan(0f));
        }
    
        [Test]
        public void FlashPopsOncePerPeriodAndIsDarkBetween()
        {
            var pops = 0;
            var lit = 0;
            var was = false;
            for (var t = 0f; t < 6f; t += 0.002f)
            {
                var b = AmbientMotion.Flash(t, 6f, 2f);
                Assert.That(b, Is.InRange(0f, 1f));
                var on = b > 0.3f;
                if (on && !was) pops++;
                was = on;
                if (b > 0.05f) lit++;
            }
            Assert.That(pops, Is.InRange(1, 2), "one pop, or a pre-flash and the photo");
            Assert.That(lit * 0.002f, Is.LessThan(0.6f), "dark for most of the period");
        }

        [Test]
        public void FlashGoesOffAtDifferentMomentsEachPeriod()
        {
            float FirstPop(float from)
            {
                for (var t = from; t < from + 6f; t += 0.002f)
                    if (AmbientMotion.Flash(t, 6f, 2f) > 0.3f)
                        return t - from;
                return -1f;
            }
            Assert.That(FirstPop(0f), Is.GreaterThanOrEqualTo(0f));
            Assert.That(FirstPop(6f), Is.Not.EqualTo(FirstPop(0f)).Within(0.05f));
        }
    
        [Test]
        public void PopRisesOncePerPeriodForItsDuration()
        {
            var pops = 0;
            var showing = 0f;
            var was = false;
            for (var t = 0f; t < 5f; t += 0.01f)
            {
                var p = AmbientMotion.Pop(t, 5f, 1.5f, 3f);
                var on = p >= 0f;
                if (on)
                {
                    Assert.That(p, Is.InRange(0f, 1f));
                    showing += 0.01f;
                }
                if (on && !was) pops++;
                was = on;
            }
            Assert.That(pops, Is.EqualTo(1));
            Assert.That(showing, Is.EqualTo(1.5f).Within(0.05f));
        }

        [Test]
        public void PopKindStaysInRange()
        {
            for (var t = 0f; t < 100f; t += 1.3f)
                Assert.That(AmbientMotion.PopKind(t, 4f, 1f, 3), Is.InRange(0, 2));
        }
    }
}
