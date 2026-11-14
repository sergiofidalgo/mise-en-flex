using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Presentation;
using Game.Presentation.Hud;
using Game.Simulation.Cooking;
using Game.Simulation.Days;
using Game.Simulation.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Not a test: the world-anchored HUD at each of the camera's shots (decision 2026-11-14), over
    /// a busy service: the dining room full and two queueing, a steak on every hob with nobody at
    /// most of them, one chef at a hob, one cutting, and one guest nearly out of patience. The camera
    /// is held at each height over the restaurant's middle (its own framing is switched off), long
    /// enough for the bands' fades to finish. Explicit, so it only runs when asked for by name:
    /// Unity -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.HudZoomCapture [-captureFolder folder]
    /// (no -batchmode: it grabs the screen, overlay canvases and all).
    /// </summary>
    [Explicit("Screenshots of the HUD at each camera height; run it by name.")]
    public class HudZoomCapture
    {
        // The kitchen's shot, the whole restaurant, the street and the road (decisions 2026-11-08 to 14).
        private static readonly float[] Heights = { 7.9f, 9.75f, 14.6f, 23.7f };

        [UnityTest]
        public IEnumerator CaptureTheHudAtEachHeight()
        {
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            Assume.That(Application.isBatchMode, Is.False, "Run it in a visible editor (without -batchmode): it grabs the screen.");
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            var services = root.Services;
            var manager = Object.FindFirstObjectByType<PlayerInputManager>();
            var pads = Enumerable.Range(0, 2).Select(i => InputSystem.AddDevice<Gamepad>($"HudZoomPad{i}")).ToList();
            var chefs = pads.Select(pad => manager.JoinPlayer(controlScheme: "Gamepad", pairWithDevices: pad).GetComponent<PlayerInteractor>()).ToList();
            for (var i = 0; i < 5; i++)
                yield return null;

            // Two tables for four in the dining room, bought in Plan: a busy room needs tables.
            var editor = Object.FindFirstObjectByType<LayoutEditor>();
            var buyer = chefs[0].GetComponent<PlayerInput>();
            var cursor = editor.Enter(buyer);
            services.Days.Receive(1000f);
            var placed = 0;
            foreach (var spot in new[] { new System.Numerics.Vector2(-2.2f, -5.6f), new System.Numerics.Vector2(1.5f, -5.6f), new System.Numerics.Vector2(1.5f, -7f), new System.Numerics.Vector2(0f, -7.5f), new System.Numerics.Vector2(2.5f, -7.5f) })
            {
                if (placed == 2)
                    break;
                cursor.Position = spot;
                if (!editor.Buy(cursor, editor.Shop.First(e => e.Id == "table")))
                    continue;
                editor.MoveCursor(cursor, spot);
                if (!editor.Place(cursor))
                {
                    Debug.Log($"[HudZoomCapture] couldn't place a table at {spot}: {cursor.Result}");
                    continue;
                }
                placed++;
                for (var i = 0; i < 3; i++)
                    editor.AddChair(cursor);
            }
            editor.Leave(buyer);
            yield return null;

            while (services.Days.Phase != DayPhase.Service)
                services.Days.Advance();
            yield return null;

            var service = services.Service;
            service.GuestsWalkIn = false;
            for (var i = 0; i < service.SeatCount + 2; i++)
            {
                var count = service.Customers.Count;
                while (service.Customers.Count == count)
                    service.Tick(1f);
            }

            // A steak on every hob; a chef at the first, the others left alone.
            var hobs = services.Stations.All.OfType<HobStation>().ToList();
            foreach (var hob in hobs)
            {
                if (hob.Hob.Pan == null)
                    continue;
                var steak = (Ingredient)services.CreateItem("Steak");
                hob.Hob.Pan.Put(steak);
                services.ItemViews.PlaceImmediately(steak, hob.ItemAnchor);
                hob.Hob.Dial = 0.35f;
            }
            // Nobody at the hob: its strip is everyone's, so it becomes a chip further up.
            StandAt(services, chefs[0], services.Stations.All.OfType<StorageStation>().First());

            var board = services.Stations.All.OfType<PrepStation>().First();
            var knife = services.Stations.All.OfType<CounterStation>().FirstOrDefault(c => c.Slot.Current is Knife);
            if (knife != null)
                knife.TryExchangeTool(chefs[1]);
            var onion = (Ingredient)services.CreateItem("Onion");
            board.Container.Put(onion);
            services.ItemViews.PlaceImmediately(onion, board.AnchorFor(onion));
            // Cutting stays with the board, but the chef steps to the sink: the board is within
            // reach of the hob, and a chef there makes its strip theirs (full at every height).
            chefs[1].GetComponent<PlayerMovement>().Teleport(new System.Numerics.Vector2(0.5f, -6.5f));

            yield return new WaitForSeconds(10f); // the guests sit and order; the steaks brown
            foreach (var chef in chefs)
                Debug.Log($"[HudZoomCapture] chef {Vector3.Distance(Flat(chef.transform.position), Flat(hobs[0].transform.position)):0.00} m from the hob");
            Debug.Log($"[HudZoomCapture] {service.SeatCount} seats, {service.Customers.Count} guests, {hobs.Count} hobs, {services.Cooking.Pans.Count()} pans");

            var camera = Camera.main;
            camera.GetComponent<FramingCamera>().enabled = false;
            var floor = services.FloorPlan;
            var middle = new Vector3((floor.RoomMin.X + floor.RoomMax.X) / 2f, 0f, (floor.RoomMin.Y + floor.RoomMax.Y) / 2f);
            var patience = typeof(Game.Simulation.Service.Customer).GetProperty("Patience");
            var orders = Object.FindFirstObjectByType<OrderTicketHud>();
            var strips = Object.FindFirstObjectByType<FlipCrustGaugeView>();
            var folder = Folder();
            foreach (var height in Heights)
            {
                // Over the restaurant's middle at this height, the usual tilt.
                camera.transform.position = middle - camera.transform.forward * (height / Mathf.Max(0.01f, -camera.transform.forward.y));
                // One seated guest nearly out of patience: their bubble stays full at every height.
                var impatient = service.Customers.FirstOrDefault(c => c.State == Game.Simulation.Service.CustomerState.Waiting);
                for (var t = 0f; t < 1.2f; t += Time.unscaledDeltaTime)
                {
                    if (impatient != null)
                        patience.SetValue(impatient, 0.2f);
                    yield return null;
                }
                var file = Path.Combine(folder, $"hud_{height:0.0#}m.png");
                yield return Shoot(file);
                Debug.Log($"[HudZoomCapture] {height} m: {orders.Placed.Count} bubbles placed, {orders.TablesShowing} table bubbles, {strips.Showing} strips ({strips.ShowingAsChips} as chips) -> {file}");
            }

            foreach (var pad in pads)
                InputSystem.RemoveDevice(pad);
            Debug.Log($"[HudZoomCapture] Saved screenshots to {folder}");
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static void StandAt(KitchenServices services, PlayerInteractor chef, Interactable station)
        {
            var front = services.Messes.InFront(station);
            chef.GetComponent<PlayerMovement>().Teleport(new System.Numerics.Vector2(front.x, front.z));
            var toward = station.transform.position - chef.transform.position;
            toward.y = 0f;
            chef.transform.rotation = Quaternion.LookRotation(toward, Vector3.up);
        }

        private static IEnumerator Shoot(string file)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(file, frame.EncodeToPNG());
            Object.Destroy(frame);
        }

        private static string Folder()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureFolder");
            var folder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.GetFullPath("Screenshots/HudZoom");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
