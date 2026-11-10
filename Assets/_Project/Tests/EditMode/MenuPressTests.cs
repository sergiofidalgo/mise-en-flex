using Game.Simulation.Days;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    // Tap Menu pauses, hold Menu readies up (decision 2026-11-10).
    public class MenuPressTests
    {
        private const float Frame = 1f / 60f;

        private static MenuPress Armed()
        {
            var press = new MenuPress();
            press.Update(false, Frame, true);
            return press;
        }

        private static MenuPress.Result Hold(MenuPress press, float seconds, bool holdMeansSomething)
        {
            var result = MenuPress.Result.None;
            for (var t = 0f; t < seconds; t += Frame)
            {
                var r = press.Update(true, Frame, holdMeansSomething);
                if (r != MenuPress.Result.None)
                    result = r;
            }
            return result;
        }

        [Test]
        public void AQuickTapPausesOnRelease()
        {
            var press = Armed();
            Assert.That(Hold(press, 0.2f, true), Is.EqualTo(MenuPress.Result.None));
            Assert.That(press.Update(false, Frame, true), Is.EqualTo(MenuPress.Result.Tap));
        }

        [Test]
        public void AHoldReadiesOnceAndTheReleaseDoesNothing()
        {
            var press = Armed();
            Assert.That(Hold(press, 1.5f, true), Is.EqualTo(MenuPress.Result.Hold));
            Assert.That(press.Update(false, Frame, true), Is.EqualTo(MenuPress.Result.None));
        }

        [Test]
        public void WhereHoldingMeansNothingThePressPausesAtOnce()
        {
            var press = Armed();
            Assert.That(press.Update(true, Frame, false), Is.EqualTo(MenuPress.Result.Tap));
            Assert.That(Hold(press, 1.5f, false), Is.EqualTo(MenuPress.Result.None));
            Assert.That(press.Update(false, Frame, false), Is.EqualTo(MenuPress.Result.None));
        }

        [Test]
        public void TheProgressFillsDuringTheHold()
        {
            var press = Armed();
            Hold(press, 0.3f, true);
            Assert.That(press.HoldProgress, Is.EqualTo(0.5f).Within(0.05f));
            Hold(press, 0.5f, true);
            Assert.That(press.HoldProgress, Is.EqualTo(0f), "a finished hold shows no ring");
        }

        [Test]
        public void TheButtonThatJoinedIsIgnoredUntilLetGo()
        {
            var press = new MenuPress();
            Assert.That(Hold(press, 1.5f, true), Is.EqualTo(MenuPress.Result.None));
            Assert.That(press.Update(false, Frame, true), Is.EqualTo(MenuPress.Result.None));
            Assert.That(press.Update(true, Frame, false), Is.EqualTo(MenuPress.Result.Tap));
        }

        [Test]
        public void DisarmingWaitsForTheButtonToComeUp()
        {
            var press = Armed();
            press.Update(true, Frame, true);
            press.Disarm();
            Assert.That(press.Update(false, Frame, true), Is.EqualTo(MenuPress.Result.None));
            Assert.That(press.Update(true, Frame, false), Is.EqualTo(MenuPress.Result.Tap));
        }
    }
}
