using Game.Simulation.Cooking;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class HobControlsTests
    {
        private const float Frame = 1f / 60f;

        private CookingSimulation _sim;
        private Hob _hob;
        private Pan _pan;

        [SetUp]
        public void CreateHob()
        {
            _sim = new CookingSimulation(22f);
            _hob = _sim.AddHob(new HobProperties { MaxPower = 2000f });
            _pan = _sim.AddPan(new PanProperties { HeatCapacity = 750f, AirLossConductance = 7.7f, Capacity = 1 });
            _hob.PlacePan(_pan);
        }

        private void Hold(float down, float up, int frames)
        {
            for (var i = 0; i < frames; i++)
                HobControls.AdjustHeat(_hob, down, up, dialSpeed: 1f, Frame);
        }

        [Test]
        public void RightTriggerTurnsHeatUpSmoothly()
        {
            Hold(down: 0f, up: 1f, frames: 30);
            Assert.That(_hob.Dial, Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void HalfTriggerTurnsHeatHalfAsFast()
        {
            Hold(down: 0f, up: 0.5f, frames: 30);
            Assert.That(_hob.Dial, Is.EqualTo(0.25f).Within(0.01f));
        }

        [Test]
        public void LeftTriggerTurnsHeatDownAndStopsAtOff()
        {
            _hob.Dial = 0.2f;
            Hold(down: 1f, up: 0f, frames: 60);
            Assert.That(_hob.Dial, Is.EqualTo(0f));
        }

        [Test]
        public void HeatStopsAtFull()
        {
            Hold(down: 0f, up: 1f, frames: 120);
            Assert.That(_hob.Dial, Is.EqualTo(1f));
        }

        [Test]
        public void UseFlipsTheFoodInThePan()
        {
            var steak = _sim.AddIngredient(new IngredientProperties { TypeId = "Steak", HeatCapacity = 1050f, SurfaceFraction = 0.15f });
            _pan.Put(steak);

            Assert.That(HobControls.TryFlip(_hob), Is.True);
            Assert.That(steak.DownSide, Is.EqualTo(IngredientSide.B));
        }

        [Test]
        public void FlippingAnEmptyPanDoesNothing()
        {
            Assert.That(HobControls.TryFlip(_hob), Is.False);
        }

        [Test]
        public void FlippingWithNoPanDoesNothing()
        {
            _hob.RemovePan();
            Assert.That(HobControls.TryFlip(_hob), Is.False);
        }
    }
}
