using Game.Presentation;
using Game.Simulation.Cooking;
using Game.Simulation.Prep;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>How the timing gauges grade a press (decision 2026-11-11): ✓, – or ✕.</summary>
    public class TimingGaugeTests
    {
        private static readonly FlipGaugeSettings Flip = new FlipGaugeSettings { GoodFrom = 0.7f, GoodTo = 1.1f, WarnFrom = 1.5f };

        [Test]
        public void AFlipInTheGoldenWindowIsGood()
        {
            Assert.That(FlipCrustGaugeView.GradeAt(0.7f, Flip), Is.EqualTo(PressGrade.Good));
            Assert.That(FlipCrustGaugeView.GradeAt(0.9f, Flip), Is.EqualTo(PressGrade.Good));
            Assert.That(FlipCrustGaugeView.GradeAt(1.1f, Flip), Is.EqualTo(PressGrade.Good));
        }

        [Test]
        public void AnEarlyFlipIsPaleAndALateOneScorched()
        {
            Assert.That(FlipCrustGaugeView.GradeAt(0.3f, Flip), Is.EqualTo(PressGrade.Off), "pale");
            Assert.That(FlipCrustGaugeView.GradeAt(1.3f, Flip), Is.EqualTo(PressGrade.Bad), "scorched");
        }

        [Test]
        public void CutGradesMapToTheThreeMarks()
        {
            Assert.That(ChopGaugeView.ToPress(ChopGrade.Good), Is.EqualTo(PressGrade.Good));
            Assert.That(ChopGaugeView.ToPress(ChopGrade.Early), Is.EqualTo(PressGrade.Off));
            Assert.That(ChopGaugeView.ToPress(ChopGrade.Late), Is.EqualTo(PressGrade.Off));
            Assert.That(ChopGaugeView.ToPress(ChopGrade.Miss), Is.EqualTo(PressGrade.Bad));
        }
    }
}
