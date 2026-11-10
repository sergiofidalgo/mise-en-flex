using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Stations;
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
    /// Not a test: screenshots of a chef buying at the street's stalls (decision 2026-11-10). They show
    /// the stall's menu open, then the moments after buying while the feedback plays: the storage that
    /// took the stock hopping, the fridge lid popping with mist, and the money flinching red. Explicit,
    /// so it only runs when asked for by name:
    /// Unity -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.RestockCapture [-captureFolder folder]
    /// (no -batchmode: it grabs the screen, so it needs a window; Unity opens, runs it and closes).
    /// </summary>
    [Explicit("Screenshots of buying at the stalls; run it by name.")]
    public class RestockCapture
    {
        [UnityTest]
        public IEnumerator CaptureBuyingAtTheStalls()
        {
            Assume.That(Application.isBatchMode, Is.False, "Run it in a visible editor (without -batchmode): it grabs the screen.");
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            var pad = InputSystem.AddDevice<Gamepad>();
            Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Gamepad", pairWithDevice: pad);
            yield return Wait(1f);

            root.Services.Days.Advance(); // into Prep: the first morning's starter stock goes in
            yield return Wait(0.5f);

            var chef = Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None).Single();
            var folder = Folder();
            yield return Buy(chef, pad, "Stall_Butcher", Path.Combine(folder, "restock_butcher"));
            yield return Buy(chef, pad, "Stall_Greengrocer", Path.Combine(folder, "restock_greengrocer"));
            InputSystem.RemoveDevice(pad);
            Debug.Log($"[RestockCapture] Saved screenshots to {folder}");
        }

        // Walks up to the stall's front (placed there), lets the camera settle, opens the menu with
        // RT, shoots it, buys with A and shoots the feedback as it plays.
        private static IEnumerator Buy(PlayerMovement chef, Gamepad pad, string stallName, string file)
        {
            var stall = GameObject.Find(stallName).transform;
            var front = stall.position + stall.forward * 1.0f;
            chef.Teleport(new System.Numerics.Vector2(front.x, front.z));
            chef.FacingOverride = Quaternion.LookRotation(-stall.forward).eulerAngles.y;
            yield return Wait(2.5f);

            Press(pad, rightTrigger: 1f);
            yield return Wait(0.4f);
            yield return Shoot(file + "_1_menu.png");

            Press(pad, rightTrigger: 1f, south: true);
            yield return Wait(0.1f);
            yield return Shoot(file + "_2_bought.png");
            Press(pad, rightTrigger: 1f);
            yield return Wait(0.15f);
            yield return Shoot(file + "_3_settling.png");

            Press(pad, rightTrigger: 0f);
            yield return Wait(1f);
        }

        private static void Press(Gamepad pad, float rightTrigger, bool south = false)
        {
            var state = new GamepadState { rightTrigger = rightTrigger };
            if (south)
                state = state.WithButton(GamepadButton.South);
            InputSystem.QueueStateEvent(pad, state);
        }

        private static IEnumerator Shoot(string file)
        {
            yield return new WaitForEndOfFrame();
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(file, frame.EncodeToPNG());
            Object.Destroy(frame);
        }

        private static IEnumerator Wait(float seconds)
        {
            var end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }

        private static string Folder()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureFolder");
            var folder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.GetFullPath("Screenshots/Restock");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
