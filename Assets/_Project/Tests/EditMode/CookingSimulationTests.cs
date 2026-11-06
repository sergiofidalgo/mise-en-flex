using Game.Simulation.Cooking;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Behaviour of the heat model. Times are in-game seconds (at 60x, one real second is one
    /// in-game minute). Ranges, not exact values, so tuning the constants doesn't break the tests;
    /// they pin down how cooking should feel, not the numbers behind it.
    /// </summary>
    public class CookingSimulationTests
    {
        private const float Ambient = 22f;
        private const float FridgeTemperature = 4f;
        private const float Minute = 60f;

        private CookingSimulation _sim;
        private Hob _hob;
        private Pan _pan;

        // A 300 g, 2.5 cm steak. Values match the ones prototyped for the IngredientDefinition defaults.
        private static IngredientProperties Steak(float startTemperature = FridgeTemperature) => new IngredientProperties
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
            DonenessLevels = new[]
            {
                new DonenessLevel("Rare", 45f),
                new DonenessLevel("Medium-rare", 52f),
                new DonenessLevel("Medium", 57f),
                new DonenessLevel("Medium-well", 63f),
                new DonenessLevel("Well done", 68f),
            },
            StartTemperature = startTemperature,
            FreshnessLossPerHourAt20C = 1f / 24f,
        };

        [SetUp]
        public void CreateKitchen()
        {
            _sim = new CookingSimulation(Ambient);
            _hob = _sim.AddHob(new HobProperties { MaxPower = 2000f });
            _pan = _sim.AddPan(new PanProperties { HeatCapacity = 750f, AirLossConductance = 7.7f, Capacity = 2 });
            _hob.PlacePan(_pan);
        }

        private void Preheat(float dial = 1f, float seconds = 2.5f * Minute)
        {
            _hob.Dial = dial;
            _sim.Tick(seconds);
        }

        // Sears side A, flips, sears side B, takes it out of the pan and rests it.
        private Ingredient CookSteak(float secondsPerSide, float restSeconds = 5f * Minute, float startTemperature = FridgeTemperature)
        {
            var steak = _sim.AddIngredient(Steak(startTemperature));
            _pan.Put(steak);
            _sim.Tick(secondsPerSide);
            steak.Flip();
            _sim.Tick(secondsPerSide);
            _pan.Take();
            _sim.Tick(restSeconds);
            return steak;
        }

        [Test]
        public void FullHeatPreheatsPanToSearingInAboutTwoMinutes()
        {
            Preheat(seconds: 2.5f * Minute);
            Assert.That(_pan.Temperature, Is.InRange(190f, 260f));
        }

        [Test]
        public void PanLevelsOffInsteadOfHeatingForever()
        {
            Preheat(seconds: 60f * Minute);
            Assert.That(_pan.Temperature, Is.InRange(250f, 300f));
        }

        [Test]
        public void MoreDialMeansHotterPan()
        {
            Preheat(dial: 0.5f, seconds: 10f * Minute);
            var half = _pan.Temperature;
            Preheat(dial: 1f, seconds: 10f * Minute);
            Assert.That(_pan.Temperature, Is.GreaterThan(half + 50f));
        }

        [Test]
        public void PanCoolsTowardsRoomWhenHobIsOff()
        {
            Preheat();
            Preheat(dial: 0f, seconds: 60f * Minute);
            Assert.That(_pan.Temperature, Is.InRange(Ambient, Ambient + 5f));
        }

        [Test]
        public void AddingColdSteakDropsPanTemperature()
        {
            Preheat();
            var before = _pan.Temperature;
            _pan.Put(_sim.AddIngredient(Steak()));
            _sim.Tick(1f * Minute);
            Assert.That(_pan.Temperature, Is.LessThan(before - 5f));
        }

        [Test]
        public void ThreeMinutesPerSideThenRestIsMediumRare()
        {
            Preheat();
            var steak = CookSteak(secondsPerSide: 3f * Minute);
            Assert.That(steak.Doneness?.Name, Is.EqualTo("Medium-rare"));
        }

        [Test]
        public void FiveMinutesPerSideIsMediumWellOrMore()
        {
            Preheat();
            var steak = CookSteak(secondsPerSide: 5f * Minute);
            Assert.That(steak.PeakCoreTemperature, Is.GreaterThanOrEqualTo(63f));
        }

        [Test]
        public void CoreLagsBehindTheSearingSurface()
        {
            Preheat();
            var steak = _sim.AddIngredient(Steak());
            _pan.Put(steak);
            _sim.Tick(2f * Minute);
            Assert.That(steak.CoreTemperature, Is.LessThan(steak.SurfaceTemperature(steak.DownSide) - 20f));
        }

        [Test]
        public void CarryoverRaisesCoreWhileResting()
        {
            Preheat();
            var steak = _sim.AddIngredient(Steak());
            _pan.Put(steak);
            _sim.Tick(3.5f * Minute);
            steak.Flip();
            _sim.Tick(3.5f * Minute);
            _pan.Take();
            var coreWhenPulled = steak.CoreTemperature;

            _sim.Tick(5f * Minute);

            Assert.That(steak.PeakCoreTemperature, Is.GreaterThan(coreWhenPulled + 2f));
        }

        [Test]
        public void OnlyTheSideInThePanBrowns()
        {
            Preheat();
            var steak = _sim.AddIngredient(Steak());
            _pan.Put(steak);
            _sim.Tick(3f * Minute);
            Assert.That(steak.Browning(IngredientSide.A), Is.GreaterThan(0.6f));
            Assert.That(steak.Browning(IngredientSide.B), Is.LessThan(0.05f));
        }

        [Test]
        public void ColdPanSearsWorseThanPreheatedPan()
        {
            _hob.Dial = 1f; // full heat, but the steak goes in straight away
            var cold = CookSteak(secondsPerSide: 3f * Minute);

            CreateKitchen();
            Preheat();
            var preheated = CookSteak(secondsPerSide: 3f * Minute);

            Assert.That(cold.Browning(IngredientSide.A), Is.LessThan(preheated.Browning(IngredientSide.A) * 0.8f));
        }

        [Test]
        public void LowHeatCooksWithoutBrowning()
        {
            Preheat(dial: 0.5f);
            var steak = CookSteak(secondsPerSide: 4f * Minute);
            Assert.That(steak.Browning(IngredientSide.A), Is.LessThan(0.05f));
        }

        [Test]
        public void ForgettingASteakBurnsIt()
        {
            Preheat();
            var steak = _sim.AddIngredient(Steak());
            _pan.Put(steak);
            _sim.Tick(8f * Minute);
            Assert.That(steak.IsBurnt);
        }

        [Test]
        public void RoomTemperatureSteakCooksFasterThanFridgeCold()
        {
            Preheat();
            var fromFridge = CookSteak(secondsPerSide: 3f * Minute, startTemperature: FridgeTemperature);

            CreateKitchen();
            Preheat();
            var tempered = CookSteak(secondsPerSide: 3f * Minute, startTemperature: Ambient);

            Assert.That(tempered.PeakCoreTemperature, Is.GreaterThan(fromFridge.PeakCoreTemperature + 3f));
        }

        [Test]
        public void ResultBarelyDependsOnFrameSize()
        {
            Preheat();
            var steak = _sim.AddIngredient(Steak());
            _pan.Put(steak);
            for (var i = 0; i < 1800; i++)
                _sim.Tick(0.1f); // 3 minutes in small frames
            var smallFrames = steak.CoreTemperature;

            CreateKitchen();
            Preheat();
            var other = _sim.AddIngredient(Steak());
            _pan.Put(other);
            for (var i = 0; i < 90; i++)
                _sim.Tick(2f); // 3 minutes in big frames (a 30 fps frame at 60x)

            Assert.That(other.CoreTemperature, Is.EqualTo(smallFrames).Within(0.5f));
        }

        [Test]
        public void RawSteakHasNoDonenessYet()
        {
            Assert.That(_sim.AddIngredient(Steak()).Doneness, Is.Null);
        }

        [Test]
        public void FridgeColdFoodStaysFreshLonger()
        {
            var cold = _sim.AddIngredient(Steak(FridgeTemperature));
            _sim.AmbientTemperature = FridgeTemperature;
            _sim.Tick(6f * 60f * Minute);

            CreateKitchen();
            var warm = _sim.AddIngredient(Steak(Ambient));
            _sim.Tick(6f * 60f * Minute);

            Assert.That(cold.Freshness, Is.GreaterThan(warm.Freshness));
            Assert.That(warm.Freshness, Is.LessThan(0.95f));
        }

        [Test]
        public void SpoilageDialAtZeroStopsSpoiling()
        {
            _sim.SpoilageMultiplier = 0f;
            var steak = _sim.AddIngredient(Steak(Ambient));
            _sim.Tick(12f * 60f * Minute);
            Assert.That(steak.Freshness, Is.EqualTo(1f));
        }

        [Test]
        public void PanRefusesIngredientsBeyondCapacity()
        {
            _pan.Put(_sim.AddIngredient(Steak()));
            _pan.Put(_sim.AddIngredient(Steak()));
            Assert.That(_pan.CanAccept(_sim.AddIngredient(Steak())), Is.False);
        }

        [Test]
        public void FlipSwapsWhichSideFacesDown()
        {
            var steak = _sim.AddIngredient(Steak());
            steak.Flip();
            Assert.That(steak.DownSide, Is.EqualTo(IngredientSide.B));
        }

        private Ingredient HalfCookedFromAHotPan()
        {
            _hob.Dial = 1f;
            _sim.Tick(2.5f * Minute);
            var steak = _sim.AddIngredient(Steak());
            _pan.Put(steak);
            _sim.Tick(3f * Minute);
            return steak;
        }

        [Test]
        public void InAPanOffTheHeatFoodGoesOnCooking()
        {
            var steak = HalfCookedFromAHotPan();
            _hob.RemovePan();
            var before = steak.PeakCoreTemperature;
            _sim.Tick(1f * Minute);
            Assert.That(steak.PeakCoreTemperature, Is.GreaterThan(before + 1f));
        }

        [Test]
        public void OnAPlateFoodStopsCookingAndCools()
        {
            var steak = HalfCookedFromAHotPan();
            var pan = _hob.RemovePan();
            pan.Take();
            var plate = new Game.Simulation.Items.Plate(3);
            plate.Put(steak);
            var before = steak.PeakCoreTemperature;
            var average = steak.AverageTemperature;
            _sim.Tick(5f * Minute);

            Assert.That(steak.PeakCoreTemperature, Is.EqualTo(before));
            Assert.That(steak.AverageTemperature, Is.LessThan(average));
        }

        [Test]
        public void TakenOffThePlateItCanCookAgain()
        {
            var steak = HalfCookedFromAHotPan();
            _pan.Take();
            var plate = new Game.Simulation.Items.Plate(3);
            plate.Put(steak);
            plate.Take();
            _pan.Put(steak);
            var before = steak.PeakCoreTemperature;
            _sim.Tick(1f * Minute);
            Assert.That(steak.PeakCoreTemperature, Is.GreaterThan(before));
        }
    }
}
