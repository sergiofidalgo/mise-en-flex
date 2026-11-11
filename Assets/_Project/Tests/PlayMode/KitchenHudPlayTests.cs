using System.Collections;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Presentation;
using Game.Presentation.Hud;
using Game.Simulation.Cooking;
using Game.Simulation.Days;
using Game.Simulation.Items;
using Game.Simulation.Service;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// The kitchen HUD after the 2026-11-11 playtest round: debug panels behind a developer toggle,
    /// no control cards on counters (only open menus), button pictures on the appliances that
    /// fade once learnt, and nothing of the kitchen's HUD over the day's dashboard.
    /// </summary>
    public class KitchenHudPlayTests
    {
        private KitchenCompositionRoot _root;
        private PlayerInteractor _chef;
        private Gamepad _pad;
        private InputSettings.BackgroundBehavior _background;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInput;

        private KitchenServices Services => _root.Services;

        private IEnumerator OpenKitchenInService()
        {
            _background = InputSystem.settings.backgroundBehavior;
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _pad = InputSystem.AddDevice<Gamepad>("KitchenHudTestPad");
            var player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Gamepad", pairWithDevices: _pad);
            yield return null;
            yield return null;
            _chef = player.GetComponent<PlayerInteractor>();
            while (Services.Days.Phase != DayPhase.Service)
                Services.Days.Advance();
            yield return null;
        }

        [TearDown]
        public void Release()
        {
            if (_pad != null)
                InputSystem.RemoveDevice(_pad);
            _pad = null;
            InputSystem.settings.backgroundBehavior = _background;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
        }

        private IEnumerator StandAt(Interactable station)
        {
            var front = Services.Messes.InFront(station);
            _chef.GetComponent<PlayerMovement>().Teleport(new System.Numerics.Vector2(front.x, front.z));
            var toward = station.transform.position - _chef.transform.position;
            toward.y = 0f;
            _chef.transform.rotation = Quaternion.LookRotation(toward, Vector3.up);
            yield return null;
            yield return null;
            Assume.That(_chef.Target, Is.SameAs(station), "the chef faces the station");
        }

        // Fades are timed in real seconds; batch frames can be far quicker than a player's.
        private static IEnumerator Settle() => Wait(0.6f);

        private static IEnumerator Wait(float seconds)
        {
            yield return null;
            yield return new WaitForSecondsRealtime(seconds);
        }

        private static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++)
                yield return null;
        }

        private static int CardsShowing() => Object.FindObjectsByType<PromptCard>(FindObjectsSortMode.None)
            .Count(card => card.gameObject.activeInHierarchy && (!card.TryGetComponent(out CanvasGroup group) || group.alpha > 0.01f));

        private HobStation GasHobWithSteak()
        {
            var hob = Services.Stations.All.OfType<HobStation>().First(h => h.Kind == HeatKind.Gas);
            hob.Hob.Pan.Put(Services.Cooking.AddIngredient(Services.Catalog.Find("Steak").ToProperties()));
            return hob;
        }

        [Test]
        public void TheDebugPanelsAreOffUntilADeveloperTurnsThemOn()
        {
            PlayerPrefs.DeleteKey("Game.DeveloperHud.Visible");
            var was = DeveloperHud.Visible;
            DeveloperHud.Visible = false;
            Assert.That(DeveloperHud.Visible, Is.False);
            DeveloperHud.Visible = true;
            Assert.That(DeveloperHud.Visible, Is.True);
            DeveloperHud.Visible = was;
            PlayerPrefs.DeleteKey("Game.DeveloperHud.Visible");
        }

        [UnityTest]
        public IEnumerator NoControlCardOverAHobOnlyItsButtonPictures()
        {
            yield return OpenKitchenInService();
            var hob = GasHobWithSteak();
            yield return StandAt(hob);
            yield return Settle();

            Assert.That(CardsShowing(), Is.Zero, "no prompt card over a hob any more");
            var hints = Object.FindFirstObjectByType<ApplianceHints>();
            Assert.That(hints, Is.Not.Null, "the kitchen has the appliance hints");
            Assert.That(hints.IsShowing(hob, triggers: true), Is.True, "LT RT for the heat");
            Assert.That(hints.IsShowing(hob, triggers: false), Is.True, "X to flip the steak");
        }

        [UnityTest]
        public IEnumerator AHintStopsShowingOnceThePlayerHasUsedItEnough()
        {
            yield return OpenKitchenInService();
            var hob = GasHobWithSteak();
            yield return StandAt(hob);
            var hints = Object.FindFirstObjectByType<ApplianceHints>();

            // Pulls on the heat, as a player would: each pull counts once.
            for (var i = 0; i < hints.UsesBeforeFading; i++)
            {
                InputSystem.QueueStateEvent(_pad, new GamepadState { rightTrigger = 1f });
                yield return Frames(3);
                InputSystem.QueueStateEvent(_pad, new GamepadState());
                yield return Frames(3);
            }
            Assert.That(hints.UsesOf(_chef.PlayerIndex, ApplianceHint.Heat), Is.EqualTo(hints.UsesBeforeFading));
            yield return Settle();

            Assert.That(hints.IsShowing(hob, triggers: true), Is.False, "the heat hint has faded");
            Assert.That(hints.IsShowing(hob, triggers: false), Is.True, "flipping isn't learnt yet");
        }

        [UnityTest]
        public IEnumerator HoldingRTAtAFridgeStillOpensItsMenuCard()
        {
            yield return OpenKitchenInService();
            var fridge = Services.Stations.All.OfType<StorageStation>().First();
            yield return StandAt(fridge);
            yield return Settle();
            Assert.That(CardsShowing(), Is.Zero, "nothing but the picture while it's closed");
            Assert.That(Object.FindFirstObjectByType<ApplianceHints>().IsShowing(fridge, triggers: true), Is.True, "LT RT to browse");

            InputSystem.QueueStateEvent(_pad, new GamepadState { rightTrigger = 1f });
            yield return Settle();
            Assert.That(_chef.IsSlotMenuOpen, Is.True);
            Assert.That(CardsShowing(), Is.EqualTo(1), "the slot menu is a choice, so its card shows");
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheKitchenHudStepsAsideWhenTheDayEnds()
        {
            yield return OpenKitchenInService();
            // A chef at the board with the knife: the cutting dial is up.
            var board = Services.Stations.All.OfType<PrepStation>().First();
            var knife = Services.Stations.All.OfType<CounterStation>().First(c => c.Slot.Current is Knife);
            Assert.That(knife.TryExchangeTool(_chef), Is.True);
            var onion = Services.Cooking.AddIngredient(Services.Catalog.Find("Onion").ToProperties());
            board.Container.Put(onion);
            Services.ItemViews.PlaceImmediately(onion, board.AnchorFor(onion));
            yield return StandAt(board);
            yield return Frames(5);
            var gate = Object.FindFirstObjectByType<ReviewHidesKitchenHud>();
            Assert.That(gate, Is.Not.Null);
            Assert.That(gate.Hidden, Is.False);
            Assert.That(Dials(), Is.EqualTo(1), "the dial is up while cutting");

            Services.Days.FinishService(ServiceSummary.From(Services.Service), wages: 0f);
            yield return Frames(5);

            var dashboard = Object.FindFirstObjectByType<ReviewDashboard>();
            Assert.That(dashboard.IsShowing, Is.True);
            Assert.That(gate.Hidden, Is.True, "the kitchen HUD is hidden behind the dashboard");
            Assert.That(Dials(), Is.Zero, "the cutting dial ended");
            var hudCanvas = gate.GetComponent<Canvas>();
            var dashboardCanvas = dashboard.GetComponentsInChildren<Canvas>(true).First(c => c.name == "ReviewDashboard");
            Assert.That(dashboardCanvas.sortingOrder, Is.GreaterThan(hudCanvas.sortingOrder), "the dashboard sorts over the kitchen HUD");
            Assert.That(dashboardCanvas.GetComponent<CanvasGroup>().ignoreParentGroups, Is.True, "hiding the HUD doesn't hide the dashboard");
        }

        private static int Dials() => Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
            .Count(r => r.name.StartsWith("ChopDial_") && r.gameObject.activeInHierarchy);
    }
}
