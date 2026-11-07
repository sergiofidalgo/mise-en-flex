using Game.Simulation.Cooking;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The charcoal grill (decisions 2026-11-07): a hot and a cool zone, two pieces each on the grate
    /// or a pan taking a whole zone; X flips the hotter piece onto the other zone.
    /// </summary>
    public class GrillTests
    {
        private const float Minute = 60f;

        private CookingSimulation _sim;
        private Grill _grill;

        [SetUp]
        public void CreateGrill()
        {
            _sim = new CookingSimulation(22f);
            PanProperties Grate(float t) => new PanProperties { TypeId = "Grate", HeatCapacity = 750f, AirLossConductance = 7.7f, Capacity = Grill.PiecesPerZone, FixedTemperature = t };
            HobProperties Coals(float t) => new HobProperties { Kind = HeatKind.Fixed, MaxPower = 6000f, FixedTemperature = t, Response = 75f };
            _grill = new Grill(_sim, Grate(260f), Grate(150f), Coals(260f), Coals(150f));
        }

        private Pan NewPan() => _sim.AddPan(new PanProperties { HeatCapacity = 750f, AirLossConductance = 7.7f, Capacity = 1 });

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

        [Test]
        public void EachZoneTakesTwoPieces()
        {
            _grill.Hot.Put(Steak());
            _grill.Hot.Put(Steak());

            Assert.That(_grill.Hot.CanAccept(Steak()), Is.False);
            Assert.That(_grill.Cool.CanAccept(Steak()), Is.True);
        }

        [Test]
        public void APanTakesAWholeZone()
        {
            _grill.Hot.Put(NewPan());

            Assert.That(_grill.Hot.CanAccept(Steak()), Is.False, "no food beside the pan");
            Assert.That(_grill.Hot.CanAccept(NewPan()), Is.False);

            _grill.Cool.Put(Steak());
            Assert.That(_grill.Cool.CanAccept(NewPan()), Is.False, "not on top of food");
        }

        [Test]
        public void APanOnAZoneHeatsToItsTemperature()
        {
            var pan = NewPan();
            _grill.Cool.Put(pan);

            _sim.Tick(3f * Minute);

            Assert.That(pan.Temperature, Is.EqualTo(150f).Within(3f));
        }

        [Test]
        public void TakingFromAZoneTakesTheHotterPiece()
        {
            var first = Steak();
            _grill.Hot.Put(first);
            _sim.Tick(Minute);
            var cold = Steak();
            _grill.Hot.Put(cold);

            Assert.That(_grill.Hot.Take(), Is.SameAs(first));
        }

        [Test]
        public void FlippingMovesTheHotterPieceToTheOtherZoneTurnedOver()
        {
            var steak = Steak();
            _grill.Hot.Put(steak);
            var down = steak.DownSide;

            Assert.That(_grill.TryFlip(_grill.Hot), Is.True);
            Assert.That(_grill.Cool.Grate.Contents, Does.Contain(steak));
            Assert.That(_grill.Hot.Grate.Contents, Is.Empty);
            Assert.That(steak.DownSide, Is.Not.EqualTo(down));

            Assert.That(_grill.TryFlip(_grill.Cool), Is.True, "and back again");
            Assert.That(_grill.Hot.Grate.Contents, Does.Contain(steak));
        }

        [Test]
        public void NoFlipWhileTheOtherZoneIsFull()
        {
            _grill.Cool.Put(Steak());
            _grill.Cool.Put(Steak());
            _grill.Hot.Put(Steak());

            Assert.That(_grill.TryFlip(_grill.Hot), Is.False);
            Assert.That(_grill.Hot.Grate.Contents.Count, Is.EqualTo(1));
        }

        [Test]
        public void InAPanFlippingTurnsTheFoodWithoutMovingIt()
        {
            var pan = NewPan();
            var steak = Steak();
            pan.Put(steak);
            _grill.Hot.Put(pan);
            var down = steak.DownSide;

            Assert.That(_grill.TryFlip(_grill.Hot), Is.True);
            Assert.That(steak.DownSide, Is.Not.EqualTo(down));
            Assert.That(_grill.Hot.StandingPan, Is.SameAs(pan));
        }

        [Test]
        public void StartingOnTheCoolZoneGivesAWeakerCrust()
        {
            var seared = Steak();
            var pale = Steak();
            _grill.Hot.Put(seared);
            _grill.Cool.Put(pale);

            _sim.Tick(2f * Minute);

            Assert.That(pale.Browning(pale.DownSide), Is.LessThan(seared.Browning(seared.DownSide) / 4f));
        }
    }
}
