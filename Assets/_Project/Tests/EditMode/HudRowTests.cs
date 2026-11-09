using System.Collections.Generic;
using Game.Presentation.Hud;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class HudRowTests
    {
        private const float Gap = 12f;
        private const float Min = 24f;
        private const float Max = 1896f;

        private static void AssertNoOverlap(IList<float> lefts, IReadOnlyList<float> widths)
        {
            for (var a = 0; a < lefts.Count; a++)
            for (var b = 0; b < lefts.Count; b++)
            {
                if (a == b || lefts[a] > lefts[b])
                    continue;
                Assert.That(lefts[a] + widths[a] + Gap, Is.LessThanOrEqualTo(lefts[b] + 0.01f), $"cards {a} and {b} overlap");
            }
        }

        [Test]
        public void CardsApartStayWhereTheyAre()
        {
            var lefts = new List<float> { 100f, 700f };
            HudRow.Spread(lefts, new[] { 440f, 440f }, Gap, Min, Max);
            Assert.That(lefts, Is.EqualTo(new[] { 100f, 700f }));
        }

        [Test]
        public void TwoOverlappingCardsMoveApartEvenly()
        {
            var lefts = new List<float> { 500f, 600f };
            HudRow.Spread(lefts, new[] { 440f, 440f }, Gap, Min, Max);
            AssertNoOverlap(lefts, new[] { 440f, 440f });
            // Centred on where they wanted to be on average: each moved the same distance.
            Assert.That(500f - lefts[0], Is.EqualTo(lefts[1] - 600f).Within(0.01f));
        }

        [Test]
        public void FourCardsOverNeighbouringLaddersAllFitWithoutOverlapping()
        {
            // Four dressing cards over chefs about 200 px apart: they'd stack without spreading.
            var widths = new[] { 440f, 440f, 440f, 440f };
            var lefts = new List<float> { 540f, 740f, 960f, 1140f };
            HudRow.Spread(lefts, widths, Gap, Min, Max);
            AssertNoOverlap(lefts, widths);
            for (var i = 0; i < lefts.Count; i++)
            {
                Assert.That(lefts[i], Is.GreaterThanOrEqualTo(Min));
                Assert.That(lefts[i] + widths[i], Is.LessThanOrEqualTo(Max + 0.01f));
            }
            Assert.That(lefts[0], Is.LessThan(lefts[1]), "left to right order kept");
            Assert.That(lefts[2], Is.LessThan(lefts[3]));
        }

        [Test]
        public void ARowAtTheEdgeIsPushedBackOnScreen()
        {
            var widths = new[] { 220f, 440f };
            var lefts = new List<float> { 1500f, 1600f };
            HudRow.Spread(lefts, widths, Gap, Min, Max);
            AssertNoOverlap(lefts, widths);
            Assert.That(lefts[1] + widths[1], Is.LessThanOrEqualTo(Max + 0.01f));
        }

        [Test]
        public void ThePromptCardsFanOutAroundTheStation()
        {
            // First at the station, then right, left, and further right.
            Assert.That(PromptHud.SideBySideSlot(0), Is.EqualTo(0));
            Assert.That(PromptHud.SideBySideSlot(1), Is.EqualTo(1));
            Assert.That(PromptHud.SideBySideSlot(2), Is.EqualTo(-1));
            Assert.That(PromptHud.SideBySideSlot(3), Is.EqualTo(2));
        }
    }
}
