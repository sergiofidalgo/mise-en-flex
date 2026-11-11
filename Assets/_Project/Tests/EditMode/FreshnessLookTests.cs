using Game.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>Old food shows its age (decision 2026-11-11): stale below 35%, mouldy below 10%.</summary>
    public class FreshnessLookTests
    {
        private static readonly Color Tomato = new Color(0.9f, 0.18f, 0.12f);

        [TestCase(1f, FreshnessLook.Stage.Fresh)]
        [TestCase(0.36f, FreshnessLook.Stage.Fresh)]
        [TestCase(0.3f, FreshnessLook.Stage.Stale)]
        [TestCase(0.11f, FreshnessLook.Stage.Stale)]
        [TestCase(0.05f, FreshnessLook.Stage.Mouldy)]
        public void StagesFollowTheLines(float freshness, FreshnessLook.Stage stage) =>
            Assert.That(FreshnessLook.StageOf(freshness, 0.35f, 0.1f), Is.EqualTo(stage));

        [Test]
        public void FreshFoodLooksAsItIs() =>
            Assert.That(FreshnessLook.Tint(Tomato, 0.6f, 0.35f, 0.1f), Is.EqualTo(Tomato));

        [Test]
        public void StaleFoodDullsMoreTheOlderItGets()
        {
            var justStale = FreshnessLook.Tint(Tomato, 0.33f, 0.35f, 0.1f);
            var veryStale = FreshnessLook.Tint(Tomato, 0.12f, 0.35f, 0.1f);
            Assert.That(Distance(justStale, Tomato), Is.LessThan(Distance(veryStale, Tomato)));
            Assert.That(Colourfulness(veryStale), Is.LessThan(Colourfulness(Tomato) * 0.6f), "noticeably duller and darker");
            Assert.That(Colourfulness(justStale), Is.LessThan(Colourfulness(Tomato) * 0.85f), "a clear step as soon as it's stale");
        }

        [Test]
        public void MouldyFoodTurnsGreener()
        {
            var stale = FreshnessLook.Tint(Tomato, 0.1001f, 0.35f, 0.1f);
            var mouldy = FreshnessLook.Tint(Tomato, 0.05f, 0.35f, 0.1f);
            Assert.That(mouldy.g - mouldy.r, Is.GreaterThan(stale.g - stale.r));
        }

        private static float Distance(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        // Saturation times brightness: how vivid a colour reads.
        private static float Colourfulness(Color c)
        {
            Color.RGBToHSV(c, out _, out var s, out var v);
            return s * v;
        }
    }
}
