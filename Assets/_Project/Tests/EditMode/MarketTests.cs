using Game.Simulation.Cooking;
using Game.Simulation.Days;
using Game.Simulation.Items;
using Game.Simulation.Market;
using Game.Simulation.Storage;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Street vendors (Phase 5, milestone 3): crates, grades, paying, unloading.</summary>
    public class MarketTests
    {
        private static IngredientProperties Food(string typeId = "Steak") => new IngredientProperties
        {
            TypeId = typeId,
            HeatCapacity = 1050f,
            SurfaceFraction = 0.15f,
            StartTemperature = 4f,
            FreshnessLossPerHourAt20C = 1f / 24f,
        };

        private static CookingSimulation Sim() => new CookingSimulation(22f);

        [Test]
        public void ACrateIsAFullStorageSlotOfOneIngredient()
        {
            var sim = Sim();
            var offer = new MarketSettings().Offer("Steak", 4f, Grade.Cheap);
            var crate = offer.Buy(new DayCycle(100f), () => sim.AddIngredient(Food()));

            Assert.That(crate.Items.Count, Is.EqualTo(6));
            Assert.That(crate.HoldsTypeId, Is.EqualTo("Steak"));
            Assert.That(crate.CanAccept(sim.AddIngredient(Food("Onion"))), Is.False);
        }

        [Test]
        public void BuyingIsPaidAtOnceAsIngredients()
        {
            var sim = Sim();
            var days = new DayCycle(100f);
            var offer = new MarketSettings().Offer("Steak", 4f, Grade.Cheap);

            offer.Buy(days, () => sim.AddIngredient(Food()));

            Assert.That(offer.CratePrice, Is.EqualTo(24f));
            Assert.That(days.Balance, Is.EqualTo(76f));
            Assert.That(days.Today.Of(LedgerLine.Ingredients), Is.EqualTo(24f));
        }

        [Test]
        public void BuyingCanGoIntoDebt()
        {
            var sim = Sim();
            var days = new DayCycle(5f);
            var offer = new MarketSettings().Offer("Steak", 4f, Grade.Cheap);

            var crate = offer.Buy(days, () => sim.AddIngredient(Food()));

            Assert.That(crate.Items.Count, Is.EqualTo(6));
            Assert.That(days.Balance, Is.EqualTo(-19f));
            Assert.That(days.OwedToBoss, Is.EqualTo(19f));
        }

        [Test]
        public void PremiumCostsMoreAndStartsFresher()
        {
            var sim = Sim();
            var settings = new MarketSettings();
            var cheap = settings.Offer("Steak", 4f, Grade.Cheap);
            var premium = settings.Offer("Steak", 4f, Grade.Premium);

            Assert.That(premium.CratePrice, Is.GreaterThan(cheap.CratePrice));

            var days = new DayCycle(100f);
            var cheapSteak = (Ingredient)cheap.Buy(days, () => sim.AddIngredient(Food())).Items[0];
            var premiumSteak = (Ingredient)premium.Buy(days, () => sim.AddIngredient(Food())).Items[0];
            Assert.That(premiumSteak.Freshness, Is.GreaterThan(cheapSteak.Freshness));
            Assert.That(cheapSteak.Freshness, Is.EqualTo(settings.CheapFreshness).Within(1e-5f));
        }

        [Test]
        public void CheapStockSpoilsSoonerThanPremium()
        {
            var sim = Sim();
            var settings = new MarketSettings();
            var days = new DayCycle(100f);
            var cheap = (Ingredient)settings.Offer("Steak", 4f, Grade.Cheap).Buy(days, () => sim.AddIngredient(Food())).Items[0];
            var premium = (Ingredient)settings.Offer("Steak", 4f, Grade.Premium).Buy(days, () => sim.AddIngredient(Food())).Items[0];

            // Same air, same hours: they age alike, so the cheap one stays behind and spoils first.
            sim.SkipClosedTime(12f * 3600f);
            Assert.That(cheap.Freshness, Is.GreaterThan(0f));
            Assert.That(premium.Freshness - cheap.Freshness, Is.EqualTo(settings.PremiumFreshness - settings.CheapFreshness).Within(1e-4f));
        }

        [Test]
        public void StartingFreshnessNeverRaisesIt()
        {
            var sim = Sim();
            var steak = sim.AddIngredient(Food());
            steak.SetStartingFreshness(0.5f);
            steak.SetStartingFreshness(0.9f);
            Assert.That(steak.Freshness, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void InteractAtStorageUnloadsAsManyAsFit()
        {
            var sim = Sim();
            var crate = new MarketSettings().Offer("Steak", 4f, Grade.Cheap).Buy(new DayCycle(100f), () => sim.AddIngredient(Food()));
            var fridge = new StorageUnit(4f, new StorageUnit.Slot(4));
            var hands = new ItemSlot();
            hands.Put(crate);

            var outcome = ItemTransfer.Interact(hands, fridge);

            Assert.That(outcome, Is.EqualTo(InteractionOutcome.Combined));
            Assert.That(fridge.Slots[0].Count, Is.EqualTo(4));
            Assert.That(hands.Current, Is.SameAs(crate), "what doesn't fit stays in the crate");
            Assert.That(crate.Items.Count, Is.EqualTo(2));
        }

        [Test]
        public void ACrateGoesDownWholeOnACounterAndComesBackWhole()
        {
            var sim = Sim();
            var crate = new MarketSettings().Offer("Onion", 1f, Grade.Premium).Buy(new DayCycle(100f), () => sim.AddIngredient(Food("Onion")));
            var hands = new ItemSlot();
            var counter = new ItemSlot();
            hands.Put(crate);

            Assert.That(ItemTransfer.Interact(hands, counter), Is.EqualTo(InteractionOutcome.Placed));
            Assert.That(ItemTransfer.Interact(hands, counter), Is.EqualTo(InteractionOutcome.PickedUp));
            Assert.That(hands.Current, Is.SameAs(crate));
            Assert.That(crate.Items.Count, Is.EqualTo(6));
        }

        [Test]
        public void ACrateTakesNoCookedFood()
        {
            var sim = Sim();
            var crate = new Crate(6);
            var steak = sim.AddIngredient(Food());
            steak.Restore(new Game.Simulation.Saving.IngredientState { Browning = new[] { 0.5f, 0f } });
            Assert.That(crate.CanAccept(steak), Is.False);
        }
    }
}
