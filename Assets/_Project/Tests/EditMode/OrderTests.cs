using System;
using System.Linq;
using Game.Data;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Quality;
using Game.Simulation.Service;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode
{
    public class OrderTests
    {
        private static readonly DonenessLevel Rare = new DonenessLevel("Rare", 45f, 48f);
        private static readonly DonenessLevel MediumRare = new DonenessLevel("Medium-rare", 52f, 54.5f);
        private static readonly DonenessLevel Medium = new DonenessLevel("Medium", 57f, 60f);
        private static readonly DonenessLevel WellDone = new DonenessLevel("Well done", 68f, 71f);

        private static OrderMenu Menu(float sideChance = 0.4f) => new OrderMenu
        {
            MainTypeId = "Steak",
            Choices = new[]
            {
                new OrderChoice(Rare, 2f), new OrderChoice(MediumRare, 4f),
                new OrderChoice(Medium, 3f), new OrderChoice(WellDone, 1f),
            },
            SideTypeId = "DicedOnion",
            SideChance = sideChance,
        };

        // A steak whose core peaked at `peakCore` and that has rested, served hot, with a crust.
        private static Ingredient Steak(float peakCore)
        {
            var steak = new CookingSimulation(22f).AddIngredient(new IngredientProperties
            {
                TypeId = "Steak",
                HeatCapacity = 1050f,
                SurfaceFraction = 0.15f,
                DonenessLevels = new[] { Rare, MediumRare, Medium, new DonenessLevel("Medium-well", 63f, 65.5f), WellDone },
                StartTemperature = peakCore,
                TargetCoreTemperature = 54.5f,
            });
            return steak;
        }

        private static Ingredient Onion() => new CookingSimulation(22f).AddIngredient(new IngredientProperties
        {
            TypeId = "DicedOnion",
            HeatCapacity = 585f,
            SurfaceFraction = 0.3f,
            StartTemperature = 20f,
        });

        private static Plate PlateOf(params Item[] items)
        {
            var plate = new Plate(3);
            foreach (var item in items)
                plate.Put(item);
            return plate;
        }

        [Test]
        public void MenuPicksFollowTheWeights()
        {
            var menu = Menu(sideChance: 0.4f);
            var random = new Random(3);
            var orders = Enumerable.Range(0, 2000).Select(_ => menu.Pick(random)).ToArray();

            float Share(string name) => orders.Count(o => o.Doneness.Name == name) / 2000f;
            Assert.That(Share("Medium-rare"), Is.EqualTo(0.4f).Within(0.04f));
            Assert.That(Share("Rare"), Is.EqualTo(0.2f).Within(0.04f));
            Assert.That(Share("Well done"), Is.EqualTo(0.1f).Within(0.03f));
            Assert.That(orders.Count(o => o.SideTypeId != null) / 2000f, Is.EqualTo(0.4f).Within(0.04f));
            Assert.That(orders.All(o => o.MainTypeId == "Steak"));
        }

        [Test]
        public void EmptyMenuOrdersNothing()
        {
            Assert.That(new OrderMenu().Pick(new Random(1)), Is.Null);
        }

        [Test]
        public void EveryCustomerOrdersWhenTheServiceHasAMenu()
        {
            var service = new ServiceSimulation(new ServiceSettings { Menu = Menu() }, seatCount: 99);
            service.Tick(6f * 3600f);
            Assert.That(service.Customers, Is.Not.Empty);
            Assert.That(service.Customers.All(c => c.Order != null));
        }

        [Test]
        public void TheMenuDoesntChangeWhenCustomersArrive()
        {
            var plain = new ServiceSimulation(new ServiceSettings(), seatCount: 99);
            var withMenu = new ServiceSimulation(new ServiceSettings { Menu = Menu() }, seatCount: 99);
            plain.Tick(6f * 3600f);
            withMenu.Tick(6f * 3600f);
            Assert.That(withMenu.Customers.Count, Is.EqualTo(plain.Customers.Count));
        }

        [Test]
        public void DonenessIsScoredAgainstTheOrderNotTheHouseStandard()
        {
            var settings = new ScoringSettings();
            var wellDone = Steak(71f);

            var house = DishScorer.Score(PlateOf(wellDone), settings).Items[0];
            var ordered = DishScorer.Score(PlateOf(wellDone), settings, new Order("Steak", WellDone)).Items[0];

            Assert.That(house.Doneness, Is.LessThan(0.2f), "well done is far off the medium-rare house standard");
            Assert.That(ordered.Doneness, Is.EqualTo(1f));
            Assert.That(ordered.TargetDonenessName, Is.EqualTo("Well done"));
            Assert.That(house.TargetDonenessName, Is.EqualTo("Medium-rare"));
        }

        [Test]
        public void AMissingSideCostsPointsButLessThanAMissingMain()
        {
            var settings = new ScoringSettings();
            var order = new Order("Steak", MediumRare, "DicedOnion");

            var full = DishScorer.Score(PlateOf(Steak(54.5f), Onion()), settings, order);
            var noOnion = DishScorer.Score(PlateOf(Steak(54.5f)), settings, order);
            var onlyOnion = DishScorer.Score(PlateOf(Onion()), settings, order);

            Assert.That(noOnion.Missing, Is.EqualTo(new[] { "DicedOnion" }));
            Assert.That(onlyOnion.Missing, Is.EqualTo(new[] { "Steak" }));
            Assert.That(full.Missing, Is.Empty);
            Assert.That(full.Match, Is.EqualTo(OrderMatch.Matches));
            Assert.That(noOnion.Match, Is.EqualTo(OrderMatch.MissingSide));
            Assert.That(DishScorer.Score(PlateOf(Steak(54.5f)), settings).Match, Is.Null, "no order, nothing to match");
            Assert.That(noOnion.Overall, Is.LessThan(full.Overall));
            Assert.That(onlyOnion.Overall, Is.LessThan(noOnion.Overall));
        }

        [Test]
        public void WithoutAnOrderEveryItemCountsTheSame()
        {
            var settings = new ScoringSettings();
            var steak = Steak(54.5f);
            var onion = Onion();
            var together = DishScorer.Score(PlateOf(steak, onion), settings).Overall;
            var average = (DishScorer.ScoreItem(steak, settings).Overall + DishScorer.ScoreItem(onion, settings).Overall) / 2f;
            Assert.That(together, Is.EqualTo(average).Within(1e-5f));
        }

        [Test]
        public void CheckSaysWhatsWrongWithAPlate()
        {
            var order = new Order("Steak", MediumRare, "DicedOnion");
            Assert.That(OrderCheck.Check(order, PlateOf(Steak(54f), Onion()), out var main), Is.EqualTo(OrderMatch.Matches));
            Assert.That(main, Is.Not.Null);
            Assert.That(OrderCheck.Check(order, PlateOf(Steak(54f)), out _), Is.EqualTo(OrderMatch.MissingSide));
            Assert.That(OrderCheck.Check(order, PlateOf(Steak(60f), Onion()), out _), Is.EqualTo(OrderMatch.WrongDoneness));
            Assert.That(OrderCheck.Check(order, PlateOf(Steak(20f), Onion()), out _), Is.EqualTo(OrderMatch.WrongDoneness), "raw");
            Assert.That(OrderCheck.Check(order, PlateOf(Onion()), out _), Is.EqualTo(OrderMatch.MissingMain));
        }

        [Test]
        public void OrderTargetsSitInsideTheirBand()
        {
            var levels = AssetDatabase.LoadAssetAtPath<IngredientDefinition>("Assets/_Project/Data/Ingredients/ING_Steak.asset")
                .ToProperties().DonenessLevels;
            for (var i = 0; i < levels.Count; i++)
            {
                var upper = i + 1 < levels.Count ? levels[i + 1].MinPeakCoreTemperature : float.MaxValue;
                Assert.That(levels[i].OrderTargetCoreTemperature, Is.GreaterThanOrEqualTo(levels[i].MinPeakCoreTemperature).And.LessThan(upper), levels[i].Name);
            }
        }

        [Test]
        public void ServiceConfigMenuIsSteakWithOnions()
        {
            var config = AssetDatabase.LoadAssetAtPath<ServiceConfig>("Assets/_Project/Data/Settings/ServiceConfig.asset");
            var menu = config.ToSettings().Menu;
            Assert.That(menu, Is.Not.Null, "run Tools/Game/Build Greybox Kitchen to fill the menu");
            Assert.That(menu.MainTypeId, Is.EqualTo("Steak"));
            Assert.That(menu.SideTypeId, Is.EqualTo("DicedOnion"));
            Assert.That(config.UnknownDonenesses(), Is.Empty);
            Assert.That(menu.Choices.Count, Is.EqualTo(4));
        }
    }
}
