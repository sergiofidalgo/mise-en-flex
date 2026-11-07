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
    public class FlipGaugeTests
    {
        private const float Minute = 60f;
        private const string SteakPath = "Assets/_Project/Data/Ingredients/ING_Steak.asset";

        private static IngredientDefinition SteakDefinition => AssetDatabase.LoadAssetAtPath<IngredientDefinition>(SteakPath);
        private static EquipmentTier Equipment => AssetDatabase.LoadAssetAtPath<EquipmentTier>("Assets/_Project/Data/Settings/EquipmentTier_Basic.asset");
        private static TuningConfig Tuning => AssetDatabase.LoadAssetAtPath<TuningConfig>("Assets/_Project/Data/Settings/TuningConfig.asset");
        private static RealismSettings Realism => AssetDatabase.LoadAssetAtPath<RealismSettings>("Assets/_Project/Data/Settings/RealismSettings.asset");

        private static (CookingSimulation Sim, Hob Hob, Pan Pan) Kitchen(float preheatSeconds = 2.5f * Minute, float dial = 1f)
        {
            var sim = new CookingSimulation(Tuning.AmbientTemperature);
            var hob = sim.AddHob(Equipment.Hob);
            var pan = sim.AddPan(Equipment.Pan);
            hob.PlacePan(pan);
            hob.Dial = dial;
            sim.Tick(preheatSeconds);
            return (sim, hob, pan);
        }

        // In-game seconds until the side facing down reaches this much browning.
        private static float SecondsUntilBrowning(CookingSimulation sim, Ingredient steak, float browning)
        {
            var seconds = 0f;
            while (steak.Browning(steak.DownSide) < browning && seconds < 30f * Minute)
            {
                sim.Tick(1f);
                seconds += 1f;
            }
            return seconds;
        }

        [Test]
        public void TheGaugeShowsOnlyUntilTheFirstFlip()
        {
            var steak = new CookingSimulation(22f).AddIngredient(SteakDefinition.ToProperties());
            Assert.That(FlipGauge.Shows(steak));
            steak.Flip();
            Assert.That(FlipGauge.Shows(steak), Is.False);
            steak.Flip();
            Assert.That(FlipGauge.Shows(steak), Is.False, "it doesn't come back");
        }

        [Test]
        public void FoodThatDoesntNeedFlippingHasNoGauge()
        {
            var onion = new CookingSimulation(22f).AddIngredient(new IngredientProperties { TypeId = "DicedOnion", HeatCapacity = 585f, SurfaceFraction = 0.3f });
            Assert.That(FlipGauge.Shows(onion), Is.False);
        }

        [Test]
        public void TheNeedleReadsTheSideFacingDown()
        {
            var (sim, _, pan) = Kitchen();
            var steak = sim.AddIngredient(SteakDefinition.ToProperties());
            pan.Put(steak);
            Assert.That(FlipGauge.Position(steak), Is.EqualTo(0f));
            sim.Tick(1f * Minute);
            var after = FlipGauge.Position(steak);
            Assert.That(after, Is.GreaterThan(0f).And.LessThan(1f));
            sim.Tick(1f * Minute);
            Assert.That(FlipGauge.Position(steak), Is.GreaterThan(after));
        }

        [Test]
        public void AColdPanMovesTheNeedleMoreSlowly()
        {
            var settings = Tuning.FlipGauge;
            var hot = Kitchen();
            var cold = Kitchen(preheatSeconds: 0f);
            var hotSteak = hot.Sim.AddIngredient(SteakDefinition.ToProperties());
            var coldSteak = cold.Sim.AddIngredient(SteakDefinition.ToProperties());
            hot.Pan.Put(hotSteak);
            cold.Pan.Put(coldSteak);

            Assert.That(SecondsUntilBrowning(cold.Sim, coldSteak, settings.GoodFrom),
                Is.GreaterThan(SecondsUntilBrowning(hot.Sim, hotSteak, settings.GoodFrom)));
        }

        [Test]
        public void TheGreenBandIsWideEnoughToHit()
        {
            var settings = Tuning.FlipGauge;
            var (sim, _, pan) = Kitchen();
            var steak = sim.AddIngredient(SteakDefinition.ToProperties());
            pan.Put(steak);
            var reach = SecondsUntilBrowning(sim, steak, settings.GoodFrom);
            var window = SecondsUntilBrowning(sim, steak, settings.GoodTo);

            UnityEngine.Debug.Log($"[FlipGauge] green from {reach}s, lasts {window}s (in-game, 60 = 1 real second)");
            Assert.That(window, Is.GreaterThanOrEqualTo(1f * Minute), "at least a real second to react");
        }

        // Following the gauge shouldn't draw "pale and sad": flip as soon as it's green, then give
        // the second side a real second at full heat.
        [Test]
        public void FlippingOnGreenAvoidsThePaleComplaint()
        {
            var settings = Tuning.FlipGauge;
            var properties = SteakDefinition.ToProperties();
            var order = new Order("Steak", properties.DonenessLevels.First(l => l.Name == "Rare"));
            var (sim, _, pan) = Kitchen();
            var steak = sim.AddIngredient(properties);
            pan.Put(steak);
            SecondsUntilBrowning(sim, steak, settings.GoodFrom);
            steak.Flip();
            sim.Tick(1f * Minute);
            pan.Take();
            var plate = new Plate(3);
            plate.Put(steak);

            var score = DishScorer.Score(plate, Tuning.Quality(Realism.CookingPrecision), order);
            Assert.That(score.Items[0].Crust, Is.GreaterThanOrEqualTo(new ReviewSettings().LowPartScore));
        }

        // The promise of the gauge: flip on green and every order can still come out well, with
        // the second side and the heat deciding the doneness.
        [TestCase("Rare")]
        [TestCase("Medium")]
        [TestCase("Well done")]
        public void FlippingOnGreenWorksForEveryOrder(string doneness)
        {
            var settings = Tuning.FlipGauge;
            var properties = SteakDefinition.ToProperties();
            var order = new Order("Steak", properties.DonenessLevels.First(l => l.Name == doneness));
            var scoring = Tuning.Quality(Realism.CookingPrecision);

            var worstOfBand = 1f;
            foreach (var flipAt in new[] { settings.GoodFrom, (settings.GoodFrom + settings.GoodTo) / 2f, settings.GoodTo })
            {
                var best = 0f;
                var bestAt = string.Empty;
                foreach (var dial in new[] { 1f, 0.8f, 0.6f, 0.45f, 0.3f })
                {
                    for (var second = 30f; second <= 720f; second += 15f)
                    {
                        var (sim, hob, pan) = Kitchen();
                        var steak = sim.AddIngredient(properties);
                        pan.Put(steak);
                        var first = SecondsUntilBrowning(sim, steak, flipAt);
                        steak.Flip();
                        hob.Dial = dial;
                        sim.Tick(second);
                        pan.Take();
                        var plate = new Plate(3);
                        plate.Put(steak);
                        sim.Tick(3f * Minute);
                        var overall = DishScorer.Score(plate, scoring, order).Overall;
                        if (overall > best)
                        {
                            best = overall;
                            bestAt = $"first {first}s, then dial {dial} for {second}s";
                        }
                    }
                }

                UnityEngine.Debug.Log($"[FlipGauge] {doneness}: flip at browning {flipAt:0.00}: best {best:0.0000} ({bestAt})");
                worstOfBand = System.Math.Min(worstOfBand, best);
            }

            Assert.That(worstOfBand, Is.GreaterThanOrEqualTo(0.95f), $"{doneness} anywhere in the green band");
        }

        [Test]
        public void AfterTheFlipTheBarShowsTheStages()
        {
            var (sim, _, pan) = Kitchen();
            var steak = sim.AddIngredient(SteakDefinition.ToProperties());
            pan.Put(steak);
            Assert.That(FlipGauge.ShowsStages(steak), Is.False);
            SecondsUntilBrowning(sim, steak, 0.9f);
            steak.Flip();
            Assert.That(FlipGauge.ShowsStages(steak), Is.True);
            Assert.That(FlipGauge.StageCount(steak.Properties), Is.EqualTo(4), "raw, rare, medium, well done");
        }

        [Test]
        public void TheNeedleIsInTheStageTheFoodIsAt()
        {
            var (sim, hob, pan) = Kitchen();
            var steak = sim.AddIngredient(SteakDefinition.ToProperties());
            pan.Put(steak);
            steak.Flip();
            hob.Dial = 0.4f;

            var last = 0f;
            for (var i = 0; i < 40 * 60; i++)
            {
                sim.Tick(1f);
                var position = FlipGauge.StagePosition(steak);
                Assert.That(position, Is.GreaterThanOrEqualTo(last), "it never goes back");
                last = position;

                var stage = steak.Doneness == null ? 0 : steak.Doneness.Value.Name == "Rare" ? 1 : steak.Doneness.Value.Name == "Medium" ? 2 : 3;
                Assert.That(position, Is.InRange(stage / 4f - 0.001f, (stage + 1) / 4f + 0.001f), $"{steak.PeakCoreTemperature} °C");
            }
            Assert.That(last, Is.EqualTo(1f).Within(0.001f), "it stops at the end");
        }

        [Test]
        public void ItWarnsBeforeASideBurnsAndFlippingClearsIt()
        {
            var settings = Tuning.FlipGauge;
            var (sim, _, pan) = Kitchen();
            var steak = sim.AddIngredient(SteakDefinition.ToProperties());
            pan.Put(steak);
            Assert.That(FlipGauge.IsAboutToBurn(steak, settings), Is.False);

            SecondsUntilBrowning(sim, steak, settings.WarnFrom);
            Assert.That(steak.IsBurnt, Is.False, "there's still time");
            Assert.That(FlipGauge.IsAboutToBurn(steak, settings), Is.True);

            steak.Flip();
            Assert.That(FlipGauge.IsAboutToBurn(steak, settings), Is.False, "that side's out of danger");
        }

        [Test]
        public void CookingRunsSlowerThanTheClock() => Assert.That(Tuning.CookingSpeed, Is.LessThan(1f));

        // The stages come in order even at full heat: a steak left on reaches well done, and there's
        // still time to take it off before it burns.
        [Test]
        public void AtFullHeatASteakReachesWellDoneBeforeItBurns()
        {
            var (sim, _, pan) = Kitchen();
            var steak = sim.AddIngredient(SteakDefinition.ToProperties());
            pan.Put(steak);
            SecondsUntilBrowning(sim, steak, 0.9f);
            steak.Flip();

            var wellDoneAt = -1f;
            var seconds = 0f;
            while (!steak.IsBurnt && seconds < 30f * Minute)
            {
                sim.Tick(1f);
                seconds += 1f;
                if (wellDoneAt < 0f && steak.Doneness?.Name == "Well done")
                    wellDoneAt = seconds;
            }

            Assert.That(wellDoneAt, Is.GreaterThan(0f), "it reached well done");
            Assert.That(seconds - wellDoneAt, Is.GreaterThan(1f * Minute), "with a game minute or more to spare");
        }

        [Test]
        public void OffTheHobTheNeedleSlowsAsThePanCools()
        {
            var (sim, hob, pan) = Kitchen();
            var steak = sim.AddIngredient(SteakDefinition.ToProperties());
            pan.Put(steak);
            SecondsUntilBrowning(sim, steak, 0.9f);
            steak.Flip();
            sim.Tick(1f * Minute);
            hob.RemovePan();

            float Moved(float seconds)
            {
                var from = steak.CoreTemperature;
                sim.Tick(seconds);
                return steak.CoreTemperature - from;
            }

            // Compared by how fast its middle heats, as the peak the needle follows only rises.
            var early = Moved(30f);
            sim.Tick(1f * Minute);
            var late = Moved(30f);
            Assert.That(early, Is.GreaterThan(0f), "it goes on cooking");
            Assert.That(late, Is.LessThan(early), "more and more slowly");
        }
    }
}
