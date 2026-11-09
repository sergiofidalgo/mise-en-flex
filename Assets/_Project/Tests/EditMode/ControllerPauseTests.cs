using Game.Simulation.Days;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    // A lost controller pauses the service only (decision 2026-11-09).
    public class ControllerPauseTests
    {
        private static (int, bool)[] Players(params bool[] lost)
        {
            var players = new (int, bool)[lost.Length];
            for (var i = 0; i < lost.Length; i++)
                players[i] = (i, lost[i]);
            return players;
        }

        [Test]
        public void ALostControllerPausesTheService()
        {
            var pause = new ControllerPause();
            pause.Update(Players(false, false, true, false), inService: true);
            Assert.That(pause.Paused);
            Assert.That(pause.WaitingFor, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void OutsideServiceNothingWaits()
        {
            var pause = new ControllerPause();
            pause.Update(Players(false, true), inService: false);
            Assert.That(pause.Paused, Is.False);
            Assert.That(pause.IsLost(1), "still shown as lost");
        }

        [Test]
        public void ItCarriesOnOnceTheControllerIsBack()
        {
            var pause = new ControllerPause();
            pause.Update(Players(false, true), inService: true);
            pause.Update(Players(false, false), inService: true);
            Assert.That(pause.Paused, Is.False);
        }

        [Test]
        public void CarryingOnWithoutThemStaysCarriedOn()
        {
            var pause = new ControllerPause();
            pause.Update(Players(false, true), inService: true);
            pause.CarryOn();
            pause.Update(Players(false, true), inService: true);
            Assert.That(pause.Paused, Is.False);
        }

        [Test]
        public void ComingBackAndDroppingAgainIsWaitedForAgain()
        {
            var pause = new ControllerPause();
            pause.Update(Players(false, true), inService: true);
            pause.CarryOn();
            pause.Update(Players(false, false), inService: true);
            pause.Update(Players(false, true), inService: true);
            Assert.That(pause.Paused);
        }

        [Test]
        public void AnotherDropAfterCarryingOnIsWaitedFor()
        {
            var pause = new ControllerPause();
            pause.Update(Players(false, true, false), inService: true);
            pause.CarryOn();
            pause.Update(Players(false, true, true), inService: true);
            Assert.That(pause.WaitingFor, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void TheReadyUpLeavesOutLostPlayers()
        {
            var ready = new ReadyCheck();
            ready.Toggle(0);
            ready.Toggle(1);
            Assert.That(ready.IsEveryoneReady(new[] { 0, 1 }), "P3 lost: P1 and P2 are everyone");
            Assert.That(ready.IsEveryoneReady(new[] { 0, 1, 2 }), Is.False, "P3 back and not ready");
            Assert.That(ready.IsEveryoneReady(new int[0]), Is.False, "nobody here");
        }
    }
}
