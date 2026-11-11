using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Presentation;
using Game.Simulation.Cooking;
using Game.Simulation.Days;
using Game.Simulation.Items;
using Game.Simulation.Prep;
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
    /// The timing gauges in the real kitchen (decision 2026-11-11): the flip's crust strip and the
    /// cutting dial, played with a controller chef pressing X. With TIMING_CAPTURE set to a folder,
    /// it also saves what the camera saw at each moment.
    /// </summary>
    public class TimingGaugePlayTests
    {
        private const string SceneName = "Kitchen_Greybox";

        private KitchenCompositionRoot _root;
        private PlayerInteractor _chef;
        private Gamepad _pad;
        private InputSettings.BackgroundBehavior _background;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInput;

        private KitchenServices Services => _root.Services;

        private IEnumerator OpenKitchenInPrep()
        {
            _background = InputSystem.settings.backgroundBehavior;
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _pad = InputSystem.AddDevice<Gamepad>("TimingTestPad");
            var player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Gamepad", pairWithDevices: _pad);
            yield return null;
            yield return null;
            _chef = player.GetComponent<PlayerInteractor>();
            if (Services.Days.Phase == DayPhase.Plan)
                Services.Days.Advance();
            Assume.That(Services.Days.Phase, Is.EqualTo(DayPhase.Prep));
            Assume.That(Object.FindFirstObjectByType<FlipCrustGaugeView>(), Is.Not.Null, "run Tools/Game/Apply Timing Gauges (or rebuild the kitchen) first");
        }

        [TearDown]
        public void Release()
        {
            Time.timeScale = 1f;
            if (_pad != null)
                InputSystem.RemoveDevice(_pad);
            _pad = null;
            InputSystem.settings.backgroundBehavior = _background;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
        }

        private void StandAt(Interactable station)
        {
            var front = Services.Messes.InFront(station);
            _chef.GetComponent<PlayerMovement>().Teleport(new System.Numerics.Vector2(front.x, front.z));
            var toward = station.transform.position - _chef.transform.position;
            toward.y = 0f;
            _chef.transform.rotation = Quaternion.LookRotation(toward, Vector3.up);
        }

        private IEnumerator PressX()
        {
            InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.West));
            yield return null;
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
        }

        private static IEnumerator Wait(float seconds)
        {
            var until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
                yield return null;
        }

        [UnityTest]
        public IEnumerator TheCrustStripShowsUnderASearingSteakAndGradesAGoldenFlip()
        {
            yield return OpenKitchenInPrep();
            var view = Object.FindFirstObjectByType<FlipCrustGaugeView>();
            var hob = Services.Stations.All.OfType<HobStation>().First(h => h.Hob.Pan != null);
            var steak = (Ingredient)Services.CreateItem("Steak");
            hob.Hob.Pan.Put(steak);
            Services.ItemViews.PlaceImmediately(steak, hob.ItemAnchor);
            hob.Hob.Dial = 0.8f;
            StandAt(hob);
            yield return Wait(0.5f);
            Assert.That(view.Showing, Is.EqualTo(1), "a strip under the steak");
            Capture("flip_raw");

            // Sear faster until the marker's in the golden window, then flip.
            var settings = Services.Tuning.FlipGauge;
            Time.timeScale = 6f;
            var until = Time.realtimeSinceStartup + 40f;
            while (steak.Browning(steak.DownSide) < (settings.GoodFrom + settings.GoodTo) * 0.5f && Time.realtimeSinceStartup < until)
                yield return null;
            Time.timeScale = 1f;
            Assert.That(FlipCrustGaugeView.GradeAt(steak.Browning(steak.DownSide), settings), Is.EqualTo(PressGrade.Good));
            yield return null;
            Capture("flip_window");

            yield return PressX();
            Assert.That(steak.HasBeenFlipped, Is.True, "X flips the steak");
            Capture("flip_toss");
            yield return Wait(0.25f);
            Assert.That(view.LastFlipGrade, Is.EqualTo(PressGrade.Good), "flipped golden");
            Capture("flip_mark");
            yield return Wait(1.5f);
            Capture("flip_doneness");
            Assert.That(view.Showing, Is.EqualTo(1), "the strip shows the doneness after the flip");
        }

        [UnityTest]
        public IEnumerator TheCuttingDialStandsOverTheBoardAndFillsAPipPerCut()
        {
            yield return OpenKitchenInPrep();
            Services.Stations.All.OfType<CounterStation>().Single(c => c.Slot.Current is Knife).TryExchangeTool(_chef);
            var board = Services.Stations.All.OfType<PrepStation>().First();
            var onion = Services.Cooking.AddIngredient(Services.Catalog.Find("Onion").ToProperties());
            board.Container.Put(onion);
            Services.ItemViews.PlaceImmediately(onion, board.AnchorFor(onion));
            StandAt(board);
            yield return Wait(0.4f);
            var view = Object.FindFirstObjectByType<ChopGaugeView>();
            Assert.That(view.Showing, Is.EqualTo(1), "the dial stands over the board");
            Capture("chop_start");

            // Cut as the knife reaches the green, as a player would.
            var dial = board.Counter.Dial;
            for (var cut = 0; cut < 3 && board.Counter.Dial == dial; cut++)
            {
                var until = Time.realtimeSinceStartup + 5f;
                while (dial.GradeAt(Time.unscaledTimeAsDouble + 0.02) != ChopGrade.Good && Time.realtimeSinceStartup < until)
                    yield return null;
                yield return PressX();
                if (cut == 0)
                    Capture("chop_cut");
                yield return Wait(0.3f);
            }
            Assert.That(dial.Hits, Is.GreaterThanOrEqualTo(2), "X cuts at the board");
            Capture("chop_pips");
        }

        [UnityTest]
        public IEnumerator ABoardInTheMiddleOfTheKitchenKeepsItsDialSmall()
        {
            yield return OpenKitchenInPrep();
            Services.Stations.All.OfType<CounterStation>().Single(c => c.Slot.Current is Knife).TryExchangeTool(_chef);
            // The worst case: the board swapped onto the middle of the island.
            var board = Services.Stations.All.OfType<PrepStation>().First();
            var middle = Services.Stations.All.OfType<CounterStation>().OrderBy(c => c.transform.position.sqrMagnitude).First();
            var boardAt = board.transform.position;
            board.transform.SetPositionAndRotation(middle.transform.position, middle.transform.rotation);
            middle.transform.position = boardAt;
            var onion = Services.Cooking.AddIngredient(Services.Catalog.Find("Onion").ToProperties());
            board.Container.Put(onion);
            Services.ItemViews.PlaceImmediately(onion, board.AnchorFor(onion));
            var front = board.transform.position - board.transform.forward * 1.0f;
            _chef.GetComponent<PlayerMovement>().Teleport(new System.Numerics.Vector2(front.x, front.z));
            _chef.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(board.transform.position - front, Vector3.up), Vector3.up);
            yield return Wait(0.5f);
            Assume.That(_chef.Target, Is.EqualTo(board));
            Assert.That(Object.FindFirstObjectByType<ChopGaugeView>().Showing, Is.EqualTo(1));
            Capture("chop_middle");
        }

        // What the camera sees, when TIMING_CAPTURE names a folder.
        private static void Capture(string name)
        {
            var folder = Environment.GetEnvironmentVariable("TIMING_CAPTURE");
            var camera = Camera.main;
            if (string.IsNullOrEmpty(folder) || camera == null)
                return;
            Directory.CreateDirectory(folder);
            var target = RenderTexture.GetTemporary(1920, 1080, 24);
            var previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = previous;
            var active = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            File.WriteAllBytes(Path.Combine(folder, $"timing_{name}.png"), image.EncodeToPNG());
            Object.Destroy(image);
        }
    }
}
