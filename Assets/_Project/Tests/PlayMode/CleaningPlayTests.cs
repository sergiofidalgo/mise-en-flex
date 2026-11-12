using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Cleaning;
using Game.Gameplay.Stations;
using Game.Simulation.Cleaning;
using Game.Simulation.Cooking;
using Game.Simulation.Days;
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
    /// Dirt, cleaning and gas fires in the real kitchen (Phase 5, decision 2026-11-10), played with a
    /// controller chef: hold X and the left stick traces the shape. With the environment
    /// variable CLEANING_CAPTURE set to a folder, it also saves what the camera saw.
    /// </summary>
    public class CleaningPlayTests
    {
        private const string SceneName = "Kitchen_Greybox";

        private KitchenCompositionRoot _root;
        private PlayerInteractor _chef;
        private Gamepad _pad;

        private KitchenServices Services => _root.Services;

        private InputSettings.BackgroundBehavior _background;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInput;

        private IEnumerator OpenKitchenInPrep()
        {
            // Run from the command line, the editor has no focus: let the simulated keys through anyway.
            _background = InputSystem.settings.backgroundBehavior;
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            // A controller, the game's main input: its left stick can follow the track as a thumb would.
            _pad = InputSystem.AddDevice<Gamepad>("CleaningTestPad");
            var player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Gamepad", pairWithDevices: _pad);
            yield return null;
            yield return null;
            _chef = player.GetComponent<PlayerInteractor>();
            if (Services.Days.Phase == DayPhase.Plan)
                Services.Days.Advance();
            Assume.That(Services.Days.Phase, Is.EqualTo(DayPhase.Prep));
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

        // Stands the chef where someone would, in front of the station, facing it.
        private void StandAt(Interactable station)
        {
            var front = Services.Messes.InFront(station);
            // A CharacterController can't be moved by setting its transform.
            _chef.GetComponent<PlayerMovement>().Teleport(new System.Numerics.Vector2(front.x, front.z));
            var toward = station.transform.position - _chef.transform.position;
            toward.y = 0f;
            _chef.transform.rotation = Quaternion.LookRotation(toward, Vector3.up);
        }

        // Holds X and works the left stick as a player following the hint would: round and round
        // for the cloth and mop, side to side for the extinguisher, a loop a second, until done()
        // or the time runs out (decision 2026-11-11: spin and sweep).
        private IEnumerator HoldXAndTrace(Func<bool> done, float seconds, string captureAs = null)
        {
            var frames = 0;
            var began = Time.time;
            var captured = false;
            var until = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < until)
            {
                var stick = Vector2.zero;
                var game = _chef.Cleaning != null ? _chef.Cleaning.Game : null;
                if (game != null)
                {
                    // A loop a second of game time, as a person would, whatever the frame rate.
                    var next = TraceGame.PointAt(game.Shape, Time.time - began);
                    stick = new Vector2(next.X, next.Y);
                    // The stick's default dead zone (0.125 to 0.925) rescales what's pushed: push so
                    // the game reads the point itself.
                    if (stick.sqrMagnitude > 1e-6f)
                        stick = stick.normalized * (0.125f + stick.magnitude * 0.8f);
                }
                InputSystem.QueueStateEvent(_pad, new GamepadState { leftStick = stick }.WithButton(GamepadButton.West));
                frames++;
                // Mid-job, by time so it's the same moment at any frame rate.
                if (captureAs != null && !captured && Time.time - began >= 1.2f)
                {
                    captured = true;
                    Capture(captureAs);
                }
                yield return null;
            }
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator ABurntPanOnGasCatchesLocksTheHobAndIsPutOutWithTheExtinguisher()
        {
            yield return OpenKitchenInPrep();
            var hob = Services.Stations.All.OfType<HobStation>().First(h => h.Kind == HeatKind.Gas);
            var steak = (Ingredient)Services.CreateItem("Steak");
            hob.Hob.Pan.Put(steak);
            Services.ItemViews.PlaceImmediately(steak, hob.ItemAnchor);
            hob.Hob.Dial = 1f;

            // Faster time burns it sooner; the fire's seconds speed up with it.
            Time.timeScale = 8f;
            var until = Time.realtimeSinceStartup + 40f;
            while (!hob.IsOnFire && Time.realtimeSinceStartup < until)
                yield return null;
            Time.timeScale = 1f;

            Assert.That(hob.IsOnFire, Is.True, "the steak burnt and the pan caught");
            Assert.That(hob.Hob.Pan.Contents, Is.Empty, "the food is lost");
            Assert.That(Services.Cooking.Contains(steak), Is.False);
            Assert.That(hob.Container, Is.Null, "nothing can be taken from or put on a hob on fire");
            hob.Hob.Dial = 0f;
            for (var i = 0; i < 10; i++)
                yield return null;
            Assert.That(hob.IsOnFire, Is.True, "turning the gas off doesn't put it out");
            // The fire sign has popped in over the hob (the user's call, 2026-11-11).
            var settle = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < settle)
                yield return null;
            var sign = hob.transform.Find("FireSign");
            Assert.That(sign != null && sign.gameObject.activeInHierarchy, Is.True, "a fire sign floats over the burning hob");
            Capture("fire");

            StandAt(hob);
            yield return null;
            yield return HoldXAndTrace(() => !hob.IsOnFire, 25f, "extinguish");

            Assert.That(hob.IsOnFire, Is.False, "three sweeps put it out");
            var gone = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < gone)
                yield return null;
            Assert.That(sign.gameObject.activeInHierarchy, Is.False, "the sign goes when the fire is out");
            Assert.That(_chef.IsBusy, Is.False);
            Assert.That(Services.Dirt.Spots.Any(s => s.Kind == DirtKind.Foam), Is.True, "foam is left on the floor");
            Assert.That(Services.Dirt.Spots.Any(s => s.Kind == DirtKind.Scorch), Is.True, "and soot from the burnt pan");
            yield return null;
            // Step back to see what's left.
            var back = _chef.transform.position - _chef.transform.forward * 1.2f;
            _chef.GetComponent<PlayerMovement>().Teleport(new System.Numerics.Vector2(back.x, back.z));
            yield return null;
            yield return null;
            Capture("after_fire");

            // For the eye only: one of each kind on open floor, and a guest's crumbs on a table.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CLEANING_CAPTURE")))
            {
                Services.Mess.PlateBroken(-1.5f, -1.6f);
                Services.Mess.PanBurnt(null, -0.2f, -1.6f);
                Services.Mess.FireOut(1.0f, -1.6f);
                for (var i = 0; i < 6; i++)
                    Services.Mess.GuestWalked(2.2f, -1.4f);
                var table = Services.Stations.All.OfType<TableStation>().First();
                Services.Messes.DinerFinished(table, table.ItemAnchor != null ? table.ItemAnchor.position : table.transform.position);
                yield return null;
                yield return null;
                Capture("every_kind");
            }
        }

        [UnityTest]
        public IEnumerator HoldingXByASpillMopsItAwayAndLettingGoStops()
        {
            yield return OpenKitchenInPrep();
            var counter = Services.Stations.All.OfType<CounterStation>().First(c => c.IsClear);
            StandAt(counter);
            var feet = _chef.transform.position + _chef.transform.forward * 0.4f;
            var spill = Services.Mess.PlateBroken(feet.x, feet.z);
            yield return null;

            // A moment of tracing, then let go: partly mopped, and the chef is free again.
            yield return HoldXAndTrace(() => false, 1.2f, "mop");
            Assert.That(Services.Dirt.Spots, Has.Member(spill));
            Assert.That(spill.Remaining, Is.LessThan(1f), "some of it is mopped");
            Assert.That(_chef.IsBusy, Is.False, "letting go of X stops");

            yield return HoldXAndTrace(() => !Services.Dirt.Spots.Contains(spill), 20f);
            Assert.That(Services.Dirt.Spots, Has.No.Member(spill), "mopped clean");
            var view = Object.FindFirstObjectByType<Game.Presentation.CleaningView>();
            Assert.That(view.GetComponentsInChildren<Renderer>().Any(r => r.transform.parent != null && r.transform.parent.name.StartsWith("Tool_")), Is.False, "the mop goes away with the spill");
            // The ring flashes as the spill comes clean, as a plate does at the sink, then goes.
            var flashed = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < flashed)
                yield return null;
            Assert.That(view.GetComponentsInChildren<Renderer>().Length, Is.Zero, "the ring goes after its flash");
        }

        [UnityTest]
        public IEnumerator ADirtyTableComesOffTheGuestsScoreAndTheClothWipesIt()
        {
            yield return OpenKitchenInPrep();
            var table = Services.Stations.All.OfType<TableStation>().First();
            var plate = table.ItemAnchor != null ? table.ItemAnchor.position : table.transform.position;
            Services.Messes.DinerFinished(table, plate);
            Services.Messes.DinerFinished(table, plate);
            var spot = Services.Dirt.On(table).Single();
            Assert.That(Services.Dirt.PenaltyFor(table, plate.x, plate.z), Is.GreaterThan(0f));

            StandAt(table);
            yield return null;
            yield return HoldXAndTrace(() => !Services.Dirt.Spots.Contains(spot), 20f, "cloth");
            Assert.That(Services.Dirt.On(table), Is.Empty, "wiped clean");
        }

        // Holds X and circles the left stick, `loops` a second, for some seconds or until done().
        private IEnumerator HoldXAndCircle(Func<bool> done, float seconds, float loops = 1.2f)
        {
            var began = Time.time;
            var until = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < until)
            {
                var angle = (Time.time - began) * loops * Mathf.PI * 2f;
                var stick = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.85f;
                InputSystem.QueueStateEvent(_pad, new GamepadState { leftStick = stick }.WithButton(GamepadButton.West));
                yield return null;
            }
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator HoldingXAtTheSinkAndCirclingTheStickWashesAPlate()
        {
            yield return OpenKitchenInPrep();
            var sink = Services.Stations.All.OfType<SinkStation>().First();
            var plate = new Game.Simulation.Items.Plate(3);
            plate.MarkDirty();
            sink.Sink.Put(plate);
            StandAt(sink);
            yield return null;
            yield return null;
            Assume.That(_chef.Target, Is.EqualTo(sink));
            var start = _chef.transform.position;

            // A moment of circling, then let go: partly washed, and the chef is free again.
            yield return HoldXAndCircle(() => false, 1f);
            Assert.That(sink.Sink.WashProgress, Is.GreaterThan(0.1f), "circling washes");
            Assert.That(sink.Game, Is.Null, "letting go of X stops washing");
            Assert.That(_chef.IsBusy, Is.False);

            yield return HoldXAndCircle(() => _chef.Hands.Current == plate, 10f);
            Assert.That(_chef.Hands.Current, Is.SameAs(plate), "the clean plate comes out into the chef's hands");
            Assert.That(plate.IsDirty, Is.False);
            var moved = _chef.transform.position - start;
            moved.y = 0f;
            Assert.That(moved.magnitude, Is.LessThan(0.05f), "the chef stays planted while washing");
        }

        [UnityTest]
        public IEnumerator HoldingXWithNothingToCleanPlantsTheChefAndTheStickTurnsThem()
        {
            yield return OpenKitchenInPrep();
            var counter = Services.Stations.All.OfType<CounterStation>().First(c => c.IsClear);
            StandAt(counter);
            yield return null;
            Assume.That(Services.Dirt.On(counter), Is.Empty);
            var start = _chef.transform.position;
            // X held, the stick pushed away from the counter: the chef turns round where they stand.
            var back = _chef.transform.eulerAngles.y + 180f;
            var away = new Vector2(Mathf.Sin(back * Mathf.Deg2Rad), Mathf.Cos(back * Mathf.Deg2Rad));
            // Game time, not frames: batch-mode frames can be very short.
            var turned = Time.time + 0.6f;
            while (Time.time < turned)
            {
                InputSystem.QueueStateEvent(_pad, new GamepadState { leftStick = away }.WithButton(GamepadButton.West));
                yield return null;
            }
            var moved = _chef.transform.position - start;
            moved.y = 0f;
            Assert.That(_chef.IsPlanted, Is.True);
            Assert.That(moved.magnitude, Is.LessThan(0.05f), "planted");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(_chef.transform.eulerAngles.y, back)), Is.LessThan(10f), "turned round towards the stick");

            // Let go of X: the stick walks again.
            var walked = Time.time + 0.3f;
            while (Time.time < walked)
            {
                InputSystem.QueueStateEvent(_pad, new GamepadState { leftStick = away });
                yield return null;
            }
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
            moved = _chef.transform.position - start;
            moved.y = 0f;
            Assert.That(_chef.IsPlanted, Is.False);
            Assert.That(moved.magnitude, Is.GreaterThan(0.2f), "free to walk");
        }

        [UnityTest]
        public IEnumerator HoldingXTurningOntoDirtStartsWipingIt()
        {
            yield return OpenKitchenInPrep();
            var table = Services.Stations.All.OfType<TableStation>().First();
            var plate = table.ItemAnchor != null ? table.ItemAnchor.position : table.transform.position;
            Services.Messes.DinerFinished(table, plate);
            StandAt(table);
            // Facing away from the table, X held: nothing to clean, so the chef is planted.
            var away = _chef.transform.eulerAngles.y + 180f;
            _chef.transform.rotation = Quaternion.Euler(0f, away, 0f);
            yield return null;
            var toward = table.transform.position - _chef.transform.position;
            var stick = new Vector2(toward.x, toward.z).normalized;
            var until = Time.realtimeSinceStartup + 3f;
            while ((_chef.Cleaning == null || !_chef.Cleaning.IsCleaning) && Time.realtimeSinceStartup < until)
            {
                InputSystem.QueueStateEvent(_pad, new GamepadState { leftStick = stick * 0.9f }.WithButton(GamepadButton.West));
                yield return null;
            }
            Assert.That(_chef.Cleaning.IsCleaning, Is.True, "turning onto the dirt with X held brings out the cloth");
            Assert.That(_chef.Cleaning.Tool, Is.EqualTo(CleaningTool.Cloth));
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
        }

        // What the camera sees, when CLEANING_CAPTURE names a folder.
        private static void Capture(string name)
        {
            var folder = Environment.GetEnvironmentVariable("CLEANING_CAPTURE");
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
            File.WriteAllBytes(Path.Combine(folder, $"cleaning_{name}.png"), image.EncodeToPNG());
            Object.Destroy(image);
        }
    }
}
