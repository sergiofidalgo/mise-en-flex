using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Gameplay;
using Game.Presentation.Hud;
using Game.Simulation.Days;
using Game.Simulation.Staff;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Not a test: forkcast's Staff tab in the morning, with one member on the team and today's
    /// candidates (one out of reach), at 1920×1080. Explicit, so it only runs when asked for by name,
    /// in a visible editor (it grabs the screen):
    /// Unity -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.ForkcastPlanCapture [-captureFolder folder]
    /// </summary>
    [Explicit("Screenshots of forkcast in Plan; run it by name.")]
    public class ForkcastPlanCapture
    {
        [UnityTest]
        public IEnumerator CaptureTheStaffTabInPlan()
        {
            Assume.That(Application.isBatchMode, Is.False, "Run it in a visible editor (without -batchmode): it grabs the screen.");
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "forkcast capture");
#endif
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            var services = root.Services;
            Assert.That(services.Days.Phase, Is.EqualTo(DayPhase.Plan));
            Assert.That(services.Forkcast.IsOpen, Is.True);

            // One hired yesterday, the rest of today's candidates still looking: the dearest out of reach.
            var candidates = services.Staff.Candidates.ToList();
            services.Staff.Restore(new StaffMember(candidates[0]));
            var dearest = candidates.Skip(1).Max(c => c.DailyWage);
            services.Days.TrySpend(Mathf.Max(0f, services.Days.Balance - (services.Staff.TotalWages + dearest - 1f)));
            for (var i = 0; i < 5; i++)
                yield return null;

            var folder = Folder();
            var dashboard = Object.FindFirstObjectByType<ReviewDashboard>();
            yield return Shoot(dashboard, 1, Path.Combine(folder, "forkcast_plan_staff.png"));
            yield return Shoot(dashboard, 0, Path.Combine(folder, "forkcast_plan_staff_member.png"));
            yield return Shoot(dashboard, -1, Path.Combine(folder, "forkcast_plan_overview.png"));
            Debug.Log($"[ForkcastPlanCapture] Saved screenshots to {folder}");
        }

        // selected −1 shows the Overview; otherwise the Staff tab with that row chosen.
        private static IEnumerator Shoot(ReviewDashboard dashboard, int selected, string file)
        {
            dashboard.ShowForCapture(selected < 0 ? 0 : 3, Mathf.Max(0, selected), false);
            yield return null;
            yield return new WaitForEndOfFrame();
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            Debug.Log($"[ForkcastPlanCapture] {Path.GetFileName(file)} is {frame.width}×{frame.height}");
            File.WriteAllBytes(file, frame.EncodeToPNG());
            Object.Destroy(frame);
        }

        private static string Folder()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureFolder");
            var folder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.GetFullPath("Screenshots/ForkcastPlan");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
