using Game.Simulation.Cooking;
using Game.Simulation.Days;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Energy as a daily expense (decision 2026-11-07): hobs are billed for the heat they used since
    /// the last reading, electricity for less than gas; each grill costs a flat amount a day.
    /// </summary>
    public class EnergyTests
    {
        private const float Hour = 3600f;
        private static readonly EnergyPrices Prices = new EnergyPrices { Electricity = 0.6f, Gas = 1f, CharcoalPerGrill = 15f };

        private CookingSimulation _sim;
        private EnergyMeter _meter;

        [SetUp]
        public void CreateKitchen()
        {
            _sim = new CookingSimulation(22f);
            _meter = new EnergyMeter();
        }

        private Pan NewPan() => _sim.AddPan(new PanProperties { HeatCapacity = 750f, AirLossConductance = 7.7f, Capacity = 1 });

        [Test]
        public void AGasHobIsBilledForTheHeatItUsed()
        {
            var hob = _sim.AddHob(new HobProperties { MaxPower = 2000f });
            hob.PlacePan(NewPan());
            hob.Dial = 1f;

            _sim.Tick(Hour);
            var bill = _meter.Read(_sim.Hobs, 0, Prices);

            Assert.That(bill.Gas, Is.EqualTo(2f).Within(0.01f), "2 kWh at 1 a kWh");
            Assert.That(bill.Electricity, Is.Zero);
        }

        [Test]
        public void EachReadingBillsOnlyWhatsNewSinceTheLast()
        {
            var hob = _sim.AddHob(new HobProperties { MaxPower = 2000f });
            hob.PlacePan(NewPan());
            hob.Dial = 1f;
            _sim.Tick(Hour);
            _meter.Read(_sim.Hobs, 0, Prices);

            hob.Dial = 0.5f;
            _sim.Tick(Hour);

            Assert.That(_meter.Read(_sim.Hobs, 0, Prices).Gas, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void AHobLeftOffCostsNothing()
        {
            var hob = _sim.AddHob(new HobProperties { MaxPower = 2000f });
            hob.PlacePan(NewPan());

            _sim.Tick(Hour);

            Assert.That(_meter.Read(_sim.Hobs, 0, Prices).Total, Is.Zero);
        }

        [Test]
        public void InductionIsBilledAsElectricityAtItsLowerPrice()
        {
            var hob = _sim.AddHob(new HobProperties { Kind = HeatKind.Induction, MaxPower = 6000f, StepTemperatures = new[] { 130f, 190f, 250f }, Response = 75f });
            hob.PlacePan(NewPan());
            hob.Step = 3;

            _sim.Tick(Hour);
            var bill = _meter.Read(_sim.Hobs, 0, Prices);

            Assert.That(bill.Electricity, Is.GreaterThan(0f));
            Assert.That(bill.Gas, Is.Zero);
            var kwh = hob.EnergyUsed / 3.6e6;
            Assert.That(bill.Electricity, Is.EqualTo((float)(kwh * Prices.Electricity)).Within(0.001f));
        }

        [Test]
        public void EachGrillCostsTheSameEveryDayAndItsZonesArentMetered()
        {
            var grill = new Grill(_sim,
                new PanProperties { TypeId = "Grate", HeatCapacity = 750f, AirLossConductance = 7.7f, Capacity = 2, FixedTemperature = 260f },
                new PanProperties { TypeId = "Grate", HeatCapacity = 750f, AirLossConductance = 7.7f, Capacity = 2, FixedTemperature = 150f },
                new HobProperties { Kind = HeatKind.Fixed, MaxPower = 6000f, FixedTemperature = 260f, Response = 75f },
                new HobProperties { Kind = HeatKind.Fixed, MaxPower = 6000f, FixedTemperature = 150f, Response = 75f });
            grill.Hot.Put(NewPan());

            _sim.Tick(Hour);
            var bill = _meter.Read(_sim.Hobs, 2, Prices);

            Assert.That(bill.Charcoal, Is.EqualTo(30f));
            Assert.That(bill.Total, Is.EqualTo(30f), "the pan on the grill adds nothing");
        }

        [Test]
        public void TheDayPaysItsEnergyWhenServiceEnds()
        {
            var days = new DayCycle();
            var before = days.Balance;
            days.Advance();
            days.Advance();

            days.FinishService(ServiceSummary.From(new ServiceSimulation(new ServiceSettings(), seatCount: 1)), energy: new EnergyBill(1f, 2f, 15f));

            Assert.That(days.Balance, Is.EqualTo(before - 18f).Within(0.001f));
            Assert.That(days.LastEnergy.Total, Is.EqualTo(18f).Within(0.001f));
        }
    }
}
