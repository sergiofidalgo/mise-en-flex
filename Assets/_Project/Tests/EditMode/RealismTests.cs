using Game.Data;
using Game.Gameplay.Saving;
using Game.Simulation.Cleaning;
using Game.Simulation.Items;
using Game.Simulation.Realism;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The realism dials and presets (game design §12; decision 2026-11-10): Relaxed / Standard / Chef
    /// over five dials, minigame complexity bending the stick games and hygiene scaling the mess.
    /// </summary>
    public class RealismTests
    {
        private RealismSettings _asset;

        [SetUp]
        public void Create() => _asset = ScriptableObject.CreateInstance<RealismSettings>();

        [TearDown]
        public void Destroy() => Object.DestroyImmediate(_asset);

        // ---- Presets ----

        [Test]
        public void ThePresetsHoldTheUsersValues()
        {
            var presets = _asset.Presets;
            AssertDials(presets.Get(RealismPreset.Relaxed), 0.2f, 0.5f, 1.5f, 0.25f, 0.5f);
            AssertDials(presets.Get(RealismPreset.Standard), 0.5f, 1f, 1f, 0.5f, 1f);
            AssertDials(presets.Get(RealismPreset.Chef), 0.8f, 1.5f, 0.75f, 0.8f, 1.5f);
        }

        [Test]
        public void StandardIsTheTuningAsDesigned()
        {
            Assert.That(_asset.Presets.Identify(_asset.Defaults), Is.EqualTo(RealismPreset.Standard));
        }

        [Test]
        public void EachPresetIsReadBackFromItsDials()
        {
            var presets = _asset.Presets;
            foreach (var preset in new[] { RealismPreset.Relaxed, RealismPreset.Standard, RealismPreset.Chef })
                Assert.That(presets.Identify(presets.Get(preset)), Is.EqualTo(preset));
        }

        [Test]
        public void TurningAnyDialMakesItCustomAndTurningItBackDoesNot()
        {
            var presets = _asset.Presets;
            var dials = presets.Get(RealismPreset.Chef);
            dials.Hygiene = 1.25f;
            Assert.That(presets.Identify(dials), Is.EqualTo(RealismPreset.Custom));
            dials.Hygiene = 1.5f;
            Assert.That(presets.Identify(dials), Is.EqualTo(RealismPreset.Chef));
        }

        [Test]
        public void ChangingAPresetsCopyLeavesThePresetAlone()
        {
            var presets = _asset.Presets;
            presets.Get(RealismPreset.Relaxed).CookingPrecision = 1f;
            Assert.That(presets.Get(RealismPreset.Relaxed).CookingPrecision, Is.EqualTo(0.2f));
        }

        [Test]
        public void StepsGoAlongThePresetsAndStopAtTheEnds()
        {
            Assert.That(RealismPresets.Step(RealismPreset.Standard, 1), Is.EqualTo(RealismPreset.Chef));
            Assert.That(RealismPresets.Step(RealismPreset.Chef, 1), Is.EqualTo(RealismPreset.Chef));
            Assert.That(RealismPresets.Step(RealismPreset.Standard, -1), Is.EqualTo(RealismPreset.Relaxed));
            Assert.That(RealismPresets.Step(RealismPreset.Relaxed, -1), Is.EqualTo(RealismPreset.Relaxed));
            Assert.That(RealismPresets.Step(RealismPreset.Custom, -1), Is.EqualTo(RealismPreset.Relaxed));
            Assert.That(RealismPresets.Step(RealismPreset.Custom, 1), Is.EqualTo(RealismPreset.Chef));
        }

        [Test]
        public void ThePlayersValuesReplaceTheDefaultsForEveryDial()
        {
            _asset.UsePlayerValues(_asset.Presets.Get(RealismPreset.Chef));
            Assert.That(_asset.CookingPrecision, Is.EqualTo(0.8f));
            Assert.That(_asset.SpoilageMultiplier, Is.EqualTo(1.5f));
            Assert.That(_asset.CustomerPatience, Is.EqualTo(0.75f));
            Assert.That(_asset.MinigameComplexity, Is.EqualTo(0.8f));
            Assert.That(_asset.Hygiene, Is.EqualTo(1.5f));
        }

        [Test]
        public void AnOlderSettingsFileGetsTheNewDialsDefaults()
        {
            // Written before minigame complexity and hygiene existed.
            var old = JsonUtility.FromJson<PlayerSettings>("{\"CookingPrecision\":0.3,\"Spoilage\":2.0,\"Patience\":1.25,\"Language\":\"es\"}");
            old.FillMissing(_asset.Defaults);
            AssertDials(old.Dials, 0.3f, 2f, 1.25f, 0.5f, 1f);
            Assert.That(old.Language, Is.EqualTo("es"));
        }

        // ---- Minigame complexity ----

        [Test]
        public void TheMiddleOfTheDialLeavesTheGamesAsTuned()
        {
            var wash = new WashGameSettings();
            var scaled = MinigameComplexity.Scale(wash, 0.5f, _asset.Complexity);
            Assert.That(scaled.GoodRadius, Is.EqualTo(wash.GoodRadius).Within(1e-5f));
            Assert.That(scaled.OkRadius, Is.EqualTo(wash.OkRadius).Within(1e-5f));
            Assert.That(scaled.Instability, Is.EqualTo(wash.Instability).Within(1e-5f));
            Assert.That(scaled.Corrections, Is.EqualTo(wash.Corrections));
            Assert.That(scaled.StartRadius, Is.EqualTo(wash.StartRadius).Within(1e-5f));

            var trace = new TraceGameSettings();
            var traced = MinigameComplexity.Scale(trace, 0.5f, _asset.Complexity);
            Assert.That(traced.MinPush, Is.EqualTo(trace.MinPush).Within(1e-5f));
            Assert.That(traced.SweepEdge, Is.EqualTo(trace.SweepEdge).Within(1e-5f));
            Assert.That(traced.CircleSpeed, Is.EqualTo(trace.CircleSpeed).Within(1e-5f));
        }

        [Test]
        public void LowerComplexityMakesTheSinkMoreForgiving()
        {
            var wash = new WashGameSettings();
            var relaxed = MinigameComplexity.Scale(wash, 0.25f, _asset.Complexity);
            var chef = MinigameComplexity.Scale(wash, 0.8f, _asset.Complexity);

            Assert.That(relaxed.GoodRadius, Is.GreaterThan(wash.GoodRadius));
            Assert.That(chef.GoodRadius, Is.LessThan(wash.GoodRadius));
            Assert.That(relaxed.Instability, Is.LessThan(wash.Instability));
            Assert.That(chef.Instability, Is.GreaterThan(wash.Instability));
            Assert.That(relaxed.Corrections, Is.EqualTo(1), "Relaxed: 2 centrings a plate");
            Assert.That(chef.Corrections, Is.EqualTo(3), "Chef: 4 centrings a plate");
        }

        [Test]
        public void AtEveryComplexityDoingNothingNeverCleansAPlate()
        {
            // The nudge must stay below Instability × OkRadius (WashGameSettings.Nudge), and each plate
            // must start outside the OK ring.
            for (var c = 0f; c <= 1.0001f; c += 0.05f)
            {
                var wash = MinigameComplexity.Scale(new WashGameSettings(), c, _asset.Complexity);
                Assert.That(wash.Nudge, Is.LessThan(wash.Instability * wash.OkRadius), $"complexity {c}");
                Assert.That(wash.StartRadius, Is.GreaterThan(wash.OkRadius), $"complexity {c}");
                Assert.That(wash.GoodRadius, Is.LessThanOrEqualTo(wash.OkRadius), $"complexity {c}");
                Assert.That(wash.Corrections, Is.GreaterThanOrEqualTo(0), $"complexity {c}");
            }
        }

        [Test]
        public void LowerComplexityCleansFasterWithTheSameHand()
        {
            Assert.That(LoopsIn(seconds: 3f, complexity: 0.25f), Is.GreaterThan(LoopsIn(3f, 0.5f)));
            Assert.That(LoopsIn(3f, 0.5f), Is.GreaterThan(LoopsIn(3f, 0.8f)));
        }

        [Test]
        public void LowerComplexityCountsALighterPushAndShorterSweeps()
        {
            var trace = new TraceGameSettings();
            var relaxed = MinigameComplexity.Scale(trace, 0.25f, _asset.Complexity);
            var chef = MinigameComplexity.Scale(trace, 0.8f, _asset.Complexity);
            Assert.That(relaxed.MinPush, Is.LessThan(trace.MinPush));
            Assert.That(chef.MinPush, Is.GreaterThan(trace.MinPush));
            Assert.That(relaxed.SweepEdge, Is.LessThan(trace.SweepEdge));
            Assert.That(chef.SweepEdge, Is.GreaterThan(trace.SweepEdge));
        }

        // A hand spinning the stick faster than any setting counts: what's counted is the game's cap.
        private float LoopsIn(float seconds, float complexity)
        {
            var settings = MinigameComplexity.Scale(new TraceGameSettings(), complexity, _asset.Complexity);
            var game = new TraceGame(TraceShape.Circle, settings);
            const float Step = 1f / 60f;
            for (var t = 0f; t < seconds; t += Step)
                game.Tick(Step, TraceGame.PointAt(TraceShape.Circle, t * 2.5f));
            return game.Loops;
        }

        // ---- Hygiene ----

        [Test]
        public void HygieneScalesHowMuchEachMessLeaves()
        {
            var settings = new DirtSettings();
            var dirt = new DirtMap(settings);
            var mess = new MessMaker(dirt) { Hygiene = 0.5f };

            Assert.That(mess.PlateBroken(0f, 0f).Amount, Is.EqualTo(settings.PlateSpill * 0.5f).Within(1e-5f));
            Assert.That(mess.PanBurnt(null, 5f, 0f).Amount, Is.EqualTo(settings.BurntScorch * 0.5f).Within(1e-5f));
            Assert.That(mess.FireOut(-5f, 0f).Amount, Is.EqualTo(settings.FireFoam * 0.5f).Within(1e-5f));

            mess.Hygiene = 1.5f;
            Assert.That(mess.DinerFinished(DirtSurface.Table, new object(), 10f, 0f).Amount, Is.EqualTo(settings.DinerSmudge * 1.5f).Within(1e-5f));
        }

        [Test]
        public void HygieneZeroLeavesNoDirt()
        {
            var dirt = new DirtMap(new DirtSettings());
            var mess = new MessMaker(dirt) { Hygiene = 0f };
            Assert.That(mess.PlateBroken(0f, 0f), Is.Null);
            Assert.That(dirt.Spots, Is.Empty);
        }

        [Test]
        public void HygieneDoesNotChangeHowOftenGuestsScuffTheFloor()
        {
            var settings = new DirtSettings();
            var mess = new MessMaker(new DirtMap(settings)) { Hygiene = 1.5f };
            for (var i = 1; i < settings.GuestsPerScuff; i++)
                Assert.That(mess.GuestWalked(i * 2f, 0f), Is.Null);
            var scuff = mess.GuestWalked(0f, 0f);
            Assert.That(scuff, Is.Not.Null);
            Assert.That(scuff.Amount, Is.EqualTo(settings.Scuff * 1.5f).Within(1e-5f));
        }

        private static void AssertDials(RealismDials dials, float precision, float spoilage, float patience, float complexity, float hygiene)
        {
            Assert.That(dials.CookingPrecision, Is.EqualTo(precision).Within(1e-5f), "cooking precision");
            Assert.That(dials.Spoilage, Is.EqualTo(spoilage).Within(1e-5f), "spoilage");
            Assert.That(dials.Patience, Is.EqualTo(patience).Within(1e-5f), "patience");
            Assert.That(dials.MinigameComplexity, Is.EqualTo(complexity).Within(1e-5f), "minigame complexity");
            Assert.That(dials.Hygiene, Is.EqualTo(hygiene).Within(1e-5f), "hygiene");
        }
    }
}
