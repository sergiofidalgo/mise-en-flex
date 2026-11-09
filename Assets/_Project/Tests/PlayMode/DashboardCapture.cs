using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Gameplay;
using Game.Presentation.Hud;
using Game.Simulation.Days;
using Game.Simulation.Saving;
using Game.Simulation.Service;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Not a test: screenshots of the Review dashboard after the mockup's evening (five guests, two
    /// walk-outs), one per tab, plus the reply wheel and a replied review, for side-by-side checks
    /// against the Art Director's mockups. Explicit, so it only runs when asked for by name:
    /// Unity -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.DashboardCapture [-captureFolder folder]
    /// (no -batchmode: it grabs the screen, so it needs a window; Unity opens, runs it and closes).
    /// </summary>
    [Explicit("Screenshots for comparing with the mockups; run it by name.")]
    public class DashboardCapture
    {
        [UnityTest]
        public IEnumerator CaptureTheReviewDashboard()
        {
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            var services = root.Services;
            var days = services.Days;

            days.Advance();
            days.Advance();
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Service));
            days.Charge(LedgerLine.Ingredients, 114f);
            var day = days.Day;
            Add(services, day, "Inés R.", "steak, medium", 19, 12, 0f, ReviewTopic.WalkedOut, "Gave up and left hungry.", walkedOut: true);
            Add(services, day, "Marco L.", "steak, medium", 19, 35, 0f, ReviewTopic.WalkedOut, "Left before my steak came.", walkedOut: true);
            Add(services, day, "Pavla K.", "steak, rare", 19, 31, 2f, ReviewTopic.SlowService, "Took forever.", paid: 20f);
            Add(services, day, "Aiko S.", "steak, medium rare", 18, 40, 3.5f, ReviewTopic.Decent, "Good, not great.", paid: 24f, tip: 2f);
            Add(services, day, "Tomáš B.", "steak, well done", 19, 20, 4.5f, ReviewTopic.Praise, "Best steak on the block.", paid: 24f, tip: 6f);
            days.FinishService(Summary(served: 3, walkOuts: 2, payments: 68f, tips: 8f, walkOutLosses: 24f));
            for (var i = 0; i < 5; i++)
                yield return null;

            Assume.That(Application.isBatchMode, Is.False, "Run it in a visible editor (without -batchmode): it grabs the screen.");
            var folder = Folder();

            var dashboard = Object.FindFirstObjectByType<ReviewDashboard>();
            yield return Shoot(dashboard, 0, 0, false, Path.Combine(folder, "dashboard_overview.png"));
            yield return Shoot(dashboard, 1, 0, false, Path.Combine(folder, "dashboard_reviews.png"));
            yield return Shoot(dashboard, 1, 0, true, Path.Combine(folder, "dashboard_reply_wheel.png"));
            yield return Shoot(dashboard, 1, 0, false, Path.Combine(folder, "dashboard_filter_walkouts.png"), 1, true);
            yield return Shoot(dashboard, 2, 0, false, Path.Combine(folder, "dashboard_menu.png"));
            yield return Shoot(dashboard, 3, 0, false, Path.Combine(folder, "dashboard_books.png"));
            root.ReplyTo(services.ReviewHistory.First(r => r.Guest == "Inés R."), ReviewReply.Apologise);
            yield return Shoot(dashboard, 1, 0, false, Path.Combine(folder, "dashboard_reply_thread.png"));
            Debug.Log($"[DashboardCapture] Saved screenshots to {folder}");
        }

        // Drawn, then grabbed from the screen at the end of the next frame: the frame players see.
        // That needs a window, so this runs in a visible editor (no -batchmode), where the frame is the
        // Game view's size. (In batch mode Unity never finishes a frame, and a canvas redrawn through a
        // hand-driven camera in Play mode comes out empty: tried 2026-11-09.)
        private static IEnumerator Shoot(ReviewDashboard dashboard, int tab, int selected, bool wheel, string file, int filter = 0, bool onFilters = false)
        {
            dashboard.ShowForCapture(tab, selected, wheel, filter, onFilters);
            yield return null;
            yield return new WaitForEndOfFrame();
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(file, frame.EncodeToPNG());
            Object.Destroy(frame);
        }

        // Arrival as service time: the dashboard's clock counts a second per minute after opening.
        private static void Add(KitchenServices services, int day, string guest, string order, int hour, int minute, float stars, ReviewTopic topic, string line, bool walkedOut = false, float paid = 0f, float tip = 0f)
        {
            var opens = services.ServiceConfig.OpensAt;
            services.ReviewHistory.Add(new ReviewRecord
            {
                Day = day,
                Guest = guest,
                Order = order,
                ArrivedAt = (hour * 60 + minute - opens * 60f) * 60f,
                Stars = stars,
                Topic = (int)topic,
                Line = line,
                WalkedOut = walkedOut,
                Penalty = walkedOut ? 12f : 0f,
                Paid = paid,
                Tip = tip,
            });
        }

        // The summary's setters are private (it's made from a finished service); a test fixture fills them in.
        private static ServiceSummary Summary(int served, int walkOuts, float payments, float tips, float walkOutLosses)
        {
            var summary = (ServiceSummary)Activator.CreateInstance(typeof(ServiceSummary), true);
            void Set(string name, object value) => typeof(ServiceSummary).GetProperty(name, BindingFlags.Public | BindingFlags.Instance).SetValue(summary, value);
            Set(nameof(ServiceSummary.Served), served);
            Set(nameof(ServiceSummary.WalkOuts), walkOuts);
            Set(nameof(ServiceSummary.Payments), payments);
            Set(nameof(ServiceSummary.Tips), tips);
            Set(nameof(ServiceSummary.WalkOutLosses), walkOutLosses);
            Set(nameof(ServiceSummary.Money), payments + tips - walkOutLosses);
            return summary;
        }

        private static string Folder()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureFolder");
            var folder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.GetFullPath("Screenshots/Dashboard");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
