using Game.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class ColorStopTests
    {
        private static readonly ColorStop[] Stops =
        {
            new ColorStop(50f, Color.red),
            new ColorStop(60f, Color.blue),
        };

        [Test]
        public void BelowTheStartIsTheItemsOwnColour()
        {
            Assert.That(ColorStop.Sample(Stops, 20f, Color.white, 40f), Is.EqualTo(Color.white));
        }

        [Test]
        public void BlendsFromTheOwnColourIntoTheFirstStop()
        {
            var halfway = ColorStop.Sample(Stops, 45f, Color.white, 40f);
            Assert.That(halfway, Is.EqualTo(Color.Lerp(Color.white, Color.red, 0.5f)));
        }

        [Test]
        public void BlendsBetweenStops()
        {
            Assert.That(ColorStop.Sample(Stops, 55f, Color.white, 40f), Is.EqualTo(Color.Lerp(Color.red, Color.blue, 0.5f)));
        }

        [Test]
        public void AboveTheLastStopHoldsTheLastColour()
        {
            Assert.That(ColorStop.Sample(Stops, 90f, Color.white, 40f), Is.EqualTo(Color.blue));
        }
    }
}
