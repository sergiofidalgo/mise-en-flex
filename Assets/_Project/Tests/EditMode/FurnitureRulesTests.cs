using Game.Simulation.Days;
using Game.Simulation.Furniture;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class FurnitureRulesTests
    {
        private static SaleProblem Check(bool forSale = true, bool isEmpty = true, bool inUse = false, bool keepOne = false, int howMany = 3) =>
            FurnitureRules.CheckSale(forSale, isEmpty, inUse, keepOne, howMany);

        [Test]
        public void AnEmptySpareOneCanBeSold() => Assert.That(Check(), Is.EqualTo(SaleProblem.None));

        [Test]
        public void ThePassAndSinkAreNotForSale() => Assert.That(Check(forSale: false), Is.EqualTo(SaleProblem.NotForSale));

        [Test]
        public void ItHasToBeEmpty() => Assert.That(Check(isEmpty: false), Is.EqualTo(SaleProblem.NotEmpty));

        [Test]
        public void NotWhileSomeoneIsUsingIt() => Assert.That(Check(inUse: true), Is.EqualTo(SaleProblem.InUse));

        [Test]
        public void TheLastHobStays()
        {
            Assert.That(Check(keepOne: true, howMany: 1), Is.EqualTo(SaleProblem.LastOne));
            Assert.That(Check(keepOne: true, howMany: 2), Is.EqualTo(SaleProblem.None));
        }

        [Test]
        public void TheLastCounterCanGo() => Assert.That(Check(keepOne: false, howMany: 1), Is.EqualTo(SaleProblem.None));

        [Test]
        public void SellingPaysAWholeShareOfThePrice()
        {
            Assert.That(FurnitureRules.SellPrice(80f, 0.5f), Is.EqualTo(40f));
            Assert.That(FurnitureRules.SellPrice(25f, 0.5f), Is.EqualTo(13f));
        }

        [Test]
        public void SpendingNeedsTheMoney()
        {
            var days = new DayCycle(100f);
            Assert.That(days.TrySpend(30f), Is.True);
            Assert.That(days.Balance, Is.EqualTo(70f));
            Assert.That(days.TrySpend(80f), Is.False, "too much");
            Assert.That(days.Balance, Is.EqualTo(70f), "nothing taken");
            days.Receive(15f);
            Assert.That(days.Balance, Is.EqualTo(85f));
        }
    }
}
