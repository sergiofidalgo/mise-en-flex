using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Simulation.Cooking;
using Game.Simulation.Days;
using Game.Simulation.Items;
using Game.Simulation.Service;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Not a test: the kitchen's HUD in service, as players see it (the 2026-11-11 HUD clean-up):
    /// one chef at a gas hob with a steak, one at the board with the knife, one facing a fridge,
    /// guests seated; then the day ending over all of that, as the dashboard opens. Explicit, so it
    /// only runs when asked for by name:
    /// Unity -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.KitchenHudCapture [-captureFolder folder]
    /// (no -batchmode: it grabs the screen, overlay canvases and all).
    /// </summary>
    [Explicit("Screenshots of the kitchen HUD; run it by name.")]
    public class KitchenHudCapture
    {
        [UnityTest]
        public IEnumerator CaptureTheKitchenHud()
        {
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            Assume.That(Application.isBatchMode, Is.False, "Run it in a visible editor (without -batchmode): it grabs the screen.");
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            var services = root.Services;
            var manager = Object.FindFirstObjectByType<PlayerInputManager>();
            var pads = Enumerable.Range(0, 3).Select(i => InputSystem.AddDevice<Gamepad>($"HudCapturePad{i}")).ToList();
            var chefs = pads.Select(pad => manager.JoinPlayer(controlScheme: "Gamepad", pairWithDevices: pad).GetComponent<PlayerInteractor>()).ToList();
            for (var i = 0; i < 5; i++)
                yield return null;

            while (services.Days.Phase != DayPhase.Service)
                services.Days.Advance();
            yield return null;

            // Guests: enough to fill a few seats.
            var service = services.Service;
            service.GuestsWalkIn = false; // straight to their seats: the street isn't what's shown here
            for (var i = 0; i < 4; i++)
            {
                var count = service.Customers.Count;
                while (service.Customers.Count == count)
                    service.Tick(1f);
            }

            // A steak on a gas hob, the heat up; a chef facing it.
            var hob = services.Stations.All.OfType<HobStation>().First(h => h.Kind == HeatKind.Gas);
            var steak = services.Cooking.AddIngredient(services.Catalog.Find("Steak").ToProperties());
            hob.Hob.Pan.Put(steak);
            hob.Hob.Dial = 0.7f;
            StandAt(services, chefs[0], hob);

            // The knife and an onion at the board; a chef facing it.
            var board = services.Stations.All.OfType<PrepStation>().First();
            var knife = services.Stations.All.OfType<CounterStation>().FirstOrDefault(c => c.Slot.Current is Knife);
            if (knife != null)
                knife.TryExchangeTool(chefs[1]);
            var onion = services.Cooking.AddIngredient(services.Catalog.Find("Onion").ToProperties());
            board.Container.Put(onion);
            services.ItemViews.PlaceImmediately(onion, board.AnchorFor(onion));
            StandAt(services, chefs[1], board);

            // Empty-handed at a fridge.
            StandAt(services, chefs[2], services.Stations.All.OfType<StorageStation>().First());

            var folder = Folder();
            yield return new WaitForSeconds(12f); // the guests walk in and sit; the steak browns
            yield return Shoot(Path.Combine(folder, "kitchen_service.png"));

            // The day ends with all of that still going: the dashboard opens over it.
            services.Days.FinishService(ServiceSummary.From(service), wages: 0f);
            for (var i = 0; i < 60; i++)
                yield return null;
            yield return Shoot(Path.Combine(folder, "dashboard_opens.png"));

            foreach (var pad in pads)
                InputSystem.RemoveDevice(pad);
            Debug.Log($"[KitchenHudCapture] Saved screenshots to {folder}");
        }

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
            var folder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.GetFullPath("Screenshots/KitchenHud");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
