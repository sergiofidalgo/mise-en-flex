using System.Collections;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Customers;
using Game.Presentation.Hud;
using Game.Simulation.Days;
using Game.Simulation.Service;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// The day's transitions in the real kitchen (the 2026-11-11 playtest round): the countdown
    /// before service holds the guests on the doorstep until zero, and the closing shutter comes
    /// down, stays behind the dashboard and rolls up on the next morning.
    /// </summary>
    public class ServiceCountdownPlayTests
    {
        private KitchenCompositionRoot _root;
        private DayTransitions _transitions;
        private PlayerInput _player;
        private float _timeScale;

        private KitchenServices Services => _root.Services;

        private IEnumerator OpenKitchen(float countdown)
        {
            KitchenCompositionRoot.CountdownSecondsOverride = countdown;
            _timeScale = Time.timeScale;
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _transitions = Object.FindFirstObjectByType<DayTransitions>();
            Assert.That(_transitions, Is.Not.Null, "The kitchen has its day transitions (Tools/Game/Build Greybox Kitchen).");
            var keyboard = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.GetDevice<Mouse>() ?? InputSystem.AddDevice<Mouse>();
            _player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Keyboard&Mouse", pairWithDevices: new InputDevice[] { keyboard, mouse });
            yield return null;
            yield return null;
        }

        [TearDown]
        public void Restore()
        {
            KitchenCompositionRoot.CountdownSecondsOverride = 0f; // as the rest of the suite runs (SaveIsolation)
            Time.timeScale = _timeScale > 0f ? _timeScale : 1f;
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, float seconds)
        {
            var until = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.That(condition(), Is.True, "in time");
        }

        [UnityTest]
        public IEnumerator GuestsWaitOnTheDoorstepAndWalkInAtZero()
        {
            // Long enough for the first party to walk the pavement to the door before zero.
            const float countdown = 30f;
            yield return OpenKitchen(countdown);
            Time.timeScale = 4f; // the countdown and the walk both run on game time

            Services.Days.Advance(); // to Prep
            Services.Days.Advance(); // to Service: the countdown starts
            yield return null;
            yield return null;

            Assert.That(Services.Countdown.Running, Is.True);
            Assert.That(Services.Service.DoorsHeld, Is.True);
            Assert.That(_transitions.CountdownShowing, Is.True, "the 4-3-2-1 is on screen");
            Assert.That(_player.GetComponent<Game.Gameplay.Stations.PlayerInteractor>().enabled, Is.True, "chefs work during it");
            Assert.That(Services.Paused, Is.False);

            var service = Services.Service;
            yield return WaitFor(() => service.Customers.Count > 0 && service.Customers[0].Party.All(c => c.AtDoor), countdown / Time.timeScale);
            Assert.That(Services.Countdown.Running, Is.True, "they reach the door before zero");
            var first = service.Customers[0];
            yield return new WaitForSeconds(1f);
            Assert.That(first.State, Is.EqualTo(CustomerState.Approaching), "nobody comes in before zero");
            Assert.That(first.Patience, Is.EqualTo(1f), "waiting for the doors costs no patience");
            Assert.That(service.Elapsed, Is.EqualTo(0f), "the service clock waits for the doors");

            yield return WaitFor(() => !Services.Countdown.Running, countdown / Time.timeScale + 1f);
            yield return null;
            Assert.That(service.DoorsHeld, Is.False);
            Assert.That(first.State, Is.Not.EqualTo(CustomerState.Approaching), "at zero they walk in");
            Assert.That(_transitions.ServiceBandShowing, Is.True, "Service! sweeps across");
            Assert.That(_transitions.CountdownShowing, Is.False);
            yield return WaitFor(() => !_transitions.ServiceBandShowing, 3f);
        }

        [UnityTest]
        public IEnumerator TheShutterComesDownStaysBehindTheDashboardAndRollsUpInTheMorning()
        {
            yield return OpenKitchen(0f);
            Assert.That(_transitions.FindsDashboard, Is.True, "the dashboard's panel and backdrop are where the transitions look");
            var days = Services.Days;
            // The kitchen loads on the dashboard (the suite starts in Plan), so the shutter's down
            // behind it, and Prep opens on day 1's sign.
            Assert.That(_transitions.Current, Is.EqualTo(DayTransitions.Stage.Shut));
            days.Advance(); // to Prep
            yield return null;
            Assert.That(_transitions.Current, Is.EqualTo(DayTransitions.Stage.Opening));
            Assert.That(_transitions.SignText, Is.EqualTo("Day 1"));
            yield return WaitFor(() => _transitions.Current == DayTransitions.Stage.Open, 3f);
            days.Advance(); // to Service
            yield return null;
            Assert.That(_transitions.Current, Is.EqualTo(DayTransitions.Stage.Open));

            days.FinishService(ServiceSummary.From(Services.Service));
            yield return null;
            Assert.That(_transitions.Current, Is.EqualTo(DayTransitions.Stage.Closing), "the shutter comes down");
            Assert.That(_transitions.SignText, Is.EqualTo("Closing time"));
            yield return WaitFor(() => _transitions.Current == DayTransitions.Stage.Shut, 3f);

            var dashboard = Object.FindFirstObjectByType<ReviewDashboard>();
            Assert.That(dashboard.IsShowing, Is.True);
            var panel = dashboard.transform.Find("ReviewDashboard/Layout") as RectTransform;
            Assert.That(panel.anchoredPosition.y, Is.EqualTo(0f).Within(0.5f), "the dashboard has risen into place");
            var backdrop = dashboard.transform.Find("ReviewDashboard/Backdrop").GetComponent<UnityEngine.UI.RawImage>();
            Assert.That(backdrop.enabled, Is.False, "the blurred shutter is behind it, not the kitchen photo");

            // Start day 2: through Plan to Prep, as the dashboard's hold does.
            days.Advance();
            days.Advance();
            yield return null;
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Prep));
            Assert.That(_transitions.Current, Is.EqualTo(DayTransitions.Stage.Opening));
            Assert.That(_transitions.SignText, Is.EqualTo("Day 2"));
            yield return WaitFor(() => _transitions.Current == DayTransitions.Stage.Open, 3f);
        }
    }
}
