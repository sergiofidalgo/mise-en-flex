using Game.Simulation.Furniture;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>The shop card's grid of pictures, three to a row (2026-11-11).</summary>
    public class ShopGridTests
    {
        [Test]
        public void LeftAndRightStepOneAndRunRound()
        {
            Assert.That(ShopGrid.Step(0, 5, 1, 0), Is.EqualTo(1));
            Assert.That(ShopGrid.Step(2, 5, 1, 0), Is.EqualTo(3), "on into the next row");
            Assert.That(ShopGrid.Step(4, 5, 1, 0), Is.EqualTo(0), "round from the last");
            Assert.That(ShopGrid.Step(0, 5, -1, 0), Is.EqualTo(4), "round from the first");
        }

        [Test]
        public void UpAndDownMoveARowAndStopAtTheEdges()
        {
            Assert.That(ShopGrid.Step(1, 5, 0, 1), Is.EqualTo(4), "down a row, same column");
            Assert.That(ShopGrid.Step(2, 5, 0, 1), Is.EqualTo(4), "a short last row: its nearest piece");
            Assert.That(ShopGrid.Step(4, 5, 0, 1), Is.EqualTo(4), "nothing below");
            Assert.That(ShopGrid.Step(1, 5, 0, -1), Is.EqualTo(1), "nothing above");
            Assert.That(ShopGrid.Step(4, 5, 0, -1), Is.EqualTo(1));
            Assert.That(ShopGrid.Step(0, 0, 1, 0), Is.EqualTo(0), "an empty page");
        }

        [Test]
        public void TheChosenRowStaysInView()
        {
            Assert.That(ShopGrid.FirstRow(4, 5, 2), Is.EqualTo(0), "two rows fit");
            Assert.That(ShopGrid.FirstRow(7, 9, 2), Is.EqualTo(1), "the third row scrolls in");
            Assert.That(ShopGrid.FirstRow(0, 9, 2), Is.EqualTo(0));
        }
    }
}
