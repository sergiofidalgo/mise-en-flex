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
    /// Street vendors (Phase 5, milestone 3; decisions 2026-11-10 and 2026-11-11): each press buys one
    /// fresh portion, straight into storage, fridge or shelf by what it is and where there's room,
    /// paid as ingredients.
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

        private Ingredient Make(string typeId) => _sim.AddIngredient(new IngredientProperties
        {
            TypeId = typeId,
            HeatCapacity = 1050f,
            SurfaceFraction = 0.15f,
            StartTemperature = 4f,
            FreshnessLossPerHourAt20C = 1f / 24f,
        });

        private List<StorageUnit> Order(bool keepsCold) =>
            StorageOrder.For(new[] { _fridge, _shelf }, unit => unit == _fridge, keepsCold);

        private StorageUnit Buy(string typeId, bool keepsCold, DayCycle days = null, float price = 6f) =>
            new StallOffer(typeId, price).BuyInto(days ?? new DayCycle(100f), Order(keepsCold), _ => Make(typeId));

        private void Fill(StorageUnit unit, string typeId, int count) => unit.Stock(typeId, () => Make(typeId), count);

        private static int Count(StorageUnit unit) => unit.Slots.Sum(slot => slot.Count);

        [Test]
        public void EachPurchaseIsOnePortion()
        {
            Buy("Steak", keepsCold: true);
            Assert.That(Count(_fridge), Is.EqualTo(1));
            Buy("Steak", keepsCold: true);
            Assert.That(_fridge.Slots[0].Count, Is.EqualTo(2), "beside the first");
        }

        [Test]
        public void BrandNewStockIsFullyFresh()
        {
            Buy("Steak", keepsCold: true);
            Assert.That(_fridge.Slots[0].Stock[0].Freshness, Is.EqualTo(1f));
        }

        [Test]
        public void MeatGoesToTheFridge()
        {
            Assert.That(Buy("Steak", keepsCold: true), Is.SameAs(_fridge));
            Assert.That(Count(_shelf), Is.Zero);
        }

        [Test]
        public void MeatGoesOnTheShelfWhenTheFridgeIsFull()
        {
            Fill(_fridge, "Fish", 12);
            Assert.That(Buy("Steak", keepsCold: true), Is.SameAs(_shelf));
        }

        [Test]
        public void ShelfFoodGoesToTheShelfThenIntoTheFridge()
        {
            Assert.That(Buy("Onion", keepsCold: false), Is.SameAs(_shelf));
            Fill(_shelf, "Bread", 6);
            Fill(_shelf, "Onion", 5); // the onions' slot is full now
            Assert.That(Buy("Onion", keepsCold: false), Is.SameAs(_fridge));
        }

        [Test]
        public void ItsPaidAtOnceAsIngredients()
        {
            var days = new DayCycle(100f);
            var charged = 0f;
            days.Charged += (line, amount) => charged = line == LedgerLine.Ingredients ? amount : -1f;

            Buy("Tomato", keepsCold: false, days, price: 0.5f);

            Assert.That(days.Today.Of(LedgerLine.Ingredients), Is.EqualTo(0.5f));
            Assert.That(days.Balance, Is.EqualTo(99.5f));
            Assert.That(charged, Is.EqualTo(0.5f), "the money on screen hears about it");
        }

        [Test]
        public void BuyingCanGoIntoDebt()
        {
            var days = new DayCycle(2f);
            Buy("Steak", keepsCold: true, days);
            Assert.That(days.Balance, Is.EqualTo(-4f));
            Assert.That(days.OwedToBoss, Is.EqualTo(4f));
        }

        [Test]
        public void WithNoRoomNothingIsBoughtOrPaid()
        {
            Fill(_fridge, "Fish", 12);
            Fill(_shelf, "Onion", 12);
            var days = new DayCycle(100f);
            var offer = new StallOffer("Steak", 6f);

            Assert.That(offer.Fits(Order(true)), Is.False);
            Assert.That(offer.BuyInto(days, Order(true), _ => Make("Steak")), Is.Null);
            Assert.That(days.Balance, Is.EqualTo(100f));
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
