using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Cooking;
using Game.Simulation.Days;
using Game.Simulation.Market;
using Game.Simulation.Storage;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Street vendors (Phase 5, milestone 3; decision 2026-11-10): bought stock goes straight into
    /// storage, fridge or shelf by what it is and where there's room, and is paid as ingredients.
    /// </summary>
    public class MarketTests
    {
        private CookingSimulation _sim;
        private StorageUnit _fridge;
        private StorageUnit _shelf;

        [SetUp]
        public void CreateKitchen()
        {
            _sim = new CookingSimulation(22f);
            _fridge = new StorageUnit(4f, new StorageUnit.Slot(6), new StorageUnit.Slot(6));
            _shelf = new StorageUnit(22f, new StorageUnit.Slot(6), new StorageUnit.Slot(6));
        }

        private static IngredientProperties Food(string typeId) => new IngredientProperties
        {
            TypeId = typeId,
            HeatCapacity = 1050f,
            SurfaceFraction = 0.15f,
            StartTemperature = 4f,
            FreshnessLossPerHourAt20C = 1f / 24f,
        };

        private Ingredient Make(string typeId, StorageUnit unit) => _sim.AddIngredient(Food(typeId));

        private List<StorageUnit> Order(bool keepsCold) =>
            StorageOrder.For(new[] { _fridge, _shelf }, unit => unit == _fridge, keepsCold);

        private static StallOffer Steak(Grade grade = Grade.Cheap) => new MarketSettings().Offer("Steak", 4f, grade);

        private void Fill(StorageUnit unit, string typeId, int count) => unit.Stock(typeId, () => Make(typeId, unit), count);

        [Test]
        public void MeatGoesToTheFridge()
        {
            var stocked = Steak().BuyInto(new DayCycle(100f), Order(keepsCold: true), unit => Make("Steak", unit));

            Assert.That(stocked.Single().Unit, Is.SameAs(_fridge));
            Assert.That(_fridge.Slots[0].Count, Is.EqualTo(6));
            Assert.That(_shelf.Slots.Sum(s => s.Count), Is.Zero);
        }

        [Test]
        public void MeatSpillsOntoTheShelfWhenTheFridgeIsFull()
        {
            Fill(_fridge, "Fish", 10); // one slot full, the other with 4: room for none of anything else

            var stocked = Steak().BuyInto(new DayCycle(100f), Order(keepsCold: true), unit => Make("Steak", unit));

            Assert.That(stocked.Single().Unit, Is.SameAs(_shelf));
            Assert.That(_shelf.Slots[0].Count, Is.EqualTo(6));
        }

        [Test]
        public void ShelfFoodGoesToTheShelfAndSpillsIntoTheFridge()
        {
            Fill(_shelf, "Onion", 9); // room for 3 more onions on the shelf

            var stocked = new MarketSettings().Offer("Onion", 1f, Grade.Cheap)
                .BuyInto(new DayCycle(100f), Order(keepsCold: false), unit => Make("Onion", unit));

            Assert.That(stocked.Select(s => (s.Unit, s.Count)), Is.EqualTo(new[] { (_shelf, 3), (_fridge, 3) }));
        }

        [Test]
        public void OnlyWhatFitsIsBoughtAndPaidFor()
        {
            Fill(_fridge, "Steak", 8); // room for 4 more steaks beside them
            Fill(_shelf, "Onion", 12);
            var days = new DayCycle(100f);
            var offer = Steak();

            Assert.That(offer.PortionsThatFit(Order(true)), Is.EqualTo(4));
            offer.BuyInto(days, Order(keepsCold: true), unit => Make("Steak", unit));

            Assert.That(days.Today.Of(LedgerLine.Ingredients), Is.EqualTo(16f));
            Assert.That(days.Balance, Is.EqualTo(84f));
        }

        [Test]
        public void WithNoRoomNothingIsBoughtOrPaid()
        {
            Fill(_fridge, "Fish", 12);
            Fill(_shelf, "Onion", 12);
            var days = new DayCycle(100f);

            var stocked = Steak().BuyInto(days, Order(keepsCold: true), unit => Make("Steak", unit));

            Assert.That(stocked, Is.Empty);
            Assert.That(days.Balance, Is.EqualTo(100f));
        }

        [Test]
        public void BuyingCanGoIntoDebtAndSaysSo()
        {
            var days = new DayCycle(5f);
            var charged = 0f;
            days.Charged += (line, amount) => charged = line == LedgerLine.Ingredients ? amount : -1f;

            Steak().BuyInto(days, Order(keepsCold: true), unit => Make("Steak", unit));

            Assert.That(days.Balance, Is.EqualTo(-19f));
            Assert.That(days.OwedToBoss, Is.EqualTo(19f));
            Assert.That(charged, Is.EqualTo(24f), "the money on screen hears about it");
        }

        [Test]
        public void PremiumCostsMoreAndStartsFresher()
        {
            var settings = new MarketSettings();
            var cheap = settings.Offer("Steak", 4f, Grade.Cheap);
            var premium = settings.Offer("Steak", 4f, Grade.Premium);
            Assert.That(premium.PriceFor(6), Is.GreaterThan(cheap.PriceFor(6)));

            var days = new DayCycle(100f);
            cheap.BuyInto(days, new[] { _fridge }, unit => Make("Steak", unit));
            premium.BuyInto(days, new[] { _shelf }, unit => Make("Steak", unit));
            Assert.That(_fridge.Slots[0].Stock[0].Freshness, Is.EqualTo(settings.CheapFreshness).Within(1e-5f));
            Assert.That(_shelf.Slots[0].Stock[0].Freshness, Is.EqualTo(settings.PremiumFreshness).Within(1e-5f));
        }

        [Test]
        public void CheapStockStaysBehindPremiumAsItAges()
        {
            var settings = new MarketSettings();
            var days = new DayCycle(100f);
            settings.Offer("Steak", 4f, Grade.Cheap).BuyInto(days, new[] { _shelf }, unit => Make("Steak", unit));
            settings.Offer("Steak", 4f, Grade.Premium).BuyInto(days, new[] { _fridge }, unit => Make("Steak", unit));
            var cheap = _shelf.Slots[0].Stock[0];
            var premium = _fridge.Slots[0].Stock[0];
            cheap.EnvironmentTemperature = premium.EnvironmentTemperature = 22f;

            _sim.SkipClosedTime(12f * 3600f);

            Assert.That(cheap.Freshness, Is.GreaterThan(0f));
            Assert.That(premium.Freshness - cheap.Freshness, Is.EqualTo(settings.PremiumFreshness - settings.CheapFreshness).Within(1e-4f));
        }

        [Test]
        public void StartingFreshnessNeverRaisesIt()
        {
            var steak = Make("Steak", _fridge);
            steak.SetStartingFreshness(0.5f);
            steak.SetStartingFreshness(0.9f);
            Assert.That(steak.Freshness, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void RoomCountsFreeSlotsAndRoomBesideTheSameFood()
        {
            Fill(_fridge, "Steak", 4);
            Assert.That(_fridge.RoomFor("Steak"), Is.EqualTo(8));
            Assert.That(_fridge.RoomFor("Onion"), Is.EqualTo(6));
        }
    }
}
