using Game.Simulation.Cooking;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The kinds of heat (decision 2026-11-07): gas takes its time, induction jumps to each step,
    /// a grill zone is always at its own temperature. Times are in-game seconds (at 60x, one real
    /// second is one in-game minute). Ranges, so tuning doesn't break them.
    /// </summary>
    public class HeatKindTests
    {
        private const float Ambient = 22f;
        private const float Minute = 60f;
        private static readonly float[] InductionSteps = { 130f, 190f, 250f };

        private CookingSimulation _sim;

        [SetUp]
        public void CreateKitchen() => _sim = new CookingSimulation(Ambient);

        private Pan NewPan() => _sim.AddPan(new PanProperties { HeatCapacity = 750f, AirLossConductance = 7.7f, Capacity = 1 });

        private Hob Gas() => _sim.AddHob(new HobProperties { MaxPower = 2000f });

        private Hob Induction() => _sim.AddHob(new HobProperties
        {
            Kind = HeatKind.Induction,
            MaxPower = 6000f,
            StepTemperatures = InductionSteps,
            Response = 75f,
        });

        private Hob Zone(float temperature) => _sim.AddHob(new HobProperties
        {
            Kind = HeatKind.Fixed,
            MaxPower = 6000f,
            FixedTemperature = temperature,
            Response = 75f,
        });

        private Pan Grate(float temperature) => _sim.AddPan(new PanProperties
        {
            TypeId = "Grate",
            HeatCapacity = 750f,
            AirLossConductance = 7.7f,
            Capacity = 2,
            FixedTemperature = temperature,
        });

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
            BrowningRate = 1f / 12600f,
            BurntBrowning = 2f,
            DonenessLevels = new[] { new DonenessLevel("Rare", 45f), new DonenessLevel("Well done", 68f) },
            StartTemperature = 4f,
            FreshnessLossPerHourAt20C = 1f / 24f,
        });

        private static Pan On(Hob hob, Pan pan)
        {
            hob.PlacePan(pan);
            return pan;
        }

        [Test]
        public void InductionReachesItsTopStepFarSoonerThanGasAtFullHeat()
        {
            var gas = Gas();
            var gasPan = On(gas, NewPan());
            gas.Dial = 1f;
            var induction = Induction();
            var inductionPan = On(induction, NewPan());
            induction.Step = 3;

            _sim.Tick(45f);

            Assert.That(inductionPan.Temperature, Is.InRange(235f, 255f), "induction is there");
            Assert.That(gasPan.Temperature, Is.LessThan(150f), "gas is still on its way");
        }

        [Test]
        public void InductionSettlesOnEachStepAndHigherStepsAreHotter()
        {
            var induction = Induction();
            var pan = On(induction, NewPan());

            for (var step = 1; step <= InductionSteps.Length; step++)
            {
                induction.Step = step;
                _sim.Tick(2f * Minute);
                Assert.That(pan.Temperature, Is.EqualTo(InductionSteps[step - 1]).Within(3f), $"step {step}");
            }
        }

        [Test]
        public void StepDownOnInductionCoolsThePanAlmostAtOnce()
        {
            var induction = Induction();
            var pan = On(induction, NewPan());
            induction.Step = 3;
            _sim.Tick(2f * Minute);

            induction.Step = 1;
            _sim.Tick(45f);

            Assert.That(pan.Temperature, Is.InRange(125f, 145f));
        }

        [Test]
        public void InductionOffLetsThePanCoolOnItsOwnLikeGasOff()
        {
            // Both heated the same way, then one moved to an induction hob that's off.
            var gas = Gas();
            var gasPan = On(gas, NewPan());
            var heater = Gas();
            var inductionPan = On(heater, NewPan());
            gas.Dial = 1f;
            heater.Dial = 1f;
            _sim.Tick(10f * Minute);
            gas.Dial = 0f;
            heater.RemovePan();
            On(Induction(), inductionPan);

            _sim.Tick(Minute);

            Assert.That(inductionPan.Temperature, Is.EqualTo(gasPan.Temperature).Within(0.5f));
        }

        [Test]
        public void InductionHoldsItsStepWhenAColdSteakGoesIn()
        {
            var gas = Gas();
            var gasPan = On(gas, NewPan());
            gas.Dial = 1f;
            var induction = Induction();
            var inductionPan = On(induction, NewPan());
            induction.Step = 3;
            _sim.Tick(10f * Minute);
            var gasBefore = gasPan.Temperature;
            var inductionBefore = inductionPan.Temperature;

            gasPan.Put(Steak());
            inductionPan.Put(Steak());
            _sim.Tick(Minute);

            var gasDrop = gasBefore - gasPan.Temperature;
            var inductionDrop = inductionBefore - inductionPan.Temperature;
            Assert.That(gasDrop, Is.GreaterThan(10f), "gas sags under a cold steak");
            Assert.That(inductionDrop, Is.LessThan(gasDrop / 3f));
        }

        [Test]
        public void StepsChangeOnePressAtATimeAndStopAtTheEnds()
        {
            var induction = Induction();

            Assert.That(HobControls.ChangeStep(induction, -1), Is.False, "already off");
            Assert.That(HobControls.ChangeStep(induction, +1), Is.True);
            HobControls.ChangeStep(induction, +1);
            HobControls.ChangeStep(induction, +1);
            Assert.That(HobControls.ChangeStep(induction, +1), Is.False, "already at the top");
            Assert.That(induction.Step, Is.EqualTo(3));
            Assert.That(induction.Dial, Is.EqualTo(1f));
            Assert.That(HobControls.ChangeStep(Gas(), +1), Is.False, "gas has a dial, not steps");
        }

        [Test]
        public void ClosingTheKitchenTurnsInductionOff()
        {
            var induction = Induction();
            induction.Step = 2;

            _sim.SkipClosedTime(8f * 60f * Minute);

            Assert.That(induction.Step, Is.EqualTo(0));
        }

        [Test]
        public void APanOnAGrillZoneSettlesAtTheZonesTemperature()
        {
            var pan = On(Zone(160f), NewPan());

            _sim.Tick(3f * Minute);

            Assert.That(pan.Temperature, Is.EqualTo(160f).Within(3f));
        }

        [Test]
        public void AGrateKeepsItsTemperatureHoweverManySteaksAreOnIt()
        {
            var grate = Grate(260f);
            grate.Put(Steak());
            grate.Put(Steak());

            _sim.Tick(5f * Minute);

            Assert.That(grate.Temperature, Is.EqualTo(260f));
            Assert.That(grate.CanAccept(Steak()), Is.False, "room for two");
        }

        [Test]
        public void TheHotZoneSearsWhereTheCoolZoneBarelyBrowns()
        {
            var hot = Grate(260f);
            var cool = Grate(150f);
            var seared = Steak();
            var pale = Steak();
            hot.Put(seared);
            cool.Put(pale);

            _sim.Tick(2f * Minute);

            Assert.That(seared.Browning(seared.DownSide), Is.GreaterThan(pale.Browning(pale.DownSide) * 4f));
        }

        [Test]
        public void ClosingTheKitchenLeavesTheGrillLit()
        {
            var grate = Grate(260f);

            _sim.SkipClosedTime(8f * 60f * Minute);

            Assert.That(grate.Temperature, Is.EqualTo(260f));
        }

        [Test]
        public void TheMeterCountsHeatPutIntoPans()
        {
            var gas = Gas();
            On(gas, NewPan());
            gas.Dial = 0.5f;
            var idle = Gas();
            var induction = Induction();
            On(induction, NewPan());
            induction.Step = 2;
            var zone = Zone(260f);
            On(zone, NewPan());

            _sim.Tick(Minute);

            Assert.That(gas.EnergyUsed, Is.EqualTo(1000f * Minute).Within(1f), "half of 2 kW for a minute");
            Assert.That(idle.EnergyUsed, Is.Zero, "no pan, no heat");
            Assert.That(induction.EnergyUsed, Is.GreaterThan(0f));
            Assert.That(zone.EnergyUsed, Is.Zero, "a grill is paid by the day");
        }
    }
}
