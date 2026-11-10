using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Cooking;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Pan fires (decisions 2026-11-07 and 2026-11-10): only gas catches, a few seconds after the
    /// food burns on a lit flame; it burns until put out, and never ruins the hob.
    /// </summary>
    public class FireSafetyTests
    {
        private const float Ambient = 22f;

        private CookingSimulation _sim;
        private FireSafety _fire;

        [SetUp]
        public void CreateKitchen()
        {
            _sim = new CookingSimulation(Ambient);
            _fire = new FireSafety(new FireSettings { IgnitionSeconds = 4f, SweepsToPutOut = 3f });
        }

        private Pan NewPan() => _sim.AddPan(new PanProperties { HeatCapacity = 750f, AirLossConductance = 7.7f, Capacity = 2 });

        private Hob Gas() => _sim.AddHob(new HobProperties { MaxPower = 2000f });

        private Hob Induction() => _sim.AddHob(new HobProperties
        {
            Kind = HeatKind.Induction,
            MaxPower = 6000f,
            StepTemperatures = new[] { 130f, 190f, 250f },
            Response = 75f,
        });

        private Hob GrillZone() => _sim.AddHob(new HobProperties
        {
            Kind = HeatKind.Fixed,
            MaxPower = 6000f,
            FixedTemperature = 260f,
            Response = 75f,
        });

        // Burns quickly, so a few in-game minutes on full heat burn it through.
        private Ingredient Steak() => _sim.AddIngredient(new IngredientProperties
        {
            TypeId = "Steak",
            HeatCapacity = 0.3f * 3500f,
            SurfaceFraction = 0.15f,
            SurfaceCoreConductance = 0.9f,
            PanContactConductance = 6f,
            AirConductance = 0.25f,
            EvaporationConductance = 25f,
            BrowningStartTemperature = 140f,
            BrowningRate = 1f / 2000f,
            BurntBrowning = 2f,
            DonenessLevels = new[] { new DonenessLevel("Rare", 45f) },
            StartTemperature = 4f,
            FreshnessLossPerHourAt20C = 1f / 24f,
        });

        // A hob at full heat with a steak cooked until it's burnt underneath.
        private (Hob Hob, Ingredient Steak) BurntOn(Hob hob)
        {
            var pan = NewPan();
            hob.PlacePan(pan);
            var steak = Steak();
            pan.Put(steak);
            hob.Dial = 1f;
            if (hob.Kind == HeatKind.Induction)
                hob.Step = hob.Steps;
            for (var i = 0; i < 60 && steak.Browning(steak.DownSide) < steak.Properties.BurntBrowning; i++)
                _sim.Tick(60f);
            Assume.That(steak.Browning(steak.DownSide), Is.GreaterThanOrEqualTo(steak.Properties.BurntBrowning), "the steak should have burnt");
            return (hob, steak);
        }

        private void Watch(float seconds, float step = 0.25f)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += step)
                _fire.Tick(_sim.Hobs, step);
        }

        [Test]
        public void OnlyGasCanCatchFire()
        {
            Assert.That(FireSafety.CanCatchFire(Gas()), Is.True);
            Assert.That(FireSafety.CanCatchFire(Induction()), Is.False);
            Assert.That(FireSafety.CanCatchFire(GrillZone()), Is.False);
        }

        [Test]
        public void ABurntPanOnALitGasFlameCatchesAfterTheDelayNotBefore()
        {
            var (hob, _) = BurntOn(Gas());

            Watch(3.5f);
            Assert.That(_fire.IsBurning(hob), Is.False, "the alarm's few seconds of warning");
            Assert.That(_fire.Smouldering(hob), Is.GreaterThan(0.5f));

            Watch(1f);
            Assert.That(_fire.IsBurning(hob), Is.True);
        }

        [Test]
        public void InductionAndTheGrillNeverCatchHoweverLongTheFoodBurns()
        {
            var (induction, _) = BurntOn(Induction());
            var (grill, _) = BurntOn(GrillZone());

            Watch(60f, 1f);

            Assert.That(_fire.IsBurning(induction), Is.False);
            Assert.That(_fire.IsBurning(grill), Is.False);
        }

        [Test]
        public void TurningTheGasOffInTimeStopsIt()
        {
            var (hob, _) = BurntOn(Gas());
            Watch(3f);
            hob.Dial = 0f;
            Watch(10f);
            Assert.That(_fire.IsBurning(hob), Is.False);

            // Lit again, the count starts over.
            hob.Dial = 1f;
            Watch(3f);
            Assert.That(_fire.IsBurning(hob), Is.False);
        }

        [Test]
        public void TakingTheFoodOutInTimeStopsIt()
        {
            var (hob, steak) = BurntOn(Gas());
            Watch(3f);
            hob.Pan.TakeOut(steak);
            Watch(10f);
            Assert.That(_fire.IsBurning(hob), Is.False);
        }

        [Test]
        public void FlippingThePaleSideDownStopsTheCount()
        {
            var (hob, steak) = BurntOn(Gas());
            Watch(3f);
            Assume.That(steak.Browning(steak.DownSide == IngredientSide.A ? IngredientSide.B : IngredientSide.A), Is.LessThan(steak.Properties.BurntBrowning));
            Assert.That(HobControls.TryFlip(hob), Is.True);
            Watch(2f);
            Assert.That(_fire.IsBurning(hob), Is.False);
        }

        [Test]
        public void CatchingFireLosesTheFoodAndSaysWhatWasLost()
        {
            var (hob, steak) = BurntOn(Gas());
            IReadOnlyList<Ingredient> lost = null;
            _fire.Ignited += (_, food) => lost = food;

            Watch(5f);

            Assert.That(lost, Is.EquivalentTo(new[] { steak }));
            Assert.That(hob.Pan.Contents, Is.Empty);
            Assert.That(steak.Pan, Is.Null);
        }

        [Test]
        public void AFireBurnsUntilPutOutAndNeverRuinsTheHob()
        {
            var (hob, _) = BurntOn(Gas());
            Watch(5f);
            hob.Dial = 0f;

            Watch(600f, 5f);
            Assert.That(_fire.IsBurning(hob), Is.True, "turning the gas off doesn't put a pan fire out");

            Assert.That(_fire.Spray(hob, 1f), Is.False);
            Assert.That(_fire.PutOutProgress(hob), Is.EqualTo(1f / 3f).Within(1e-4f));
            Assert.That(_fire.Spray(hob, 2f), Is.True);
            Assert.That(_fire.IsBurning(hob), Is.False);

            // The same hob works again: a new pan of food can burn and catch again.
            var pan = hob.RemovePan();
            _sim.Remove(pan);
            BurntOn(hob);
            Watch(5f);
            Assert.That(_fire.IsBurning(hob), Is.True);
        }

        [Test]
        public void PuttingItOutTurnsTheGasOff()
        {
            var (hob, _) = BurntOn(Gas());
            Watch(5f);
            var putOut = 0;
            _fire.PutOut += _ => putOut++;

            _fire.Spray(hob, 3f);

            Assert.That(hob.Dial, Is.EqualTo(0f));
            Assert.That(putOut, Is.EqualTo(1));
        }

        [Test]
        public void ClosingPutsOutWhateverIsBurning()
        {
            var (burning, _) = BurntOn(Gas());
            Watch(5f);
            var quiet = Gas();

            var were = _fire.PutOutAll();

            Assert.That(were, Is.EquivalentTo(new[] { burning }));
            Assert.That(_fire.AnyBurning, Is.False);
            Assert.That(_fire.IsBurning(quiet), Is.False);
        }

        [Test]
        public void FoodBurningOnTheHeatIsReportedOnceOnAnyHob()
        {
            var reports = new List<Hob>();
            _fire.FoodBurnt += (hob, _) => reports.Add(hob);
            var (gas, _) = BurntOn(Gas());
            var (induction, _) = BurntOn(Induction());

            Watch(2f);
            Watch(2f);

            Assert.That(reports, Is.EquivalentTo(new[] { gas, induction }));
        }

        [Test]
        public void AnEmptyOrUnlitPanNeverCatches()
        {
            var empty = Gas();
            empty.PlacePan(NewPan());
            empty.Dial = 1f;
            var (unlit, _) = BurntOn(Gas());
            unlit.Dial = 0f;

            Watch(30f, 1f);

            Assert.That(_fire.AnyBurning, Is.False);
            Assert.That(_sim.Hobs.Any(_fire.IsBurning), Is.False);
        }
    }
}
