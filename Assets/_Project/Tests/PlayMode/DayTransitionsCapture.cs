using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Gameplay;
using Game.Presentation.Hud;
using Game.Simulation.Days;
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
    /// Not a test: the day's transitions as players see them (the 2026-11-11 playtest round): the
    /// countdown at 3 and at 1, "Service!", the shutter coming down, the dashboard over the blurred
    /// shutter, and the next morning's "Day 2" as the shutter rolls up. Explicit, so it only runs
    /// when asked for by name:
    /// Unity -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.DayTransitionsCapture [-captureFolder folder]
    /// (no -batchmode: it grabs the screen, overlay canvases and all).
    /// </summary>
    [Explicit("Screenshots of the day transitions; run it by name.")]
    public class DayTransitionsCapture
    {
        [UnityTest]
        public IEnumerator CaptureTheDayTransitions()
        {
            KitchenCompositionRoot.CountdownSecondsOverride = null; // TuningConfig's, as in the game
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            Assume.That(Application.isBatchMode, Is.False, "Run it in a visible editor (without -batchmode): it grabs the screen.");
            var services = Object.FindFirstObjectByType<KitchenCompositionRoot>().Services;
            var transitions = Object.FindFirstObjectByType<DayTransitions>();
            var manager = Object.FindFirstObjectByType<PlayerInputManager>();
            var pads = Enumerable.Range(0, 2).Select(i => InputSystem.AddDevice<Gamepad>($"TransitionCapturePad{i}")).ToList();
            foreach (var pad in pads)
                manager.JoinPlayer(controlScheme: "Gamepad", pairWithDevices: pad);
            for (var i = 0; i < 5; i++)
                yield return null;
            var folder = Folder();

            var days = services.Days;
            while (days.Phase != DayPhase.Prep)
                days.Advance();
            yield return new WaitForSeconds(1.5f); // the morning shutter rolls away
            days.Advance(); // the doors are held: the countdown starts
            yield return WaitUntil(() => services.Countdown.Shown == 3);
            yield return new WaitForSeconds(0.3f);
            yield return Shoot(Path.Combine(folder, "1_countdown_3.png"));
            yield return WaitUntil(() => services.Countdown.Shown == 1);
            yield return new WaitForSeconds(0.5f);
            yield return Shoot(Path.Combine(folder, "2_countdown_1.png"));
            yield return WaitUntil(() => transitions.ServiceBandShowing);
            yield return new WaitForSeconds(0.55f);
            yield return Shoot(Path.Combine(folder, "3_service.png"));

            yield return new WaitForSeconds(8f); // guests come in
            days.FinishService(ServiceSummary.From(services.Service), wages: 0f);
            yield return new WaitForSeconds(0.3f);
            yield return Shoot(Path.Combine(folder, "4_shutter_dropping.png"));
            yield return new WaitForSeconds(0.45f);
            yield return Shoot(Path.Combine(folder, "5_closing_time.png"));
            yield return WaitUntil(() => transitions.Current == DayTransitions.Stage.Shut);
            yield return new WaitForSeconds(0.5f);
            yield return Shoot(Path.Combine(folder, "6_dashboard_over_shutter.png"));

            days.Advance(); // Start day 2: through Plan into Prep
            days.Advance();
            yield return new WaitForSeconds(0.45f);
            yield return Shoot(Path.Combine(folder, "7_morning_day_sign.png"));
            yield return new WaitForSeconds(0.55f);
            yield return Shoot(Path.Combine(folder, "8_morning_rolling_up.png"));

            foreach (var pad in pads)
                InputSystem.RemoveDevice(pad);
            KitchenCompositionRoot.CountdownSecondsOverride = 0f; // as the rest of the suite runs
            Debug.Log($"[DayTransitionsCapture] Saved screenshots to {folder}");
        }

        private static IEnumerator WaitUntil(Func<bool> condition)
        {
            var until = Time.realtimeSinceStartup + 20f;
            while (!condition() && Time.realtimeSinceStartup < until)
                yield return null;
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
            var folder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.GetFullPath("Screenshots/DayTransitions");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
