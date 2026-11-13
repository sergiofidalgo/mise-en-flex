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
    /// happens on its Staff tab with a controller, and only holding Y on "Start day N" leaves it
    /// (Y, not A, since 2026-11-11; Prep's own ready-up is a Y hold too).
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
        public IEnumerator HoldingBFillsTheOfficeChipAndLettingGoEarlyStays()
        {
            // The header's "Go to the boss's office" (2026-11-13): a hold of B, so a tap can't leave.
            yield return Arrive();
            yield return Join();
            yield return Press(GamepadButton.East);
            Assert.That(_dashboard.OfficeHold, Is.LessThan(0.2f), "a tap barely fills it");
            InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.East));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(_dashboard.OfficeHold, Is.InRange(0.3f, 0.95f), "holding B fills it");
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(_dashboard.OfficeHold, Is.EqualTo(0f), "let go early, it drains");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneName), "and nobody left");
        }

        [UnityTest]
        public IEnumerator TheTabsRunOverviewRestaurantBooksStaff()
        {
            yield return Arrive();
            yield return Join();
            Assert.That(_dashboard.ShownTab, Is.EqualTo("Overview"));
            foreach (var tab in new[] { "Restaurant", "Books", "Staff", "Overview" })
            {
                yield return Press(GamepadButton.RightShoulder);
                Assert.That(_dashboard.ShownTab, Is.EqualTo(tab));
            }
        }

        [UnityTest]
        public IEnumerator UnlockingADishWarmsItUpAndSettles()
        {
            // The unlock moment (2026-11-11): A on a locked dish buys it, and it warms up on screen.
            yield return Arrive();
            yield return Join();
            var book = _root.Services.Cookbook;
            Assume.That(book, Is.Not.Null);
            Assume.That(book.Recipes.Count, Is.GreaterThan(1));
            var second = book.Recipes[1];
            Assume.That(book.IsUnlocked(second.Id), Is.False);
            _root.Services.Days.Receive(second.UnlockCost + 100f);

            yield return Press(GamepadButton.RightShoulder);
            Assume.That(_dashboard.ShownTab, Is.EqualTo("Restaurant"));
            yield return Press(GamepadButton.DpadDown);

            // A tap no longer buys (2026-11-13): it's a hold, with a bar filling.
            yield return Press(GamepadButton.South);
            Assert.That(book.IsUnlocked(second.Id), Is.False, "a tap doesn't buy");
            InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.South));
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(_dashboard.UnlockHold, Is.GreaterThan(0.2f), "the bar fills while A is held");
            Assert.That(book.IsUnlocked(second.Id), Is.False, "not yet");
            yield return new WaitForSecondsRealtime(0.7f);
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;

            Assert.That(book.IsUnlocked(second.Id), Is.True, "holding A bought the second dish");
            Assert.That(book.IsOnMenu(second.Id), Is.True, "and put it on the menu");
            Assert.That(_dashboard.IsWarmingUp, Is.True, "and it's warming up");
            yield return new WaitForSecondsRealtime(2.4f);
            Assert.That(_dashboard.IsWarmingUp, Is.False, "the moment settles");

            // A tap on a dish you own takes it off the menu, and another puts it back (2026-11-13).
            yield return Press(GamepadButton.South);
            Assert.That(book.IsOnMenu(second.Id), Is.False, "taken off the menu");
            Assert.That(book.IsUnlocked(second.Id), Is.True, "still owned");
            yield return Press(GamepadButton.South);
            Assert.That(book.IsOnMenu(second.Id), Is.True, "put back on");
        }

        [UnityTest]
        public IEnumerator AShortDPadPressStepsOneRowWithASecondCookIdle()
        {
            // The playtest's jumping (2026-11-11): with a second cook idle, a held D-pad stepped every
            // frame, so a short press raced to the bottom of the list.
            yield return Arrive();
            yield return Join();
            var second = InputSystem.AddDevice<Gamepad>("ForkcastTestPad2");
            Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Gamepad", pairWithDevices: second);
            yield return null;
            yield return null;
            try
            {
                var services = _root.Services;
                services.Days.Receive(1000f);
                Assume.That(services.Staff.Hired.Count, Is.Zero);
                Assume.That(services.Staff.Candidates.Count, Is.GreaterThanOrEqualTo(3));
                var target = services.Staff.Candidates[1];

                yield return Press(GamepadButton.LeftShoulder);
                Assume.That(_dashboard.ShownTab, Is.EqualTo("Staff"));
                // Held for less than the repeat delay: one step, from the first candidate to the second.
                InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.DpadDown));
                yield return new WaitForSecondsRealtime(0.15f);
                InputSystem.QueueStateEvent(_pad, new GamepadState());
                yield return null;
                yield return Press(GamepadButton.South);

                Assert.That(services.Staff.Hired.Count, Is.EqualTo(1));
                Assert.That(services.Staff.Hired[0].Name, Is.EqualTo(target.Name), "one row down, not the bottom");
            }
            finally
            {
                InputSystem.RemoveDevice(second);
            }
        }

        [UnityTest]
        public IEnumerator HoldingYFillsStartAndOpensPrep()
        {
            yield return Arrive();
            yield return Join();
            var hired = _root.Services.Staff.Hired.Count;

            // Y held on the Overview: no tap, so no reply wheel, and the Start button fills.
            InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.North));
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

            InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.North));
            yield return new WaitForSecondsRealtime(1.1f);
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;

            Assert.That(days.Phase, Is.EqualTo(DayPhase.Prep));
            Assert.That(days.Day, Is.EqualTo(day + 1));
            Assert.That(_dashboard.IsShowing, Is.False);
        }

        [UnityTest]
        public IEnumerator InPrepHoldingYOpensUp()
        {
            yield return Arrive();
            yield return Join();
            var days = _root.Services.Days;
            days.Advance(); // Prep
            yield return null;
            Assume.That(days.Phase, Is.EqualTo(DayPhase.Prep));

            InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.North));
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Prep), "not held long enough yet");
            Assert.That(Object.FindFirstObjectByType<DayLoop>().ReadyHoldProgress(_player.playerIndex), Is.GreaterThan(0f), "the bar fills");
            yield return new WaitForSecondsRealtime(0.8f);
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Service), "one cook: holding Y opens up");
        }

        [UnityTest]
        public IEnumerator InPrepBTakesAReadyBack()
        {
            yield return Arrive();
            yield return Join();
            var second = InputSystem.AddDevice<Gamepad>("ForkcastTestPad2");
            Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Gamepad", pairWithDevices: second);
            yield return null;
            yield return null;
            var days = _root.Services.Days;
            days.Advance(); // Prep
            yield return null;
            var ready = Object.FindFirstObjectByType<DayLoop>().Ready;

            InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.North));
            yield return new WaitForSecondsRealtime(1.1f);
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
            Assert.That(ready.IsReady(_player.playerIndex), Is.True, "holding Y readies");
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Prep), "the second cook isn't ready");

            yield return Press(GamepadButton.East);
            Assert.That(ready.IsReady(_player.playerIndex), Is.False, "B takes it back");
            InputSystem.RemoveDevice(second);
        }
    }
}
