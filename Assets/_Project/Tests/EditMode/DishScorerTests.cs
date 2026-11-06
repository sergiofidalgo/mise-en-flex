using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Quality;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class DishScorerTests
    {
        private const float Minute = 60f;

        private CookingSimulation _sim;
        private Hob _hob;
        private Pan _pan;
        private readonly ScoringSettings _settings = new ScoringSettings();

        private static IngredientProperties Steak() => new IngredientProperties
        {
            TypeId = "Steak",
            HeatCapacity = 0.3f * 3500f,
            SurfaceFraction = 0.15f,
            SurfaceCoreConductance = 0.9f,
            PanContactConductance = 6f,
            AirConductance = 0.25f,
            EvaporationConductance = 25f,
            BrowningStartTemperature = 140f,
            BrowningRate = 1f / 12600f,
            BurntBrowning = 2f,
            DonenessLevels = new[] { new DonenessLevel("Rare", 45f), new DonenessLevel("Medium-rare", 52f), new DonenessLevel("Medium", 57f) },
            StartTemperature = 4f,
            FreshnessLossPerHourAt20C = 1f / 24f,
            TargetCoreTemperature = 54.5f,
            ServedHot = true,
            ServingIdealTemperature = 52f,
            ServingColdTemperature = 38f,
            ScoresBrowning = true,
        };

        [SetUp]
        public void CreateKitchen()
        {
            _sim = new CookingSimulation(22f);
            _hob = _sim.AddHob(new HobProperties { MaxPower = 2000f });
            _pan = _sim.AddPan(new PanProperties { HeatCapacity = 750f, AirLossConductance = 7.7f, Capacity = 1 });
            _hob.PlacePan(_pan);
            _hob.Dial = 1f;
            _sim.Tick(2.5f * Minute);
        }

        private Plate CookAndPlate(float secondsPerSide, float restSeconds)
        {
            var steak = _sim.AddIngredient(Steak());
            _pan.Put(steak);
            _sim.Tick(secondsPerSide);
            steak.Flip();
            _sim.Tick(secondsPerSide);
            _pan.Take();
            var plate = new Plate(3);
            plate.Put(steak);
            _sim.Tick(restSeconds);
            return plate;
        }

        [Test]
        public void WellCookedSteakServedPromptlyScoresHigh()
        {
            var score = DishScorer.Score(CookAndPlate(3f * Minute, 3f * Minute), _settings);
            Assert.That(score.Overall, Is.GreaterThan(0.85f));
            Assert.That(score.Stars, Is.GreaterThanOrEqualTo(4f));
        }

        [Test]
        public void OvercookedSteakLosesDonenessPoints()
        {
            var item = DishScorer.Score(CookAndPlate(6f * Minute, 3f * Minute), _settings).Items[0];
            Assert.That(item.Doneness, Is.LessThan(0.3f));
        }

        [Test]
        public void SteakLeftWaitingGoesColdAndLosesTemperaturePoints()
        {
            var prompt = DishScorer.Score(CookAndPlate(3f * Minute, 3f * Minute), _settings).Items[0];

            CreateKitchen();
            var late = DishScorer.Score(CookAndPlate(3f * Minute, 90f * Minute), _settings).Items[0];

            Assert.That(late.Temperature, Is.LessThan(prompt.Temperature - 0.5f));
            Assert.That(late.Overall, Is.LessThan(prompt.Overall));
        }

        [Test]
        public void BurntSteakScoresNoCrust()
        {
            var steak = _sim.AddIngredient(Steak());
            _pan.Put(steak);
            _sim.Tick(9f * Minute); // forgotten on one side
            _pan.Take();
            var plate = new Plate(3);
            plate.Put(steak);

            Assert.That(DishScorer.Score(plate, _settings).Items[0].Crust, Is.EqualTo(0f));
        }

        [Test]
        public void PaleSteakFromALowHeatScoresLittleCrust()
        {
            _hob.Dial = 0.4f;
            _sim.Tick(10f * Minute); // let the pan settle at a low temperature
            var item = DishScorer.Score(CookAndPlate(4f * Minute, 3f * Minute), _settings).Items[0];
            Assert.That(item.Crust, Is.LessThan(0.2f));
        }

        [Test]
        public void TighterToleranceIsStricter()
        {
            var plate = CookAndPlate(3.5f * Minute, 4f * Minute); // a little past medium-rare
            var generous = DishScorer.Score(plate, new ScoringSettings { DonenessTolerance = 5f }).Items[0];
            var strict = DishScorer.Score(plate, new ScoringSettings { DonenessTolerance = 0.5f }).Items[0];
            Assert.That(strict.Doneness, Is.LessThan(generous.Doneness));
        }

        [Test]
        public void PartsThatDontApplyAreLeftOut()
        {
            var tomato = _sim.AddIngredient(new IngredientProperties
            {
                TypeId = "Tomato", HeatCapacity = 470f, SurfaceFraction = 0.15f, StartTemperature = 20f,
            });
            var item = DishScorer.ScoreItem(tomato, _settings);

            Assert.That(item.Doneness, Is.Null);
            Assert.That(item.Crust, Is.Null);
            Assert.That(item.Temperature, Is.Null);
            Assert.That(item.Overall, Is.EqualTo(1f).Within(0.001f), "A fresh raw tomato loses nothing");
        }

        [Test]
        public void DishScoreAveragesItsItems()
        {
            var plate = CookAndPlate(3f * Minute, 3f * Minute);
            plate.Put(_sim.AddIngredient(new IngredientProperties { TypeId = "Tomato", HeatCapacity = 470f, SurfaceFraction = 0.15f, StartTemperature = 20f }));
            var score = DishScorer.Score(plate, _settings);
            Assert.That(score.Overall, Is.EqualTo((score.Items[0].Overall + score.Items[1].Overall) / 2f).Within(0.001f));
        }

        [Test]
        public void StarsAreHalfSteps()
        {
            Assert.That(new DishScore(System.Array.Empty<ItemScore>(), 0.83f).Stars, Is.EqualTo(4f));
            Assert.That(new DishScore(System.Array.Empty<ItemScore>(), 0.86f).Stars, Is.EqualTo(4.5f));
        }

        private Ingredient SearOneSide()
        {
            var steak = _sim.AddIngredient(Steak());
            _pan.Put(steak);
            while (steak.Browning(steak.DownSide) < 0.9f)
                _sim.Tick(1f);
            steak.Flip();
            return steak;
        }

        private float CrustOf(Ingredient steak)
        {
            _pan.Take();
            var plate = new Plate(3);
            plate.Put(steak);
            return DishScorer.Score(plate, _settings).Items[0].Crust.Value;
        }

        [Test]
        public void SearingOneSideAndFinishingGentlyGetsFullCrust()
        {
            var steak = SearOneSide();
            _hob.Dial = 0.3f;
            _sim.Tick(2f * Minute);
            Assert.That(steak.Browning(steak.DownSide), Is.LessThan(0.5f), "the second side stayed pale");
            Assert.That(CrustOf(steak), Is.EqualTo(1f));
        }

        [Test]
        public void ASideCloseToBurntLosesPointsHoweverGoodTheOther()
        {
            var steak = SearOneSide();
            while (steak.Browning(steak.DownSide) < 1.85f)
                _sim.Tick(1f);
            Assert.That(steak.IsBurnt, Is.False);
            Assert.That(CrustOf(steak), Is.LessThan(0.4f));
        }
    }
}
