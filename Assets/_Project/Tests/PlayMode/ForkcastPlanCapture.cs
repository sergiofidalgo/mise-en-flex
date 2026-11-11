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
using UnityEngine.InputSystem;
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

        [UnityTest]
        public IEnumerator CaptureTheOverviewInReviewAndPlan()
        {
            Assume.That(Application.isBatchMode, Is.False, "Run it in a visible editor (without -batchmode): it grabs the screen.");
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "forkcast capture");
#endif
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            var services = root.Services;
            var days = services.Days;
            var dashboard = Object.FindFirstObjectByType<ReviewDashboard>();
            var folder = Folder();

            // Two cooks, so the Start button shows a pip each.
            // Four cooks: the Start button's chips and "Waiting for…" at their fullest.
            var pads = new[] { InputSystem.AddDevice<Gamepad>("CapturePad1"), InputSystem.AddDevice<Gamepad>("CapturePad2"), InputSystem.AddDevice<Gamepad>("CapturePad3"), InputSystem.AddDevice<Gamepad>("CapturePad4") };
            var manager = Object.FindFirstObjectByType<PlayerInputManager>();
            foreach (var pad in pads)
                manager.JoinPlayer(controlScheme: "Gamepad", pairWithDevices: pad);
            for (var i = 0; i < 3; i++)
                yield return null;

            // An evening one service before the collection, with the money short: the card turns urgent.
            typeof(DayCycle).GetProperty(nameof(DayCycle.DaysTowardsFee)).SetValue(days, days.Fee.EveryDays - 2);
            days.Advance();
            days.Advance();
            days.Charge(LedgerLine.Ingredients, 96f);
            days.TrySpend(Mathf.Max(0f, days.Balance - 140f));
            var guests = new[] { ("Hank B.", 4.5f, "Best steak on the block."), ("Carlos S.", 3.5f, "Good, not great."), ("Destiny K.", 2f, "Took forever."), ("Megan R.", 4f, "Would argue with the chef again.") };
            foreach (var (guest, stars, line) in guests)
                services.ReviewHistory.Add(new Game.Simulation.Saving.ReviewRecord { Day = days.Day, Guest = guest, Order = "steak, medium", Stars = stars, Line = line, Topic = (int)Game.Simulation.Service.ReviewTopic.Decent, Paid = 24f });
            days.FinishService(Summary(served: 6, walkOuts: 1, payments: 188f, tips: 22f, walkOutLosses: 12f), wages: 45f);
            // What they ordered that night, as DayLoop records it from the guests.
            days.History[days.History.Count - 1].Dishes = new System.Collections.Generic.List<DishNight>
            {
                new DishNight { RecipeId = services.Cookbook.Menu.First().Id, Dish = "steak, medium", Ordered = 7, WalkedOut = 1, StarSum = 21f, Rated = 6, Price = 24 },
            };
            for (var i = 0; i < 5; i++)
                yield return null;
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Review));
            var loop = Object.FindFirstObjectByType<DayLoop>();
            loop.Ready.Toggle(PlayerInput.all[0].playerIndex);
            loop.Ready.Toggle(PlayerInput.all[2].playerIndex);
            yield return Shoot(dashboard, -1, Path.Combine(folder, "forkcast_review_overview_urgent.png"));
            // P2 half-way through holding Y: the gold sweeps on and their chip fills.
            InputSystem.QueueStateEvent(pads[1], new UnityEngine.InputSystem.LowLevel.GamepadState().WithButton(UnityEngine.InputSystem.LowLevel.GamepadButton.North));
            yield return new WaitForSecondsRealtime(0.45f);
            yield return new WaitForEndOfFrame();
            var holding = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder, "forkcast_start_four_players_holding.png"), holding.EncodeToPNG());
            Object.Destroy(holding);
            InputSystem.QueueStateEvent(pads[1], new UnityEngine.InputSystem.LowLevel.GamepadState());
            yield return null;
            yield return ShootTab(dashboard, 2, Path.Combine(folder, "forkcast_review_books.png"));

            // The next morning (as after leaving and joining again): still day 1's summary.
            Object.FindFirstObjectByType<DayLoop>().Ready.Clear();
            days.Advance();
            for (var i = 0; i < 5; i++)
                yield return null;
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Plan));
            yield return Shoot(dashboard, -1, Path.Combine(folder, "forkcast_plan_overview_urgent.png"));
            yield return ShootTab(dashboard, 2, Path.Combine(folder, "forkcast_plan_books.png"));

            // Start day: straight into Prep, where the strip says how many guests to expect.
            days.Advance();
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Prep));
            Assert.That(dashboard.IsShowing, Is.False);
            yield return new WaitForEndOfFrame();
            var prep = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder, "prep_expected_guests.png"), prep.EncodeToPNG());
            Object.Destroy(prep);

            foreach (var pad in pads)
                InputSystem.RemoveDevice(pad);
            Debug.Log($"[ForkcastPlanCapture] Saved screenshots to {folder}");
        }

        [UnityTest]
        public IEnumerator CaptureTheBossCardStates()
        {
            Assume.That(Application.isBatchMode, Is.False, "Run it in a visible editor (without -batchmode): it grabs the screen.");
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "forkcast capture");
#endif
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            var days = root.Services.Days;
            var dashboard = Object.FindFirstObjectByType<ReviewDashboard>();
            // One cook: the start bar is a single bar.
            var pad = InputSystem.AddDevice<Gamepad>("BossCapturePad");
            Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Gamepad", pairWithDevices: pad);
            for (var i = 0; i < 3; i++)
                yield return null;
            days.Advance();
            days.Advance();
            days.FinishService(Summary(served: 6, walkOuts: 0, payments: 180f, tips: 20f, walkOutLosses: 0f), wages: 0f);
            days.History[days.History.Count - 1].Dishes.Add(new DishNight { Dish = "steak, rare", Ordered = 4, StarSum = 18f, Rated = 4, Price = 24 });
            var due = days.NextCollection.Due;
            var folder = Folder();
            var shots = new System.Collections.Generic.List<Texture2D>();

            // Short, two days to go; just covered; comfortably covered; a collection night paid in full.
            yield return BossState(dashboard, days, due * 0.4f, days.Fee.EveryDays - 2, shots);
            yield return BossState(dashboard, days, due * 1.04f, days.Fee.EveryDays - 3, shots);
            yield return BossState(dashboard, days, due * 1.8f, days.Fee.EveryDays - 3, shots);
            var record = days.History[days.History.Count - 1];
            record.Collected = true;
            record.CollectionDue = due;
            record.CollectionPaid = due;
            yield return BossState(dashboard, days, due * 0.5f, 0, shots);

            // One picture: the four cards stacked, cropped from the 1920×1080 frames.
            const int x = 752, top = 250, width = 626, height = 176;
            var sheet = new Texture2D(width, height * shots.Count, TextureFormat.RGB24, false);
            for (var i = 0; i < shots.Count; i++)
                sheet.SetPixels(0, (shots.Count - 1 - i) * height, width, height, shots[i].GetPixels(x, shots[i].height - top - height, width, height));
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(folder, "forkcast_boss_card_states.png"), sheet.EncodeToPNG());
            File.WriteAllBytes(Path.Combine(folder, "forkcast_one_player.png"), shots[2].EncodeToPNG());
            InputSystem.RemoveDevice(pad);
            foreach (var shot in shots)
                Object.Destroy(shot);
            Object.Destroy(sheet);
        }

        private static IEnumerator BossState(ReviewDashboard dashboard, DayCycle days, float balance, int daysTowardsFee, System.Collections.Generic.List<Texture2D> shots)
        {
            if (days.Balance > balance)
                days.TrySpend(days.Balance - balance);
            else
                days.Receive(balance - days.Balance);
            typeof(DayCycle).GetProperty(nameof(DayCycle.DaysTowardsFee)).SetValue(days, daysTowardsFee);
            dashboard.ShowForCapture(0, 0, false);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return new WaitForEndOfFrame();
            shots.Add(ScreenCapture.CaptureScreenshotAsTexture());
        }

        // The summary's setters are private (it's made from a finished service); a test fixture fills them in.
        private static Game.Simulation.Service.ServiceSummary Summary(int served, int walkOuts, float payments, float tips, float walkOutLosses)
        {
            var type = typeof(Game.Simulation.Service.ServiceSummary);
            var summary = (Game.Simulation.Service.ServiceSummary)Activator.CreateInstance(type, true);
            void Set(string name, object value) => type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).SetValue(summary, value);
            Set(nameof(Game.Simulation.Service.ServiceSummary.Served), served);
            Set(nameof(Game.Simulation.Service.ServiceSummary.WalkOuts), walkOuts);
            Set(nameof(Game.Simulation.Service.ServiceSummary.Payments), payments);
            Set(nameof(Game.Simulation.Service.ServiceSummary.Tips), tips);
            Set(nameof(Game.Simulation.Service.ServiceSummary.WalkOutLosses), walkOutLosses);
            Set(nameof(Game.Simulation.Service.ServiceSummary.Money), payments + tips - walkOutLosses);
            return summary;
        }

        private static IEnumerator ShootTab(ReviewDashboard dashboard, int tab, string file)
        {
            dashboard.ShowForCapture(tab, 0, false);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return new WaitForEndOfFrame();
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(file, frame.EncodeToPNG());
            Object.Destroy(frame);
        }

        // selected −1 shows the Overview; otherwise the Staff tab with that row chosen.
        private static IEnumerator Shoot(ReviewDashboard dashboard, int selected, string file)
        {
            dashboard.ShowForCapture(selected < 0 ? 0 : 3, Mathf.Max(0, selected), false);
            // Long enough for the editor to compile any new UI shader variant (drawn cyan until then)
            // and for the joining chefs' prompts to fade.
            yield return new WaitForSecondsRealtime(1.5f);
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
