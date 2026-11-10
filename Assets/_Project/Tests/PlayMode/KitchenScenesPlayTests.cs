using System.Collections;
using System.Linq;
using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Saving;
using Game.Gameplay.Stations;
using Game.Simulation.Campaign;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Saving;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Vector2 = System.Numerics.Vector2;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Phase 6, milestone 3: each playable restaurant opens its own kitchen, wired for itself (its
    /// fee, guests, seats, name and old reviews), and a restaurant left idle comes back as it was.
    /// </summary>
    public class KitchenScenesPlayTests
    {
        private const string MiniaturePath = "Assets/_Project/Data/Restaurants/MiniatureConfig.asset";
        private const int Slot = 0;

        private KitchenCompositionRoot _root;

        [TearDown]
        public void BackToNewCampaigns()
        {
            SaveSlots.Delete(Slot);
            CampaignLaunch.StartNew();
        }

        private static MiniatureConfig Miniature()
        {
#if UNITY_EDITOR
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<MiniatureConfig>(MiniaturePath);
            Assert.That(config, Is.Not.Null, MiniaturePath);
            return config;
#else
            Assert.Ignore("Needs the Editor's asset database.");
            return null;
#endif
        }

        private static RestaurantSettings Lot(string id) => Miniature().LotSettings().First(l => l.Id == id);

        // A campaign owning the grill and Casa Lucía, as bought on the miniature, with this one current.
        private static CampaignSave TwoRestaurants(string current)
        {
            var campaign = new CampaignSave { Balance = 1000f };
            campaign.Add(Lot(Version1Restaurant.GrillId).NewRecord(keepOldReviews: true));
            campaign.Add(Lot("casa-lucia").NewRecord(keepOldReviews: true));
            campaign.Switch(campaign.Restaurants.FindIndex(r => r.DefinitionId == current));
            return campaign;
        }

        // What the office does: save the campaign, then load the current restaurant's kitchen.
        private IEnumerator Launch(CampaignSave campaign, string expectedScene)
        {
            SaveSlots.File(Slot).Write(campaign);
            CampaignLaunch.Continue(Slot);
            var scene = CampaignLaunch.KitchenScene(Slot, Miniature());
            Assert.That(scene, Is.EqualTo(expectedScene));
            yield return SceneManager.LoadSceneAsync(scene);
            yield return null;
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            Assert.That(_root, Is.Not.Null);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(expectedScene));
        }

        [UnityTest]
        public IEnumerator TheGrillOpensItsOwnKitchenWiredForItself() => OpensWiredForItself(Version1Restaurant.GrillId, "Kitchen_Greybox");

        [UnityTest]
        public IEnumerator CasaLuciaOpensItsOwnKitchenWiredForItself() => OpensWiredForItself("casa-lucia", "Kitchen_CasaLucia");

        private IEnumerator OpensWiredForItself(string id, string sceneName)
        {
            yield return Launch(TwoRestaurants(id), sceneName);
            var lot = Lot(id);
            var services = _root.Services;

            Assert.That(_root.Played.Id, Is.EqualTo(id));
            Assert.That(_root.RestaurantId, Is.EqualTo(id));
            Assert.That(services.RestaurantName, Is.EqualTo(lot.Name), "its own name");
            Assert.That(services.StyleName, Is.EqualTo(lot.PreviousStyle), "its own style");
            Assert.That(services.Days.Day, Is.EqualTo(1), "a restaurant just bought opens on day 1");
            Assert.That(services.Days.Balance, Is.EqualTo(1000f), "with the campaign's money");

            // Its cut, and its guests: a bigger room draws more, and more can wait.
            var config = services.ServiceConfig;
            Assert.That(services.Days.Fee.Amount, Is.EqualTo(config.ToFeeSettings().Amount * lot.FeeScale).Within(0.01f));
            var baseService = config.ToSettings(services.Days.Day);
            Assert.That(services.Service.Settings.ArrivalIntervalSeconds, Is.EqualTo(baseService.ArrivalIntervalSeconds / lot.GuestRate).Within(0.01f));
            Assert.That(services.Service.Settings.MaxQueue, Is.EqualTo(lot.MaxQueue > 0 ? lot.MaxQueue : baseService.MaxQueue));

            // Its seats, appliances and old reviews.
            var stations = services.Stations.All;
            Assert.That(stations.OfType<TableStation>().Sum(t => t.SeatCount), Is.EqualTo(lot.Seats), "every seat has room in the building");
            Assert.That(stations.OfType<HobStation>().Count(), Is.EqualTo(lot.InheritedAppliances.Count(a => a == "hob")));
            Assert.That(services.ReviewHistory.Count(r => r.Day == 0), Is.EqualTo(lot.OldReviews.Count), "kept with its name");

            // The sign on the street says who it is.
            var sign = GameObject.Find("SignLettering");
            Assert.That(sign, Is.Not.Null);
            Assert.That(sign.GetComponentsInChildren<TMP_Text>().Select(t => t.text), Does.Contain(lot.Name));

            // Everyone has somewhere free to start, and every piece can be walked to and worked at.
            var layout = services.NewLayout();
            for (var i = 1; i <= 4; i++)
            {
                var spawn = GameObject.Find($"Spawn_P{i}").transform.position;
                Assert.That(layout.IsFree(new Vector2(spawn.x, spawn.z)), $"player {i} starts somewhere free");
            }
            var staff = GameObject.Find("StaffSpawn").transform.position;
            var from = new Vector2(staff.x, staff.z);
            Assert.That(layout.IsFree(from), "staff arrive somewhere free");
            foreach (var station in stations.Where(s => !(s is StreetDoor) && !(s is StallStation)))
            {
                var spot = layout.StandingSpot(station.Footprint);
                Assert.That(spot, Is.Not.Null, $"nowhere to stand at {station.name}");
                Assert.That(layout.FindPath(from, spot.Value), Is.Not.Null, $"no way to {station.name}");
            }
        }

        [UnityTest]
        public IEnumerator AnIdleRestaurantComesBackAsItWasLeft()
        {
            // A day at the grill: a steak left on a counter, money made, then saved as Review starts.
            yield return Launch(TwoRestaurants(Version1Restaurant.GrillId), "Kitchen_Greybox");
            var counter = _root.Services.Stations.All.OfType<CounterStation>().First(c => c.IsClear);
            var counterName = counter.name;
            counter.Slot.Put((Ingredient)_root.Services.CreateItem("Steak"));
            _root.Services.Days.Receive(200f);
            _root.SaveCampaign();

            // Four days played and three towards its fee, as if played that long.
            var file = SaveSlots.File(Slot);
            var campaign = file.Read();
            campaign.Current.Day = 4;
            campaign.Current.DaysTowardsFee = 3;
            var grillBefore = JsonUtility.ToJson(campaign.Current);

            // To the office: open Casa Lucía and play a day there.
            campaign.Switch(campaign.Restaurants.FindIndex(r => r.DefinitionId == "casa-lucia"));
            yield return Launch(campaign, "Kitchen_CasaLucia");
            Assert.That(_root.Played.Id, Is.EqualTo("casa-lucia"));
            Assert.That(_root.Services.Days.Day, Is.EqualTo(1), "Casa Lucía's own day count");
            Assert.That(_root.Services.Days.DaysTowardsFee, Is.EqualTo(0), "and its own fee clock");
            Assert.That(_root.Services.Days.Balance, Is.EqualTo(1200f), "one pocket for the whole campaign");
            _root.Services.Days.Receive(50f);
            _root.SaveCampaign();

            campaign = file.Read();
            Assert.That(JsonUtility.ToJson(campaign.Find(Version1Restaurant.GrillId)), Is.EqualTo(grillBefore), "the grill sat idle, untouched");
            Assert.That(campaign.Find("casa-lucia").Stations, Is.Not.Empty, "Casa Lucía's room is saved too");

            // Back to the grill: as it was left, its fee clock where it stopped.
            campaign.Switch(campaign.Restaurants.FindIndex(r => r.DefinitionId == Version1Restaurant.GrillId));
            yield return Launch(campaign, "Kitchen_Greybox");
            var days = _root.Services.Days;
            Assert.That(days.Day, Is.EqualTo(5), "the morning after its fourth day");
            Assert.That(days.DaysTowardsFee, Is.EqualTo(3));
            Assert.That(days.DaysUntilFee, Is.EqualTo(2), "paused while Casa Lucía ran");
            Assert.That(days.Balance, Is.EqualTo(1250f));
            var back = _root.Services.Stations.All.OfType<CounterStation>().First(c => c.name == counterName);
            Assert.That(back.Slot.Current, Is.InstanceOf<Ingredient>(), "the steak is where it was left");
        }
    }
}
