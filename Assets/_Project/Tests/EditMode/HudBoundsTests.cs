using Game.Presentation.Hud;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class HudBoundsTests
    {
        private static readonly Rect Screen = new Rect(0f, 0f, 1920f, 1080f);
        private const float Margin = 12f;

        [Test]
        public void ACardOnScreenStaysPut()
        {
            Assert.That(HudBounds.Shift(new Rect(800f, 400f, 300f, 120f), Screen, Margin), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ACardOffTheTopSlidesDownToTheMargin()
        {
            // The shop list over a station by the back wall: its top was off the screen.
            var shift = HudBounds.Shift(new Rect(700f, 1000f, 220f, 200f), Screen, Margin);
            Assert.That(shift.x, Is.EqualTo(0f));
            Assert.That(1000f + 200f + shift.y, Is.EqualTo(1080f - Margin).Within(1e-3f));
        }

        [Test]
        public void ACardOffTheLeftSlidesRight()
        {
            var shift = HudBounds.Shift(new Rect(-50f, 300f, 200f, 60f), Screen, Margin);
            Assert.That(-50f + shift.x, Is.EqualTo(Margin).Within(1e-3f));
            Assert.That(shift.y, Is.EqualTo(0f));
        }

        [Test]
        public void ACardTooTallForTheScreenKeepsItsTopLineOnScreen()
        {
            var shift = HudBounds.Shift(new Rect(100f, -300f, 200f, 1600f), Screen, Margin);
            Assert.That(-300f + 1600f + shift.y, Is.EqualTo(1080f - Margin).Within(1e-3f));
        }
    }
}
