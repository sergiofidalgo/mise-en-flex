using Game.Data;
using Game.Simulation.Cooking;
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
        private const float Minute = 60f;

        [Test]
        public void SteakAssetIsMediumRareAtThreeMinutesPerSideInPreheatedPan()
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

            Assert.That(steak.Doneness?.Name, Is.EqualTo("Medium-rare"));
            Assert.That(steak.IsBurnt, Is.False);
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
