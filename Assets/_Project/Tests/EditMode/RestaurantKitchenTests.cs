using System.IO;
using System.Linq;
using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Saving;
using Game.Gameplay.Stations;
using Game.Simulation.Campaign;
using Game.Simulation.Days;
using Game.Simulation.Saving;
using Game.Simulation.Service;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Phase 6, milestone 3: a second restaurant. Each playable restaurant has its own kitchen scene,
    /// the campaign opens the current one's, the one being played sets the fee, the guests and the
    /// name, and the restaurants not being played sit exactly as they were left.
    /// </summary>
    public class RestaurantKitchenTests
    {
        private const string MiniaturePath = "Assets/_Project/Data/Restaurants/MiniatureConfig.asset";
        private const string Version1Fixture = "Assets/_Project/Tests/EditMode/Fixtures/campaign_v1.json";
        private const string GrillScene = "Kitchen_Greybox";
        private const string DiningRoomScene = "Kitchen_CasaLucia";

        private static RestaurantSettings Lot(string id, string scene) => new RestaurantSettings { Id = id, Name = id, KitchenScene = scene };

        private static readonly RestaurantSettings[] Lots =
        {
            Lot("grill", GrillScene), Lot("forno-rosso", ""), Lot("casa-lucia", DiningRoomScene),
        };

        private static CampaignSave Owning(params string[] ids)
        {
            var campaign = new CampaignSave();
            foreach (var id in ids)
                campaign.Add(Lots.First(l => l.Id == id).NewRecord());
            return campaign;
        }

        // --- Choosing the scene ---

        [Test]
        public void TheCurrentRestaurantOpensItsOwnKitchen()
        {
            var campaign = Owning("grill", "casa-lucia");
            Assert.That(KitchenScenes.For(campaign, Lots, "fallback"), Is.EqualTo(GrillScene), "the first bought is current");

            campaign.Switch(1);
            Assert.That(KitchenScenes.For(campaign, Lots, "fallback"), Is.EqualTo(DiningRoomScene));

            campaign.Switch(0);
            Assert.That(KitchenScenes.For(campaign, Lots, "fallback"), Is.EqualTo(GrillScene), "and back");
        }

        [Test]
        public void ARestaurantWithoutAKitchenYetOpensTheFallback()
        {
            var campaign = Owning("grill", "forno-rosso");
            campaign.Switch(1);
            Assert.That(KitchenScenes.For(campaign, Lots, GrillScene), Is.EqualTo(GrillScene));
            Assert.That(KitchenScenes.For((CampaignSave)null, Lots, GrillScene), Is.EqualTo(GrillScene), "no campaign yet");
            Assert.That(KitchenScenes.For("not-a-lot", Lots, GrillScene), Is.EqualTo(GrillScene), "an unknown id");
            Assert.That(KitchenScenes.For(campaign, null, GrillScene), Is.EqualTo(GrillScene), "no lots known");
        }

        [Test]
        public void EveryKitchenIsListedOnceInTheLotsOrder()
        {
            Assert.That(KitchenScenes.All(Lots.Concat(new[] { Lot("again", GrillScene) })), Is.EqualTo(new[] { GrillScene, DiningRoomScene }));
        }

        [Test]
        public void ASaveFromBeforeTheCampaignOpensTheGrillsKitchen()
        {
            var campaign = SaveFile.Parse(File.ReadAllText(Version1Fixture));
            Assert.That(campaign.Current.DefinitionId, Is.EqualTo(Version1Restaurant.GrillId));
            Assert.That(CampaignLaunch.KitchenScene(campaign, Miniature()), Is.EqualTo(GrillScene));
        }

        [Test]
        public void TheLaunchOpensTheCurrentRestaurantsKitchenFromTheRealLots()
        {
            var miniature = Miniature();
            var lots = miniature.LotSettings();
            var campaign = new CampaignSave();
            campaign.Add(lots.First(l => l.Id == Version1Restaurant.GrillId).NewRecord());
            campaign.Add(lots.First(l => l.Id == "casa-lucia").NewRecord());
            campaign.Add(lots.First(l => l.Id == "forno-rosso").NewRecord());

            Assert.That(CampaignLaunch.KitchenScene(campaign, miniature), Is.EqualTo(GrillScene));
            campaign.Switch(1);
            Assert.That(CampaignLaunch.KitchenScene(campaign, miniature), Is.EqualTo(DiningRoomScene));
            campaign.Switch(2);
            Assert.That(CampaignLaunch.KitchenScene(campaign, miniature), Is.EqualTo(GrillScene), "Forno Rosso has no kitchen of its own yet");
            Assert.That(CampaignLaunch.KitchenScene(null, miniature), Is.EqualTo(GrillScene), "a new campaign");
        }

        // --- The definitions ---

        [Test]
        public void TheTwoPlayableRestaurantsHaveTheirKitchens()
        {
            var lots = Miniature().LotSettings();
            var grill = lots.First(l => l.Id == Version1Restaurant.GrillId);
            Assert.That(grill.KitchenScene, Is.EqualTo(GrillScene));
            Assert.That((grill.RoomWidth, grill.KitchenDepth, grill.DiningDepth), Is.EqualTo((8f, 5f, 4f)), "the grill's building as it always was");
            Assert.That((grill.GuestRate, grill.MaxQueue), Is.EqualTo((1f, 0)));

            // The user's calls (2026-11-10): wider and deeper, guests 2.4 times as often, up to 6 waiting.
            var casa = lots.First(l => l.Id == "casa-lucia");
            Assert.That(casa.KitchenScene, Is.EqualTo(DiningRoomScene));
            Assert.That((casa.RoomWidth, casa.KitchenDepth, casa.DiningDepth), Is.EqualTo((12f, 5f, 6f)));
            Assert.That(casa.GuestRate, Is.EqualTo(casa.FeeScale).Within(0.001f), "its guests keep pace with its cut");
            Assert.That(casa.MaxQueue, Is.EqualTo(6));
            Assert.That(casa.Seats, Is.EqualTo(16));

            Assert.That(KitchenScenes.All(lots), Is.EqualTo(new[] { GrillScene, DiningRoomScene }), "the others are data until 6b or Phase 7");
        }

        [Test]
        public void TheBuildListsTheOfficeThenEveryKitchen()
        {
            var scenes = EditorBuildSettings.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path)).ToList();
            Assert.That(scenes, Does.Contain(GrillScene));
            Assert.That(scenes, Does.Contain(DiningRoomScene));
            Assert.That(scenes.IndexOf(GrillScene), Is.LessThan(scenes.IndexOf(DiningRoomScene)));
            if (scenes.Contains("Office_Greybox"))
                Assert.That(scenes[0], Is.EqualTo("Office_Greybox"), "the game boots into the office");
        }

        // --- Per-restaurant settings ---

        [Test]
        public void ABiggerRoomDrawsMoreGuestsAndALongerQueue()
        {
            var service = new ServiceSettings { ArrivalIntervalSeconds = 1500f, MaxQueue = 3 };
            new RestaurantSettings { GuestRate = 2.4f, MaxQueue = 6 }.ScaleService(service);
            Assert.That(service.ArrivalIntervalSeconds, Is.EqualTo(625f).Within(0.01f));
            Assert.That(service.MaxQueue, Is.EqualTo(6));
        }

        [Test]
        public void TheGrillsGuestsAreUnchanged()
        {
            var service = new ServiceSettings { ArrivalIntervalSeconds = 1500f, MaxQueue = 3 };
            new RestaurantSettings().ScaleService(service);
            Assert.That(service.ArrivalIntervalSeconds, Is.EqualTo(1500f));
            Assert.That(service.MaxQueue, Is.EqualTo(3), "0 keeps the Service Config's queue");
        }

        [Test]
        public void EachRestaurantOwesItsOwnCut()
        {
            var lots = Miniature().LotSettings();
            var baseFee = new FeeSettings { Amount = 450f, EveryDays = 5 };
            Assert.That(lots.First(l => l.Id == Version1Restaurant.GrillId).ScaleFee(baseFee).Amount, Is.EqualTo(450f));
            Assert.That(lots.First(l => l.Id == "casa-lucia").ScaleFee(baseFee).Amount, Is.EqualTo(1080f).Within(0.01f));
        }

        // --- Idle restaurants ---

        [Test]
        public void AnIdleRestaurantIsWrittenBackExactlyAsItWasLeft()
        {
            // Day 5 played at the grill, three towards its fee, with a review and a counter moved.
            var campaign = Owning("grill", "casa-lucia");
            var grill = campaign.Current;
            grill.Day = 5;
            grill.DaysTowardsFee = 3;
            grill.Reviews.Add(new ReviewRecord { Day = 4, Stars = 4f, Line = "Great steak." });
            grill.Stations.Add(new StationRecord { FurnitureId = "counter", X = 1.5f, Z = -1.5f });
            var before = JsonUtility.ToJson(grill);

            // To the office, open Casa Lucía and play two days there.
            campaign.Switch(1);
            var casa = JsonUtility.FromJson<RestaurantSave>(JsonUtility.ToJson(campaign.Current));
            casa.Day = 3;
            casa.DaysTowardsFee = 2;
            campaign.StoreCurrent(casa);
            var reloaded = SaveFile.Parse(JsonUtility.ToJson(campaign));

            Assert.That(JsonUtility.ToJson(reloaded.Find(Version1Restaurant.GrillId)), Is.EqualTo(before), "the grill as it was left");
            Assert.That(reloaded.Find("casa-lucia").Day, Is.EqualTo(3));

            // Back to the grill: its fee clock picks up where it stopped, paused while Casa Lucía ran.
            reloaded.Switch(0);
            var days = DayCycle.Continuing(reloaded.Current.Day, reloaded.Balance, new FeeSettings { Amount = 450f, EveryDays = 5 }, reloaded.Current.DaysTowardsFee);
            Assert.That(days.Day, Is.EqualTo(6), "the morning after the day it was saved on");
            Assert.That(days.DaysUntilFee, Is.EqualTo(2));
        }

        // --- The generated scenes ---

        [TestCase(GrillScene, "grill")]
        [TestCase(DiningRoomScene, "casa-lucia")]
        public void EachKitchenIsBuiltFromItsRestaurant(string sceneName, string id)
        {
            var path = $"Assets/_Project/Scenes/{sceneName}.unity";
            if (!File.Exists(path))
                Assert.Ignore($"{path} isn't built yet: run Tools/Game/Build Greybox Kitchen.");
            var lot = Miniature().LotSettings().First(l => l.Id == id);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var all = scene.GetRootGameObjects();

            var root = all.SelectMany(r => r.GetComponentsInChildren<KitchenCompositionRoot>(true)).Single();
            var wiring = new SerializedObject(root);
            Assert.That(((RestaurantDefinition)wiring.FindProperty("_restaurant").objectReferenceValue).Id, Is.EqualTo(id));
            Assert.That(wiring.FindProperty("_restaurants").objectReferenceValue, Is.Not.Null, "every lot, for the restaurant being played");
            Assert.That(wiring.FindProperty("_floorPlan").objectReferenceValue, Is.Not.Null);

            T[] Find<T>() where T : Component => all.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
            int Inherited(string appliance) => lot.InheritedAppliances.Count(a => a == appliance);
            var tables = Find<TableStation>();
            var seats = tables.Sum(t => Mathf.Min(new SerializedObject(t).FindProperty("_startingChairs").intValue, new SerializedObject(t).FindProperty("_chairs").arraySize));
            Assert.That(seats, Is.EqualTo(lot.Seats), "as many seats as its definition says");
            Assert.That(Find<HobStation>().Length, Is.EqualTo(Inherited("hob")), "its gas hobs");
            Assert.That(Find<PrepStation>().Length, Is.EqualTo(Inherited("board")), "its boards");
            Assert.That(Find<StorageStation>().Length, Is.EqualTo(Inherited("fridge") + Inherited("shelf")), "its fridges and shelves");
            Assert.That(Find<KnifeRackStation>().Length, Is.EqualTo(id == "grill" ? 0 : Inherited("knife rack")), "the grill's knife sits on a counter");
            Assert.That(Find<StreetDoor>().Length, Is.EqualTo(1));
            Assert.That(Find<Game.Presentation.RestaurantSignView>().Length, Is.EqualTo(1), "a sign with its name");

            // The street's stalls are its own vendors: open if they have something to sell.
            var stalls = Find<StallStation>().Select(s => s.name).ToArray();
            Assert.That(stalls, Is.EquivalentTo(lot.Vendors.Where(v => v == "Butcher" || v == "Greengrocer").Select(v => $"Stall_{v}")));
        }

        [TestCase(GrillScene)]
        [TestCase(DiningRoomScene)]
        public void EveryStationSitsInsideItsBuilding(string sceneName)
        {
            var path = $"Assets/_Project/Scenes/{sceneName}.unity";
            if (!File.Exists(path))
                Assert.Ignore($"{path} isn't built yet: run Tools/Game/Build Greybox Kitchen.");
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<KitchenCompositionRoot>(true)).Single();
            var plan = (FloorPlanConfig)new SerializedObject(root).FindProperty("_floorPlan").objectReferenceValue;
            var stations = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Interactable>(true))
                .Where(s => !(s is StreetDoor) && !(s is StallStation) && !(s is WheelieBin)) // the bin stands outside: BinTrackTests
                .ToArray();
            foreach (var station in stations)
            {
                var p = station.transform.position;
                var inKitchen = p.x >= plan.KitchenAreaMin.X - 0.01f && p.x <= plan.KitchenAreaMax.X + 0.01f && p.z >= plan.KitchenAreaMin.Y - 0.01f && p.z <= plan.KitchenAreaMax.Y + 0.01f;
                var inDining = p.x >= plan.DiningAreaMin.X - 0.01f && p.x <= plan.DiningAreaMax.X + 0.01f && p.z >= plan.DiningAreaMin.Y - 0.3f && p.z <= plan.DiningAreaMax.Y + 0.3f;
                Assert.That(inKitchen || inDining, $"{station.name} at ({p.x:0.00}, {p.z:0.00}) is outside {sceneName}'s rooms");
            }
            // No two pieces on the same spot.
            var spots = stations.Select(s => new Vector2(Mathf.Round(s.transform.position.x * 10f), Mathf.Round(s.transform.position.z * 10f))).ToList();
            Assert.That(spots.Distinct().Count(), Is.EqualTo(spots.Count), "two stations share a spot");
        }

        private static MiniatureConfig Miniature()
        {
            var config = AssetDatabase.LoadAssetAtPath<MiniatureConfig>(MiniaturePath);
            Assert.That(config, Is.Not.Null, MiniaturePath);
            return config;
        }
    }
}
