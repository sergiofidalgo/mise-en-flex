using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Stations;
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
    /// Captures of the kitchen's contextual hints for design review: a searing steak at the hob, the
    /// fridge with RT held, and a stain on the floor. Run by name with CONTEXT_CAPTURE set to a folder.
    /// </summary>
    [Explicit("Captures for design review; run by name.")]
    public class ContextHintsCapture
    {
        private KitchenCompositionRoot _root;
        private PlayerInteractor _chef;
        private Gamepad _pad;
        private KitchenServices Services => _root.Services;

        private IEnumerator Open()
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _pad = InputSystem.AddDevice<Gamepad>("CapturePad");
            var player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Gamepad", pairWithDevices: _pad);
            yield return null;
            yield return null;
            _chef = player.GetComponent<PlayerInteractor>();
            if (Services.Days.Phase == DayPhase.Plan)
                Services.Days.Advance();
        }

        [TearDown]
        public void Release()
        {
            Time.timeScale = 1f;
            if (_pad != null)
                InputSystem.RemoveDevice(_pad);
        }

        private void StandAt(Interactable station)
        {
            var front = Services.Messes.InFront(station);
            _chef.GetComponent<PlayerMovement>().Teleport(new System.Numerics.Vector2(front.x, front.z));
            var toward = station.transform.position - _chef.transform.position;
            toward.y = 0f;
            _chef.transform.rotation = Quaternion.LookRotation(toward, Vector3.up);
        }

        private static IEnumerator Wait(float seconds)
        {
            var until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
                yield return null;
        }

        [UnityTest]
        public IEnumerator CaptureTheHints()
        {
            yield return Open();

            var hob = Services.Stations.All.OfType<HobStation>().First(h => h.Hob.Pan != null);
            var steak = (Ingredient)Services.CreateItem("Steak");
            hob.Hob.Pan.Put(steak);
            Services.ItemViews.PlaceImmediately(steak, hob.ItemAnchor);
            hob.Hob.Dial = 0.8f;
            StandAt(hob);
            yield return Wait(1.5f);
            Capture("hob_searing");

            var fridge = Services.Stations.All.OfType<StorageStation>().First();
            StandAt(fridge);
            yield return Wait(0.6f);
            Capture("storage_facing");
            var until = Time.realtimeSinceStartup + 0.8f;
            while (Time.realtimeSinceStartup < until)
            {
                InputSystem.QueueStateEvent(_pad, new GamepadState { rightTrigger = 1f });
                yield return null;
            }
            Capture("storage_browsing");
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;

            // A spill on open floor, the chef a step away facing it.
            var at = new Vector3(0.6f, 0f, -1.6f);
            Services.Mess.PlateBroken(at.x, at.z);
            _chef.GetComponent<PlayerMovement>().Teleport(new System.Numerics.Vector2(at.x, at.z - 0.75f));
            _chef.transform.rotation = Quaternion.identity;
            yield return Wait(0.6f);
            Capture("floor_stain");

            // Every kind of floor stain side by side, close up: greasy, not flat.
            Services.Mess.PlateBroken(-1.6f, -1.9f);
            Services.Mess.PanBurnt(null, -0.6f, -1.9f);
            Services.Mess.FireOut(0.4f, -1.9f);
            for (var i = 0; i < 6; i++)
                Services.Mess.GuestWalked(1.4f, -1.9f);
            _chef.GetComponent<PlayerMovement>().Teleport(new System.Numerics.Vector2(3f, -3f));
            yield return Wait(0.4f);
            Capture("stains");

            // A spill on the floor just behind the island, hidden from the camera by the counter.
            var island = Services.Stations.All.OfType<CounterStation>().OrderBy(c => c.transform.position.sqrMagnitude).First();
            var behind = island.transform.position + new Vector3(0f, 0f, 0.95f);
            Services.Mess.PlateBroken(behind.x, behind.z);
            _chef.GetComponent<PlayerMovement>().Teleport(new System.Numerics.Vector2(behind.x - 0.8f, behind.z));
            _chef.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            yield return Wait(0.6f);
            Capture("behind_counter");

            var table = Services.Stations.All.OfType<TableStation>().First();
            var plate = table.ItemAnchor != null ? table.ItemAnchor.position : table.transform.position;
            Services.Messes.DinerFinished(table, plate);
            StandAt(table);
            yield return Wait(0.6f);
            Capture("table_dirty");

            var sink = Services.Stations.All.OfType<SinkStation>().First();
            var dirty = new Game.Simulation.Items.Plate(3);
            dirty.MarkDirty();
            sink.Sink.Put(dirty);
            StandAt(sink);
            yield return Wait(0.6f);
            Capture("sink_dirty");
        }

        private static void Capture(string name)
        {
            Debug.Log($"[ContextCapture] {name} at {Time.realtimeSinceStartup:0.0}s");
            var folder = Environment.GetEnvironmentVariable("CONTEXT_CAPTURE");
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
            File.WriteAllBytes(Path.Combine(folder, $"context_{name}.png"), image.EncodeToPNG());
            Object.Destroy(image);
        }
    }
}
