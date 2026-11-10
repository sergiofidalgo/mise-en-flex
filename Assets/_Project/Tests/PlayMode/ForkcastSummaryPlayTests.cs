using System;
using System.Collections;
using System.Reflection;
using Game.Gameplay;
using Game.Gameplay.Saving;
using Game.Presentation.Hud;
using Game.Simulation.Days;
using Game.Simulation.Service;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// forkcast as the end-of-day summary (the user's calls, 2026-11-11): day 1 starts straight in
    /// Prep, and leaving and joining again shows the last finished day, not the next one's empty books.
    /// </summary>
    public class ForkcastSummaryPlayTests
    {
        private const string SceneName = "Kitchen_Greybox";

        private KitchenCompositionRoot _root;
        private ReviewDashboard _dashboard;

        private IEnumerator Open()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _dashboard = Object.FindFirstObjectByType<ReviewDashboard>();
        }

        [TearDown]
        public void BackToNewCampaigns()
        {
            DayLoop.NewRestaurantsStartInPrep = false; // as the rest of the suite runs (SaveIsolation)
            SaveSlots.Delete(CampaignLaunch.Slot);
            CampaignLaunch.StartNew();
        }

        [UnityTest]
        public IEnumerator ANewRestaurantStartsDayOneInPrepWithNoSummary()
        {
            SaveSlots.Delete(CampaignLaunch.Slot);
            CampaignLaunch.StartNew();
            DayLoop.NewRestaurantsStartInPrep = true;
            yield return Open();
            var days = _root.Services.Days;
            Assume.That(days.History.Count, Is.Zero, "a brand-new restaurant");
            Assert.That(days.Day, Is.EqualTo(1));
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Prep));
            Assert.That(_root.Services.Forkcast.IsOpen, Is.False);
            Assert.That(_dashboard.IsShowing, Is.False);
        }

        [UnityTest]
        public IEnumerator RejoiningShowsTheLastFinishedDay()
        {
            SaveSlots.Delete(CampaignLaunch.Slot);
            CampaignLaunch.StartNew();
            yield return Open();
            var days = _root.Services.Days;

            // Day 1 and day 2 played to their summaries, each saved as Review starts, as DayLoop does.
            Finish(ingredients: 60f, payments: 120f, tips: 10f, served: 4);
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Review));
            Assert.That(_dashboard.ShownDay.Day, Is.EqualTo(1));
            days.Advance(); // to the next morning, which "Start day 2" passes through
            Finish(ingredients: 80f, payments: 210f, tips: 25f, served: 7);
            Assert.That(days.Day, Is.EqualTo(2));
            var dayTwo = days.Today.ToArray();

            // Day 3's prep, then the players leave and join again: no new save.
            days.Advance();
            days.Advance();
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Prep));
            CampaignLaunch.Continue(CampaignLaunch.Slot);
            yield return Open();
            days = _root.Services.Days;

            Assert.That(days.Day, Is.EqualTo(3), "the live day is the next one");
            Assert.That(_dashboard.IsShowing, Is.True, "they land on the summary");
            var shown = _dashboard.ShownDay;
            Assert.That(shown.Day, Is.EqualTo(2), "the summary is day 2's");
            Assert.That(shown.NextDay, Is.EqualTo(3), "Start day 3");
            Assert.That(shown.Served, Is.EqualTo(7));
            foreach (LedgerLine line in Enum.GetValues(typeof(LedgerLine)))
                Assert.That(shown.Ledger.Of(line), Is.EqualTo(dayTwo[(int)line]).Within(0.01f), $"the Books tab's {line} is day 2's");
            Assert.That(days.Today.In + days.Today.Out, Is.Zero, "while the live day's books are still empty");
        }

        private void Finish(float ingredients, float payments, float tips, int served)
        {
            var days = _root.Services.Days;
            if (days.Phase == DayPhase.Plan)
                days.Advance();
            days.Advance(); // Service
            days.Charge(LedgerLine.Ingredients, ingredients);
            days.FinishService(Summary(served, payments, tips), wages: 0f);
            _root.ListTodaysReviews();
            _root.SaveCampaign();
        }

        // The summary's setters are private (it's made from a finished service); a test fixture fills them in.
        private static ServiceSummary Summary(int served, float payments, float tips)
        {
            var type = typeof(ServiceSummary);
            var summary = (ServiceSummary)Activator.CreateInstance(type, true);
            void Set(string name, object value) => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance).SetValue(summary, value);
            Set(nameof(ServiceSummary.Served), served);
            Set(nameof(ServiceSummary.Payments), payments);
            Set(nameof(ServiceSummary.Tips), tips);
            Set(nameof(ServiceSummary.Money), payments + tips);
            return summary;
        }
    }
}
