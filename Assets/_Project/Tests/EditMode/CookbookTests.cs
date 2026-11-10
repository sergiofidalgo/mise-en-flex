using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Simulation.Cookbook;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Quality;
using Game.Simulation.Saving;
using Game.Simulation.Service;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace Game.Tests.EditMode
{
    /// <summary>The cookbook, the menu it makes and how prices move customers (decisions 2026-11-10).</summary>
    public class CookbookTests
    {
        private const string CatalogPath = "Assets/_Project/Data/Items/ItemCatalog.asset";
        private const string ConfigPath = "Assets/_Project/Data/Settings/CookbookConfig.asset";
        private static readonly DonenessLevel Rare = new DonenessLevel("Rare", 45f, 48f);
        private static readonly DonenessLevel Medium = new DonenessLevel("Medium", 57f, 60f);
        private static readonly DonenessLevel Cooked = new DonenessLevel("Cooked", 92f, 92f);

        private static Recipe Steak() => new Recipe
        {
            Id = "steak", Name = "Steak", MainTypeId = "Steak", MainName = "Steak",
            Choices = new[] { new OrderChoice(Rare, 1f), new OrderChoice(Medium, 1f) },
            SuggestedPrice = 20, StartsUnlocked = true,
        };

        private static Recipe SteakOnions() => new Recipe
        {
            Id = "steak-onions", Name = "Steak with sautéed onions", MainTypeId = "Steak", MainName = "Steak",
            Choices = new[] { new OrderChoice(Medium, 1f) },
            SideTypeId = "DicedOnion", SideName = "Onions",
            SuggestedPrice = 25, SuggestedSidePrice = 5, UnlockCost = 60,
        };

        private static Recipe Onions() => new Recipe
        {
            Id = "onions", Name = "Sautéed onions", MainTypeId = "DicedOnion", MainName = "Onions",
            Choices = new[] { new OrderChoice(Cooked, 1f) },
            SuggestedPrice = 8, UnlockCost = 30, Popularity = 1f,
        };

        private static RecipeBook Book(PricingSettings pricing = null) =>
            new RecipeBook(new[] { Steak(), SteakOnions(), Onions() }, pricing ?? new PricingSettings());

        // ---- Unlocking ----

        [Test]
        public void ANewCampaignStartsWithOnlyTheStarterOnItsMenu()
        {
            var book = Book();
            Assert.That(book.Menu.Select(r => r.Id), Is.EqualTo(new[] { "steak" }));
            Assert.That(book.Recipes.Count, Is.EqualTo(3), "every recipe can be browsed");
        }

        [Test]
        public void UnlockingPaysAndPutsTheDishOnTheMenu()
        {
            var book = Book();
            var balance = 100f;
            bool Spend(float amount)
            {
                if (amount > balance)
                    return false;
                balance -= amount;
                return true;
            }

            Assert.That(book.TryUnlock("steak-onions", Spend), Is.EqualTo(UnlockResult.Unlocked));
            Assert.That(balance, Is.EqualTo(40f));
            Assert.That(book.Menu.Select(r => r.Id), Is.EqualTo(new[] { "steak", "steak-onions" }));

            Assert.That(book.TryUnlock("steak-onions", Spend), Is.EqualTo(UnlockResult.AlreadyUnlocked));
            Assert.That(balance, Is.EqualTo(40f), "paid once");
            Assert.That(book.TryUnlock("nope", Spend), Is.EqualTo(UnlockResult.UnknownRecipe));
        }

        [Test]
        public void ARecipeYouCantAffordStaysLocked()
        {
            var book = Book();
            Assert.That(book.TryUnlock("steak-onions", _ => false), Is.EqualTo(UnlockResult.CantAfford));
            Assert.That(book.IsUnlocked("steak-onions"), Is.False);
        }

        [Test]
        public void UnlockingRaisesItsEvents()
        {
            var book = Book();
            Recipe unlocked = null;
            var changed = 0;
            book.Unlocked += r => unlocked = r;
            book.Changed += () => changed++;
            book.TryUnlock("onions", _ => true);
            Assert.That(unlocked?.Id, Is.EqualTo("onions"));
            Assert.That(changed, Is.EqualTo(1));
        }

        // ---- Prices ----

        [Test]
        public void PricesStartAtTheSuggestionAndMoveInStepsOfOne()
        {
            var book = Book();
            Assert.That(book.PriceOf("steak"), Is.EqualTo(20));
            book.Nudge("steak", 1);
            book.Nudge("steak", 1);
            Assert.That(book.PriceOf("steak"), Is.EqualTo(22));
            book.Nudge("steak", -5);
            Assert.That(book.PriceOf("steak"), Is.EqualTo(17));
        }

        [Test]
        public void PricesStayBetweenTheLowestAndHighestAllowed()
        {
            var book = Book(new PricingSettings { MinPrice = 1, MaxPriceFactor = 3f });
            book.SetPrice("steak", -10);
            Assert.That(book.PriceOf("steak"), Is.EqualTo(1));
            book.SetPrice("steak", 1000);
            Assert.That(book.PriceOf("steak"), Is.EqualTo(60));
        }

        // ---- The menu ----

        [Test]
        public void TheMenuHasOnlyUnlockedDishesAtTheirPrices()
        {
            var book = Book();
            book.TryUnlock("steak-onions", _ => true);
            book.SetPrice("steak-onions", 30);
            var menu = book.BuildMenu();

            Assert.That(menu.Dishes.Select(d => d.RecipeId), Is.EqualTo(new[] { "steak", "steak-onions" }));
            var onions = menu.Dishes[1];
            Assert.That(onions.SideChance, Is.EqualTo(1f), "a recipe's side always comes with it");
            Assert.That(onions.SidePrice, Is.EqualTo(6f), "the side keeps its fifth of the price");
            Assert.That(onions.MainPrice + onions.SidePrice, Is.EqualTo(30f));

            var random = new Random(3);
            for (var i = 0; i < 200; i++)
            {
                var order = menu.Pick(random);
                Assert.That(new[] { "steak", "steak-onions" }, Does.Contain(order.RecipeId));
                Assert.That(order.MainPrice + (order.SideTypeId != null ? order.SidePrice : 0f), Is.EqualTo(order.RecipeId == "steak" ? 20f : 30f));
            }
        }

        [Test]
        public void AtTheSuggestedPricesNothingShifts()
        {
            var menu = Book().BuildMenu();
            Assert.That(menu.Demand, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(menu.Dishes[0].Expectation, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(menu.Dishes[0].Weight, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void ADearerDishIsOrderedLessOften()
        {
            var book = Book();
            book.TryUnlock("onions", _ => true);
            int Count(RecipeBook b)
            {
                var menu = b.BuildMenu();
                var random = new Random(11);
                return Enumerable.Range(0, 4000).Count(_ => menu.Pick(random).RecipeId == "steak");
            }

            var even = Count(book);
            book.SetPrice("steak", 40);
            var dear = Count(book);
            Assert.That(even, Is.InRange(1800, 2200), "same popularity, same price ratio: about half each");
            Assert.That(dear, Is.LessThan(even * 0.8f));
        }

        [Test]
        public void AMenuWithoutDishesStillPicksItsSingleDish()
        {
            // Tests and a scene without a cookbook describe one dish on the menu itself.
            var menu = new OrderMenu { MainTypeId = "Steak", MainName = "Steak", MainPrice = 24f, Choices = new[] { new OrderChoice(Rare, 1f) } };
            var order = menu.Pick(new Random(1));
            Assert.That(order.MainTypeId, Is.EqualTo("Steak"));
            Assert.That(order.Expectation, Is.EqualTo(0f));
            Assert.That(new OrderMenu { Dishes = new[] { new MenuDish { MainTypeId = "Steak", Choices = new[] { new OrderChoice(Rare, 1f) }, Weight = 0f } } }.Pick(new Random(1)), Is.Null);
        }

        // ---- How price moves customers ----

        [Test]
        public void DemandFollowsThePowerCurveWithinItsLimits()
        {
            var settings = new PricingSettings { DemandElasticity = 1.5f, MinDemand = 0.15f, MaxDemand = 1.5f };
            Assert.That(PriceResponse.Demand(1f, settings), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(PriceResponse.Demand(1.2f, settings), Is.EqualTo(0.7607f).Within(1e-3f), "20% dearer: about 24% fewer");
            Assert.That(PriceResponse.Demand(0.8f, settings), Is.EqualTo(1.3975f).Within(1e-3f), "20% cheaper: about 40% more");
            Assert.That(PriceResponse.Demand(0.2f, settings), Is.EqualTo(1.5f), "however cheap");
            Assert.That(PriceResponse.Demand(10f, settings), Is.EqualTo(0.15f), "however dear");
        }

        [Test]
        public void ExpectationsRiseWithThePrice()
        {
            var settings = new PricingSettings { StarsPerRatio = 2.5f, MaxStarShift = 1f };
            Assert.That(PriceResponse.Expectation(1.2f, settings), Is.EqualTo(-0.5f).Within(1e-5f));
            Assert.That(PriceResponse.Expectation(0.8f, settings), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(PriceResponse.Expectation(3f, settings), Is.EqualTo(-1f), "price alone never decides a review");
            Assert.That(PriceResponse.Expectation(0.1f, settings), Is.EqualTo(1f));
        }

        [Test]
        public void TheMenusDemandAveragesItsDishesByPopularity()
        {
            var book = Book();
            book.TryUnlock("onions", _ => true);
            book.SetPrice("steak", 30); // 1.5× suggested
            // Onions at 1×, equally popular: the average ratio is 1.25.
            var expected = PriceResponse.Demand(1.25f, book.Pricing);
            Assert.That(book.BuildMenu().Demand, Is.EqualTo(expected).Within(1e-5f));
        }

        [Test]
        public void HigherPricesBringFewerCustomers()
        {
            int Arrivals(float demand)
            {
                var settings = new ServiceSettings
                {
                    DurationSeconds = 5f * 3600f, LastOrdersSeconds = 0f, ArrivalIntervalSeconds = 10f * 60f,
                    ArrivalJitter = 0f, RushMultiplier = 1f, PatienceSeconds = 24f * 3600f, MaxQueue = 100, Seed = 5,
                };
                var service = new ServiceSimulation(settings, seatCount: 100);
                service.SetMenu(new OrderMenu { MainTypeId = "Steak", Choices = new[] { new OrderChoice(Rare, 1f) }, Demand = demand });
                service.Tick(5f * 3600f);
                return service.Customers.Count;
            }

            Assert.That(Arrivals(1f), Is.EqualTo(29));
            Assert.That(Arrivals(0.5f), Is.EqualTo(14), "half the demand, twice the gap");
            Assert.That(Arrivals(1.5f), Is.GreaterThan(Arrivals(1f)));
        }

        [Test]
        public void TheMenuIsSetBeforeTheFirstCustomer()
        {
            var settings = new ServiceSettings { ArrivalIntervalSeconds = 60f, ArrivalJitter = 0f };
            var service = new ServiceSimulation(settings, seatCount: 3);
            service.Tick(5f * 60f);
            Assert.Throws<InvalidOperationException>(() => service.SetMenu(new OrderMenu()));
        }

        [Test]
        public void ADearDishIsRatedAgainstWhatItCost()
        {
            var settings = new ServiceSettings { ArrivalIntervalSeconds = 60f, ArrivalJitter = 0f, PatienceSeconds = 24f * 3600f };
            var service = new ServiceSimulation(settings, seatCount: 3);
            var dear = new MenuDish { RecipeId = "steak", MainTypeId = "Steak", Choices = new[] { new OrderChoice(Rare, 1f) }, Expectation = -0.5f };
            service.SetMenu(new OrderMenu { Dishes = new[] { dear } });
            service.Tick(61f);
            var customer = service.Customers.Single();
            service.SitDown(customer);
            var plate = new Plate(3);
            plate.Put(new Item("Steak"));
            service.Serve(customer.Seat, plate, new DishScore(Array.Empty<ItemScore>(), 1f));

            Assert.That(customer.Satisfaction.Stars, Is.EqualTo(5f), "the dish itself was perfect");
            Assert.That(customer.RatedStars, Is.EqualTo(4.5f), "but they paid 20% over the odds");
            Assert.That(service.AverageStars, Is.EqualTo(4.5f));
        }

        [Test]
        public void ACheapDishCantRateAboveFiveStars()
        {
            var settings = new ServiceSettings { ArrivalIntervalSeconds = 60f, ArrivalJitter = 0f, PatienceSeconds = 24f * 3600f };
            var service = new ServiceSimulation(settings, seatCount: 3);
            service.SetMenu(new OrderMenu { Dishes = new[] { new MenuDish { MainTypeId = "Steak", Choices = new[] { new OrderChoice(Rare, 1f) }, Expectation = 1f } } });
            service.Tick(61f);
            var customer = service.Customers.Single();
            service.SitDown(customer);
            var plate = new Plate(3);
            plate.Put(new Item("Steak"));
            service.Serve(customer.Seat, plate, new DishScore(Array.Empty<ItemScore>(), 0.9f));
            Assert.That(customer.RatedStars, Is.EqualTo(5f));
        }

        // ---- Saves ----

        [Test]
        public void UnlocksAndPricesComeBackFromTheSave()
        {
            var book = Book();
            book.TryUnlock("onions", _ => true);
            book.SetPrice("onions", 11);
            book.SetPrice("steak", 18);

            var save = new CampaignSave();
            save.Recipes.AddRange(book.UnlockedIds());
            save.Prices.AddRange(book.PriceRecords());
            var loaded = JsonUtility.FromJson<CampaignSave>(JsonUtility.ToJson(save));

            var again = Book();
            again.Load(loaded.Recipes, loaded.Prices);
            Assert.That(again.Menu.Select(r => r.Id), Is.EqualTo(new[] { "steak", "onions" }));
            Assert.That(again.PriceOf("onions"), Is.EqualTo(11));
            Assert.That(again.PriceOf("steak"), Is.EqualTo(18));
            Assert.That(again.PriceOf("steak-onions"), Is.EqualTo(25), "never set: still the suggestion");
        }

        [Test]
        public void ASaveFromBeforeTheCookbookStartsWithTheStarters()
        {
            var save = JsonUtility.FromJson<CampaignSave>("{\"Version\":1,\"Day\":4,\"Balance\":10}");
            Assert.That(save.Recipes, Is.Empty);
            Assert.That(save.Prices, Is.Empty);
            var book = Book();
            book.Load(save.Recipes, save.Prices);
            Assert.That(book.Menu.Select(r => r.Id), Is.EqualTo(new[] { "steak" }));
        }

        [Test]
        public void RecipesTheCookbookNoLongerHasAreDropped()
        {
            var book = Book();
            book.Load(new[] { "gone", "onions" }, new[] { new PriceRecord { Recipe = "gone", Price = 5 }, new PriceRecord { Recipe = "onions", Price = 900 } });
            Assert.That(book.UnlockedIds(), Is.EqualTo(new[] { "steak", "onions" }));
            Assert.That(book.PriceOf("onions"), Is.EqualTo(40), "kept within the highest allowed (5× suggested)");
        }

        [Test]
        public void TheSharedCookbookKeepsEachRecipeOnce()
        {
            var shared = new SharedCookbook();
            Assert.That(shared.Add("steak"), Is.True);
            Assert.That(shared.Add("steak"), Is.False);
            Assert.That(shared.Add(""), Is.False);
            var loaded = JsonUtility.FromJson<SharedCookbook>(JsonUtility.ToJson(shared));
            Assert.That(loaded.Upgrade(), Is.True);
            Assert.That(loaded.Unlocked, Is.EqualTo(new[] { "steak" }));
            Assert.That(new SharedCookbook { Version = SharedCookbook.CurrentVersion + 1 }.Upgrade(), Is.False, "a newer game's file is left alone");
        }

        // ---- The real recipes ----

        private static IEnumerable<CookbookConfig> Configs()
        {
            yield return ScriptableObject.CreateInstance<CookbookConfig>(); // the defaults a fresh asset gets
            var asset = AssetDatabase.LoadAssetAtPath<CookbookConfig>(ConfigPath);
            if (asset != null)
                yield return asset;
        }

        [Test]
        public void EveryRecipeIsMadeFromThingsThatExist()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            foreach (var config in Configs())
            {
                Assert.That(config.Entries.Select(e => e.Id).Distinct().Count(), Is.EqualTo(config.Entries.Count), "ids are unique");
                foreach (var entry in config.Entries)
                {
                    var main = catalog.Find(entry.Main);
                    Assert.That(main, Is.Not.Null, $"{entry.Id}: main {entry.Main}");
                    if (entry.Side != null)
                        Assert.That(catalog.Find(entry.Side), Is.Not.Null, $"{entry.Id}: side {entry.Side}");
                    foreach (var id in entry.Ingredients)
                        Assert.That(catalog.Find(id), Is.Not.Null, $"{entry.Id}: ingredient {id}");
                    var levels = main.ToProperties().DonenessLevels.Select(l => l.Name).ToList();
                    foreach (var doneness in entry.Donenesses)
                        Assert.That(levels, Does.Contain(doneness), $"{entry.Id}: doneness {doneness}");
                    var recipe = entry.ToRecipe(catalog);
                    Assert.That(recipe.Choices.Count, Is.GreaterThan(0), $"{entry.Id} can be ordered");
                    Assert.That(recipe.Value, Is.GreaterThan(0f), $"{entry.Id} earns more than it costs");
                }
            }
        }

        [Test]
        public void TheBossesDishIsFreeAndOnEveryNewMenu()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            foreach (var config in Configs())
            {
                var book = config.NewBook(catalog);
                var starters = book.Menu.ToList();
                Assert.That(starters, Is.Not.Empty, "a new campaign can always open");
                Assert.That(starters.All(r => r.UnlockCost == 0), Is.True);
                Assert.That(book.Recipes.Where(r => !r.StartsUnlocked).All(r => r.UnlockCost > 0), Is.True, "the rest are bought");
            }
        }

        [Test]
        public void HarderRecipesPayMore()
        {
            // Game design §15.5: harder recipes pay more.
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            var steaks = ScriptableObject.CreateInstance<CookbookConfig>().ToRecipes(catalog)
                .Where(r => r.MainTypeId == "Steak").OrderBy(r => r.Complexity).ToList();
            for (var i = 1; i < steaks.Count; i++)
                Assert.That(steaks[i].Value, Is.GreaterThan(steaks[i - 1].Value), $"{steaks[i].Id} over {steaks[i - 1].Id}");
        }
    }
}
