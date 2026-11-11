using Game.Simulation.Prep;
using Game.Simulation.Realism;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>The sweet spot steps along after each good cut (decision 2026-11-11, options B + C).</summary>
    public class SweetSpotStepTests
    {
        private const float Arc = 170f;
        private const double T0 = 10.0;

        private static ChopDial Dial(float spot, float step)
        {
            var dial = new ChopDial(0.8f, Arc, spot, goodHalfWidth: 12f, okHalfWidth: 28f, sweetSpotStep: step);
            dial.Start(T0);
            return dial;
        }

        // Cuts right in the middle of the sweet spot, wherever it is now.
        private static double CutGood(ChopDial dial, double now)
        {
            var at = now + dial.SecondsUntil(dial.SweetSpot, now);
            Assert.That(dial.Hit(at), Is.EqualTo(ChopGrade.Good));
            return at + 0.01;
        }

        [Test]
        public void AGoodCutStepsTheSpotTowardsTheRoomierEnd()
        {
            var dial = Dial(60f, 20f);
            CutGood(dial, T0);
            Assert.That(dial.SweetSpot, Is.EqualTo(80f).Within(0.01f), "more room towards the left end");

            var high = Dial(120f, 20f);
            CutGood(high, T0);
            Assert.That(high.SweetSpot, Is.EqualTo(100f).Within(0.01f));
        }

        [Test]
        public void AMissOrAnOffCutLeavesTheSpotWhereItIs()
        {
            var dial = Dial(85f, 20f);
            Assert.That(dial.Hit(T0), Is.EqualTo(ChopGrade.Miss));
            Assert.That(dial.SweetSpot, Is.EqualTo(85f));
            var early = T0 + 1.0 + dial.SecondsUntil(85f - 20f, T0 + 1.0);
            var grade = dial.Hit(early);
            Assert.That(grade, Is.Not.EqualTo(ChopGrade.Good));
            Assert.That(dial.SweetSpot, Is.EqualTo(85f), "only a good cut moves it, so a slip can be recovered");
        }

        [Test]
        public void ItTurnsBackAtTheEndAndKeepsItsWholeBandOnTheArc()
        {
            var dial = Dial(100f, 30f);
            var now = T0;
            for (var i = 0; i < 12; i++)
            {
                now = CutGood(dial, now);
                Assert.That(dial.SweetSpot, Is.InRange(28f, Arc - 28f), $"cut {i + 1}");
            }
        }

        [Test]
        public void NoStepKeepsTheOldFixedSpot()
        {
            var dial = Dial(90f, 0f);
            CutGood(dial, T0);
            Assert.That(dial.SweetSpot, Is.EqualTo(90f));
        }

        [Test]
        public void TheComplexityDialTurnsTheStepOffWhenRelaxedAndUpWhenHard()
        {
            Assert.That(MinigameComplexity.SweetSpotStepScale(0f), Is.Zero);
            Assert.That(MinigameComplexity.SweetSpotStepScale(0.25f), Is.Zero, "relaxed: pure cadence");
            Assert.That(MinigameComplexity.SweetSpotStepScale(0.5f), Is.EqualTo(1f), "standard: as tuned");
            Assert.That(MinigameComplexity.SweetSpotStepScale(1f), Is.EqualTo(1.5f));
        }

        [Test]
        public void TheCounterAppliesTheScaleToEachIngredientsStep()
        {
            var properties = new Game.Simulation.Cooking.IngredientProperties
            {
                CutIntoTypeId = "Diced", CutsNeeded = 3, SweetSpotPosition = 90f, SweetSpotStep = 20f,
            };
            var counter = new PrepCounter(whole => whole, stepScale: () => 0.5f);
            counter.Put(new Game.Simulation.Cooking.CookingSimulation(22f).AddIngredient(properties));
            Assert.That(counter.Dial.SweetSpotStep, Is.EqualTo(10f).Within(0.001f));
        }
    }
}
