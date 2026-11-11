using System.Collections;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Saving;
using Game.Gameplay.Stations;
using Game.Simulation.Cooking;
using Game.Simulation.Days;
using Game.Simulation.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Vector2 = System.Numerics.Vector2;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// The real scene saved and continued (Phase 5, decisions 2026-11-08): bought furniture, food left
    /// out and money come back the morning after.
    /// </summary>
    public class SavePlayTests
    {
        private const string SceneName = "Kitchen_Greybox";

        // Open kitchen floor between the island and the pass, as in the buying tests.
        private static readonly Vector2 OpenFloor = new Vector2(-0.4f, -2.0f);

        private KitchenCompositionRoot _root;

        [TearDown]
        public void BackToNewCampaigns()
        {
            SaveSlots.Delete(CampaignLaunch.Slot);
            CampaignLaunch.StartNew();
        }

        private IEnumerator Open()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
        }

        [UnityTest]
        public IEnumerator ContinuingPutsBackTheKitchenAsItWasLeft()
        {
            yield return Open();
            var editor = Object.FindFirstObjectByType<LayoutEditor>();
            var keyboard = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.GetDevice<Mouse>() ?? InputSystem.AddDevice<Mouse>();
            var player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Keyboard&Mouse", pairWithDevices: new InputDevice[] { keyboard, mouse });
            yield return null;
            yield return null;

            var services = _root.Services;

            // A steak left out on a free counter overnight, part-seasoned.
            var counter = services.Stations.All.OfType<CounterStation>().First(c => c.IsClear);
            var steak = (Ingredient)services.CreateItem("Steak");
            steak.Seasoning = 0.3f;
            counter.Slot.Put(steak);
            var counterName = counter.name;
            var knives = CountKnives(services);

            // A counter bought today, and the money it left.
            services.Days.Receive(500f);
            var cursor = editor.Enter(player);
            cursor.Position = OpenFloor;
            Assert.That(editor.Buy(cursor, editor.Shop.First(e => e.Id == "counter")), Is.True);
            editor.MoveCursor(cursor, Vector2.Zero);
            var bought = cursor.Held;
            Assert.That(editor.Place(cursor), Is.True, cursor.Result.ToString());
            var boughtAt = bought.transform.position;
            var stationCount = services.Stations.All.Count;

            // Saved as Review starts, as the game saves (since 2026-11-11 a save records the last
            // finished day, so the day is played to its end first).
            services.Days.Advance();
            services.Days.Advance();
            services.Days.FinishService(Game.Simulation.Service.ServiceSummary.From(services.Service));
            var balance = services.Days.Balance;
            var day = services.Days.Day;
            _root.SaveCampaign();
            Assert.That(_root.SaveFile.Exists, Is.True, "written");

            CampaignLaunch.Continue(CampaignLaunch.Slot);
            yield return Open();
            services = _root.Services;

            Assert.That(services.Days.Day, Is.EqualTo(day + 1), "the morning after");
            Assert.That(services.Days.Phase, Is.EqualTo(DayPhase.Plan));
            Assert.That(services.Days.Balance, Is.EqualTo(balance).Within(0.01f));
            Assert.That(services.Stations.All.Count, Is.EqualTo(stationCount), "the bought counter is back, nothing extra");
            Assert.That(services.Stations.All.Any(s => Vector3.Distance(s.transform.position, boughtAt) < 0.01f), Is.True, "where it was put");

            var counterBack = services.Stations.All.OfType<CounterStation>().First(c => c.name == counterName);
            var steakBack = counterBack.Slot.Current as Ingredient;
            Assert.That(steakBack, Is.Not.Null, "the steak is still on its counter");
            Assert.That(steakBack.Seasoning, Is.EqualTo(0.3f));
            Assert.That(services.Cooking.Contains(steakBack), Is.True, "it goes on ageing");
            Assert.That(CountKnives(services), Is.EqualTo(knives), "no knife lost or doubled");
        }

        private static int CountKnives(KitchenServices services) =>
            services.Stations.All.OfType<CounterStation>().Count(c => c.Slot.Current is Knife)
            + services.Stations.All.OfType<KnifeRackStation>().Sum(r => r.KnifeCount)
            + Object.FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None).Count(p => p.Tool is Knife);
    }
}
