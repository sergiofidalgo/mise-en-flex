using Game.Data;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Quality;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Checks the real tuning assets against the design targets. If a retune breaks one of these
    /// on purpose, update the target here and in docs/decisions.md together.
    /// </summary>
    public class CookingAssetTests
    {
        private const string SteakPath = "Assets/_Project/Data/Ingredients/ING_Steak.asset";
        private const string EquipmentPath = "Assets/_Project/Data/Settings/EquipmentTier_Basic.asset";
        private const string TuningPath = "Assets/_Project/Data/Settings/TuningConfig.asset";
        private const string RealismPath = "Assets/_Project/Data/Settings/RealismSettings.asset";
        private const float Minute = 60f;

        // Food left on the heat burns. Cooked first, then a short window, then ruined.
        [TestCase(1f)]
        [TestCase(0.7f)]
        public void DicedOnionBurnsIfLeftOnTheHeat(float dial)
        {
            var onion = AssetDatabase.LoadAssetAtPath<IngredientDefinition>("Assets/_Project/Data/Ingredients/ING_DicedOnion.asset");
            var equipment = AssetDatabase.LoadAssetAtPath<EquipmentTier>(EquipmentPath);
            var tuning = AssetDatabase.LoadAssetAtPath<TuningConfig>(TuningPath);

            var sim = new CookingSimulation(tuning.AmbientTemperature);
            var hob = sim.AddHob(equipment.Hob);
            var pan = sim.AddPan(equipment.Pan);
            hob.PlacePan(pan);
            hob.Dial = dial;
            sim.Tick(4f * Minute);

            var food = sim.AddIngredient(onion.ToProperties());
            pan.Put(food);
            float? cookedAt = null;
            float? burntAt = null;
            for (var t = 0f; t < 60f * Minute && !burntAt.HasValue; t += 1f)
            {
                sim.Tick(1f);
                if (!cookedAt.HasValue && food.Doneness?.Name == "Cooked")
                    cookedAt = t;
                if (food.IsBurnt)
                    burntAt = t;
            }

            UnityEngine.Debug.Log($"[OnionBurn] dial {dial}: cooked at {cookedAt}s, burnt at {burntAt}s (60 = 1 real second)");
            Assert.That(burntAt, Is.Not.Null, "it burns");
            Assert.That(cookedAt, Is.Not.Null, "it gets cooked first");
            Assert.That(burntAt.Value, Is.GreaterThan(cookedAt.Value + 1f * Minute), "at least a real second to react");
            Assert.That(burntAt.Value, Is.LessThan(cookedAt.Value + 20f * Minute), "it doesn't take forever");
        }

        [Test]
        public void SteakAssetIsRareAtThreeMinutesPerSideInPreheatedPan()
        {
            var steakDefinition = AssetDatabase.LoadAssetAtPath<IngredientDefinition>(SteakPath);
            var equipment = AssetDatabase.LoadAssetAtPath<EquipmentTier>(EquipmentPath);
            var tuning = AssetDatabase.LoadAssetAtPath<TuningConfig>(TuningPath);
            Assert.That(steakDefinition, Is.Not.Null, $"Missing {SteakPath}; run Tools/Game/Build Greybox Kitchen");
            Assert.That(equipment, Is.Not.Null, $"Missing {EquipmentPath}");

            var sim = new CookingSimulation(tuning.AmbientTemperature);
            var hob = sim.AddHob(equipment.Hob);
            var pan = sim.AddPan(equipment.Pan);
            hob.PlacePan(pan);
            hob.Dial = 1f;
            sim.Tick(2.5f * Minute);

            var steak = sim.AddIngredient(steakDefinition.ToProperties());
            pan.Put(steak);
            sim.Tick(3f * Minute);
            steak.Flip();
            sim.Tick(3f * Minute);
            pan.Take();
            sim.Tick(5f * Minute);

            Assert.That(steak.Doneness?.Name, Is.EqualTo("Rare"));
            Assert.That(steak.IsBurnt, Is.False);
        }

        [Test]
        public void SteakAssetCookedWellAndServedPromptlyEarnsFourStars()
        {
            var steakDefinition = AssetDatabase.LoadAssetAtPath<IngredientDefinition>(SteakPath);
            var equipment = AssetDatabase.LoadAssetAtPath<EquipmentTier>(EquipmentPath);
            var tuning = AssetDatabase.LoadAssetAtPath<TuningConfig>(TuningPath);
            var realism = AssetDatabase.LoadAssetAtPath<RealismSettings>(RealismPath);

            var sim = new CookingSimulation(tuning.AmbientTemperature);
            var hob = sim.AddHob(equipment.Hob);
            var pan = sim.AddPan(equipment.Pan);
            hob.PlacePan(pan);
            hob.Dial = 1f;
            sim.Tick(2.5f * Minute);

            var steak = sim.AddIngredient(steakDefinition.ToProperties());
            pan.Put(steak);
            sim.Tick(3f * Minute);
            steak.Flip();
            sim.Tick(3f * Minute);
            pan.Take();
            var plate = new Plate(equipment.PlateCapacity);
            plate.Put(steak);
            sim.Tick(3f * Minute); // rest on the plate, then straight to the pass

            var score = DishScorer.Score(plate, tuning.Quality(realism.CookingPrecision));
            Assert.That(score.Stars, Is.GreaterThanOrEqualTo(4f), $"Scored {score.Overall:P0}");
        }

        [Test]
        public void BasicPanHoldsOnePieceOfFood()
        {
            // Decision 2026-11-02: one piece of food per pan.
            var equipment = AssetDatabase.LoadAssetAtPath<EquipmentTier>(EquipmentPath);
            Assert.That(equipment.Pan.Capacity, Is.EqualTo(1));
        }

        [Test]
        public void TimeScaleIsSixty()
        {
            // Decision 2026-11-02: one real second is one in-game minute.
            var tuning = AssetDatabase.LoadAssetAtPath<TuningConfig>(TuningPath);
            Assert.That(tuning.TimeScale, Is.EqualTo(60f));
        }
    }
}
