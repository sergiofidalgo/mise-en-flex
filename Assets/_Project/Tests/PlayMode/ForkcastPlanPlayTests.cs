using System.Collections;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Presentation.Hud;
using Game.Simulation.Days;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// The real kitchen, entered in the morning (the user's calls, 2026-11-10): forkcast is up, hiring
    /// happens on its Staff tab with a controller, and only holding A on "Start day N" leaves it.
    /// </summary>
    public class ForkcastPlanPlayTests
    {
        private const string SceneName = "Kitchen_Greybox";

        private KitchenCompositionRoot _root;
        private ReviewDashboard _dashboard;
        private Gamepad _pad;
        private PlayerInput _player;

        private IEnumerator Arrive()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _dashboard = Object.FindFirstObjectByType<ReviewDashboard>();
            yield return null;
        }

        private IEnumerator Join()
        {
            _pad = InputSystem.AddDevice<Gamepad>("ForkcastTestPad");
            _player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Gamepad", pairWithDevices: _pad);
            // Joining's own press never counts: the player acts from the frame after they're here.
            yield return null;
            yield return null;
        }

        private IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
        }

        [TearDown]
        public void RemovePad()
        {
            if (_pad != null)
                InputSystem.RemoveDevice(_pad);
            _pad = null;
        }

        [UnityTest]
        public IEnumerator ForkcastIsUpOnArrivingInTheMorning()
        {
            yield return Arrive();
            Assert.That(_root.Services.Days.Phase, Is.EqualTo(DayPhase.Plan));
            Assert.That(_root.Services.Forkcast.IsOpen, Is.True);
            Assert.That(_dashboard.IsShowing, Is.True, "the dashboard is drawn, not just meant to be");
        }

        [UnityTest]
        public IEnumerator APressOnACandidateHires()
        {
            yield return Arrive();
            yield return Join();
            var services = _root.Services;
            services.Days.Receive(1000f);
            Assume.That(services.Staff.Candidates.Count, Is.GreaterThan(0));
            Assume.That(services.Staff.Hired.Count, Is.Zero);
            var first = services.Staff.Candidates[0];

            // LB from the Overview wraps round to Staff, the last tab. With nobody hired, the first
            // row is the first candidate.
            yield return Press(GamepadButton.LeftShoulder);
            yield return Press(GamepadButton.South);

            Assert.That(services.Staff.Hired.Count, Is.EqualTo(1));
            Assert.That(services.Staff.Hired[0].Name, Is.EqualTo(first.Name));
            Assert.That(_dashboard.IsShowing, Is.True, "hiring doesn't close it");
        }

        [UnityTest]
        public IEnumerator HoldingAFillsStartAndOpensPrep()
        {
            yield return Arrive();
            yield return Join();
            var hired = _root.Services.Staff.Hired.Count;

            // A held on the Overview: no tap, so nothing's hired, and the Start button fills.
            InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.South));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(_root.Services.Days.Phase, Is.EqualTo(DayPhase.Plan), "not full yet");
            yield return new WaitForSecondsRealtime(0.7f);
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;

            Assert.That(_root.Services.Days.Phase, Is.EqualTo(DayPhase.Prep), "one cook: ready is everyone");
            Assert.That(_root.Services.Forkcast.IsOpen, Is.False);
            Assert.That(_root.Services.Staff.Hired.Count, Is.EqualTo(hired));
        }

        [UnityTest]
        public IEnumerator HoldingMenuInPlanNoLongerStartsTheDay()
        {
            yield return Arrive();
            yield return Join();
            InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.Start));
            yield return new WaitForSecondsRealtime(1f);
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
            Assert.That(_root.Services.Days.Phase, Is.EqualTo(DayPhase.Plan));
            // It opened the pause menu instead: close it so the next test starts clean.
            Game.Gameplay.GamePause.MenuOpen = false;
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator NothingButStartPrepLeavesIt()
        {
            yield return Arrive();
            yield return Join();
            var chef = _player.GetComponent<PlayerInteractor>();
            foreach (var button in new[] { GamepadButton.East, GamepadButton.North, GamepadButton.Select })
            {
                yield return Press(button);
                Assert.That(_root.Services.Forkcast.IsOpen, Is.True, $"{button} leaves it up");
                Assert.That(chef.IsManaging, Is.False, "no rearranging behind it");
            }
            Assert.That(chef.IsReviewing, Is.True, "the chef waits");
        }

        [UnityTest]
        public IEnumerator FromReviewOneHoldGoesStraightToPrep()
        {
            yield return Arrive();
            yield return Join();
            var days = _root.Services.Days;
            days.Advance();
            days.Advance();
            days.FinishService(Game.Simulation.Service.ServiceSummary.From(_root.Services.Service));
            yield return null;
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Review));
            Assert.That(_dashboard.IsShowing, Is.True);
            var day = days.Day;

            InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.South));
            yield return new WaitForSecondsRealtime(1.1f);
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;

            Assert.That(days.Phase, Is.EqualTo(DayPhase.Prep));
            Assert.That(days.Day, Is.EqualTo(day + 1));
            Assert.That(_dashboard.IsShowing, Is.False);
        }
    }
}
