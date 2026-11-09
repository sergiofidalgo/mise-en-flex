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
        public void ClockwiseGoesTheOtherWay()
        {
            var a = AmbientMotion.Loop(Vector3.zero, Vector2.one, 0f, 1f, 8f, 0f, false);
            var b = AmbientMotion.Loop(Vector3.zero, Vector2.one, 0f, 1f, 8f, 0f, true);
            Assert.That(a.z, Is.GreaterThan(0f));
            Assert.That(b.z, Is.LessThan(0f));
        }
    }
}
