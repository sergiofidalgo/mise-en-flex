using Game.Simulation.CloseUp;
using Game.Simulation.Cooking;
using NUnit.Framework;
using Vector2 = System.Numerics.Vector2;

namespace Game.Tests.EditMode
{
    public class HobTechniqueTests
    {
        private const float Frame = 1f / 60f;

        private CookingSimulation _sim;
        private Hob _hob;
        private Pan _pan;
        private HobTechnique _technique;

        private static CloseUpInput Input(
            float aimX = 0f, bool flip = false, float heatDown = 0f, float heatUp = 0f) =>
            new CloseUpInput(new Vector2(aimX, 0f), flip, false, false, heatDown, heatUp);

        private Ingredient AddSteak()
        {
            var steak = _sim.AddIngredient(new IngredientProperties { TypeId = "Steak", HeatCapacity = 1050f, SurfaceFraction = 0.15f });
            _pan.Put(steak);
            return steak;
        }

        [SetUp]
        public void CreateHob()
        {
            _sim = new CookingSimulation(22f);
            _hob = _sim.AddHob(new HobProperties { MaxPower = 2000f });
            _pan = _sim.AddPan(new PanProperties { HeatCapacity = 750f, AirLossConductance = 7.7f, Capacity = 2 });
            _hob.PlacePan(_pan);
            _technique = new HobTechnique(_hob, dialSpeed: 1f);
        }

        [Test]
        public void RightTriggerTurnsHeatUpSmoothly()
        {
            for (var i = 0; i < 30; i++)
                _technique.HandleInput(Input(heatUp: 1f), Frame);
            Assert.That(_hob.Dial, Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void HalfTriggerTurnsHeatHalfAsFast()
        {
            for (var i = 0; i < 30; i++)
                _technique.HandleInput(Input(heatUp: 0.5f), Frame);
            Assert.That(_hob.Dial, Is.EqualTo(0.25f).Within(0.01f));
        }

        [Test]
        public void LeftTriggerTurnsHeatDownAndStopsAtOff()
        {
            _hob.Dial = 0.2f;
            for (var i = 0; i < 60; i++)
                _technique.HandleInput(Input(heatDown: 1f), Frame);
            Assert.That(_hob.Dial, Is.EqualTo(0f));
        }

        [Test]
        public void AFlipsTheSelectedFood()
        {
            var steak = AddSteak();
            _technique.HandleInput(Input(flip: true), Frame);
            Assert.That(steak.DownSide, Is.EqualTo(IngredientSide.B));
        }

        [Test]
        public void StickFlickSelectsTheNextPieceAndAFlipsOnlyThatOne()
        {
            var first = AddSteak();
            var second = AddSteak();

            _technique.HandleInput(Input(), Frame);           // selects the first by default
            _technique.HandleInput(Input(aimX: 1f), Frame);   // flick right
            _technique.HandleInput(Input(flip: true), Frame);

            Assert.That(_technique.Selected, Is.SameAs(second));
            Assert.That(second.DownSide, Is.EqualTo(IngredientSide.B));
            Assert.That(first.DownSide, Is.EqualTo(IngredientSide.A));
        }

        [Test]
        public void HoldingTheStickMovesOnlyOncePerFlick()
        {
            AddSteak();
            AddSteak();
            _technique.HandleInput(Input(), Frame);
            for (var i = 0; i < 10; i++)
                _technique.HandleInput(Input(aimX: 1f), Frame);
            Assert.That(_technique.SelectedIndex, Is.EqualTo(1));
        }

        [Test]
        public void SelectionWrapsAround()
        {
            AddSteak();
            AddSteak();
            _technique.HandleInput(Input(), Frame);
            _technique.HandleInput(Input(aimX: -1f), Frame);  // flick left from the first
            Assert.That(_technique.SelectedIndex, Is.EqualTo(1));
        }

        [Test]
        public void EmptyPanHasNoSelectionAndAIsHarmless()
        {
            Assert.DoesNotThrow(() => _technique.HandleInput(Input(flip: true), Frame));
            Assert.That(_technique.SelectedIndex, Is.EqualTo(-1));
        }

        [Test]
        public void NoPanOnTheHobIsHarmless()
        {
            _hob.RemovePan();
            Assert.DoesNotThrow(() => _technique.HandleInput(Input(flip: true, heatUp: 1f), Frame));
        }
    }
}
