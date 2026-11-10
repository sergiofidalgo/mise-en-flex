using System.IO;
using System.Linq;
using Game.Data;
using Game.Gameplay.Saving;
using Game.Simulation.Campaign;
using Game.Simulation.Days;
using Game.Simulation.Saving;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// A campaign of restaurants (Phase 6, milestone 1; decision 2026-11-10): the save holds several
    /// restaurants, each with its own room, stock, mess, reviews, staff, day count and fee clock, while
    /// money and the loan belong to the campaign. A version-1 file becomes restaurant 1, and an idle
    /// restaurant waits exactly as it was left.
    /// </summary>
    public class CampaignSaveTests
    {
        private const string Version1Fixture = "Assets/_Project/Tests/EditMode/Fixtures/campaign_v1.json";

        private string _directory;

        [SetUp]
        public void MakeFolder() =>
            _directory = Path.Combine(Path.GetTempPath(), "MiseEnFlexCampaignTest_" + System.Guid.NewGuid().ToString("N"));

        [TearDown]
        public void RemoveFolder()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        // ---- Two restaurants ----

        [Test]
        public void TwoRestaurantsRoundTripThroughTheFile()
        {
            var campaign = TwoRestaurants();
            campaign.Switch(1);

            var file = new SaveFile(_directory);
            file.Write(campaign);
            var back = file.Read();

            Assert.That(back.Version, Is.EqualTo(CampaignSave.CurrentVersion));
            Assert.That(back.Balance, Is.EqualTo(-250f), "money is the campaign's");
            Assert.That(back.Loan, Is.EqualTo(1200f), "so is the loan");
            Assert.That(back.Recipes, Is.EqualTo(new[] { "steak", "onions" }));
            Assert.That(back.Restaurants, Has.Count.EqualTo(2));
            Assert.That(back.CurrentRestaurant, Is.EqualTo(1));
            Assert.That(back.Current.Name, Is.EqualTo("Harbour Room"));

            var grill = back.Find(Version1Restaurant.GrillId);
            Assert.That(grill.Name, Is.EqualTo("The Copper Pan"));
            Assert.That(grill.Day, Is.EqualTo(9));
            Assert.That(grill.DaysTowardsFee, Is.EqualTo(4));
            Assert.That(grill.Period, Is.EqualTo(new[] { 100f, -20f }));
            Assert.That(grill.Stations.Single().FurnitureId, Is.EqualTo("hob"));
            Assert.That(grill.Items.Single().TypeId, Is.EqualTo("Steak"));
            Assert.That(grill.Staff.Single().Name, Is.EqualTo("Ana"));
            Assert.That(grill.Reviews.Single().Line, Is.EqualTo("Good steak."));
            Assert.That(grill.Dirt.Single().Amount, Is.EqualTo(0.4f));
            Assert.That(grill.Prices.Single().Price, Is.EqualTo(19));

            var harbour = back.Find("harbour");
            Assert.That(harbour.Day, Is.EqualTo(2));
            Assert.That(harbour.DaysTowardsFee, Is.EqualTo(1));
            Assert.That(harbour.Stations.Select(s => s.FurnitureId), Is.EqualTo(new[] { "table", "table" }));
            Assert.That(harbour.Items, Is.Empty, "nothing of the grill's leaks into it");
            Assert.That(harbour.Staff, Is.Empty);
            Assert.That(harbour.Reviews.Single().Line, Is.EqualTo("Fresh fish."));
            Assert.That(harbour.Dirt, Is.Empty);
        }

        [Test]
        public void ANewCampaignHasNoRestaurantUntilOneIsStored()
        {
            var campaign = new CampaignSave();
            Assert.That(campaign.Current, Is.Null);
            Assert.That(campaign.Loan, Is.EqualTo(0f));

            campaign.StoreCurrent(new RestaurantSave { Name = "First" });

            Assert.That(campaign.Restaurants, Has.Count.EqualTo(1));
            Assert.That(campaign.Current.Name, Is.EqualTo("First"));
        }

        [Test]
        public void SwitchingToARestaurantThatIsntOwnedIsRefused()
        {
            var campaign = TwoRestaurants();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => campaign.Switch(2));
            Assert.That(campaign.CurrentRestaurant, Is.EqualTo(0), "still where it was");
        }

        // ---- Idle restaurants ----

        [Test]
        public void IdleRestaurantsKeepTheirState()
        {
            var campaign = TwoRestaurants();
            var idleBefore = JsonUtility.ToJson(campaign.Restaurants[1]);

            // Three days played at the grill, each saved as Review starts, through the file each time.
            var file = new SaveFile(_directory);
            for (var day = 10; day < 13; day++)
            {
                var played = campaign.Current;
                campaign.StoreCurrent(new RestaurantSave
                {
                    DefinitionId = played.DefinitionId,
                    Name = played.Name,
                    Day = day,
                    DaysTowardsFee = (played.DaysTowardsFee + 1) % 5,
                });
                campaign.Balance += 100f;
                file.Write(campaign);
                campaign = file.Read();
            }

            Assert.That(campaign.Current.Day, Is.EqualTo(12), "the grill moved on");
            Assert.That(campaign.Restaurants[1].Day, Is.EqualTo(2), "the idle one's day didn't");
            Assert.That(campaign.Restaurants[1].DaysTowardsFee, Is.EqualTo(1), "nor its fee clock");
            Assert.That(JsonUtility.ToJson(campaign.Restaurants[1]), Is.EqualTo(idleBefore), "nothing about it changed");

            // Going there finds it as it was left, and the grill now waits in turn.
            var grillBefore = JsonUtility.ToJson(campaign.Restaurants[0]);
            campaign.Switch(1);
            Assert.That(campaign.Current.Name, Is.EqualTo("Harbour Room"));
            campaign.StoreCurrent(new RestaurantSave { DefinitionId = "harbour", Name = "Harbour Room", Day = 3 });
            Assert.That(JsonUtility.ToJson(campaign.Restaurants[0]), Is.EqualTo(grillBefore));
            Assert.That(campaign.Balance, Is.EqualTo(50f), "one pocket for both");
        }

        // ---- Migration ----

        [Test]
        public void AVersion1FileBecomesRestaurantOne()
        {
            var campaign = SaveFile.Parse(File.ReadAllText(Version1Fixture));

            Assert.That(campaign, Is.Not.Null);
            Assert.That(campaign.Version, Is.EqualTo(CampaignSave.CurrentVersion));

            // The campaign's own fields stay with it.
            Assert.That(campaign.Balance, Is.EqualTo(-120.5f));
            Assert.That(campaign.Loan, Is.EqualTo(0f), "there was no loan before Phase 6");
            Assert.That(campaign.PlayedSeconds, Is.EqualTo(2710.5f));
            Assert.That(campaign.LastPlayedTicks, Is.EqualTo(639000000000000000L));
            Assert.That(campaign.Cooks, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(campaign.RecentLines, Is.EqualTo(new[] { "Best {main} in town." }));
            Assert.That(campaign.Recipes, Is.EqualTo(new[] { "steak", "onions" }));

            // Everything else is restaurant 1's, and it's the one played next.
            Assert.That(campaign.Restaurants, Has.Count.EqualTo(1));
            Assert.That(campaign.CurrentRestaurant, Is.EqualTo(0));
            var first = campaign.Current;
            Assert.That(first.DefinitionId, Is.EqualTo(Version1Restaurant.GrillId));
            Assert.That(first.Name, Is.EqualTo("The Copper Pan"));
            Assert.That(first.StyleName, Is.EqualTo("Grill"));
            Assert.That(first.Area, Is.EqualTo("Old Town"));
            Assert.That(first.Day, Is.EqualTo(6));
            Assert.That(first.BrokenPlates, Is.EqualTo(1));
            Assert.That(first.DaysTowardsFee, Is.EqualTo(3));
            Assert.That(first.Period, Is.EqualTo(new[] { 310f, -85.5f, -30f, 0f }));
            Assert.That(first.History.Single().Served, Is.EqualTo(9));
            Assert.That(first.Stations.Select(s => s.SceneName), Is.EqualTo(new[] { "Fridge", "BarTable_01", "Bin" }));
            Assert.That(first.Stations[2].Count, Is.EqualTo(4));
            var steak = first.Items.Single();
            Assert.That((steak.TypeId, steak.Kind, steak.Station), Is.EqualTo(("Steak", ItemKind.Ingredient, 0)));
            Assert.That(steak.Ingredient.Seasoning, Is.EqualTo(0.25f));
            Assert.That(first.Staff.Single().Morale, Is.EqualTo(0.7f).Within(1e-6f));
            var review = first.Reviews.Single();
            Assert.That((review.Line, review.Guest, review.Facts.Main), Is.EqualTo(("Best steak in town.", "Mike S.", "steak")));
            Assert.That(first.Dishes.Single().Count, Is.EqualTo(21));
            Assert.That(first.Prices.Single().Price, Is.EqualTo(19));
            Assert.That(first.Dirt.Single().Station, Is.EqualTo(1));
        }

        [Test]
        public void AMigratedFileIsWrittenInTheNewShapeAndReadsTheSame()
        {
            Directory.CreateDirectory(_directory);
            var file = new SaveFile(_directory);
            File.Copy(Version1Fixture, file.Path);

            var migrated = file.Read();
            file.Write(migrated);
            var text = File.ReadAllText(file.Path);
            var again = file.Read();

            Assert.That(text, Does.Contain("\"Version\": 2"));
            Assert.That(text, Does.Contain("\"Restaurants\""));
            Assert.That(JsonUtility.ToJson(again), Is.EqualTo(JsonUtility.ToJson(migrated)), "read twice, nothing moves");
        }

        [Test]
        public void AFileFromANewerGameIsStillRefused()
        {
            var text = JsonUtility.ToJson(new CampaignSave { Version = CampaignSave.CurrentVersion + 1 });
            Assert.That(SaveFile.Parse(text), Is.Null);
        }

        // ---- Restaurant definitions ----

        [Test]
        public void ADefinitionBecomesSettingsAndANewRecord()
        {
            var definition = ScriptableObject.CreateInstance<RestaurantDefinition>();
            try
            {
                definition.Set("harbour", "Harbour Room", RestaurantSize.DiningRoom, 16, 4000f, 1.5f,
                    new[] { "hob", "fridge" }, "Seafood",
                    new[] { new RestaurantDefinition.OldReviewEntry(2f, "Tired fish.", "Ola K.") },
                    "Quay Street", new[] { "Fishmonger" });

                var settings = definition.ToSettings();
                Assert.That((settings.Id, settings.Size, settings.Seats, settings.Price), Is.EqualTo(("harbour", RestaurantSize.DiningRoom, 16, 4000f)));
                Assert.That(settings.InheritedAppliances, Is.EqualTo(new[] { "hob", "fridge" }));
                Assert.That(settings.Vendors, Is.EqualTo(new[] { "Fishmonger" }));
                Assert.That(settings.ScaleFee(new FeeSettings { Amount = 450f, EveryDays = 5 }).Amount, Is.EqualTo(675f));

                var kept = settings.NewRecord(keepOldReviews: true);
                Assert.That((kept.DefinitionId, kept.Name, kept.StyleName, kept.Area, kept.Day), Is.EqualTo(("harbour", "Harbour Room", "Seafood", "Quay Street", 1)));
                Assert.That(kept.Reviews.Single().Line, Is.EqualTo("Tired fish."));
                Assert.That(settings.NewRecord(keepOldReviews: false).Reviews, Is.Empty, "a new name starts with no reviews");
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void TheGrillDefinitionIsTheRestaurantOldSavesMigrateTo()
        {
            var grill = AssetDatabase.LoadAssetAtPath<RestaurantDefinition>("Assets/_Project/Data/Restaurants/RES_Grill.asset");
            Assert.That(grill, Is.Not.Null, "run Tools/Game/Build Restaurant Definitions");

            var settings = grill.ToSettings();
            Assert.That(settings.Id, Is.EqualTo(Version1Restaurant.GrillId));
            Assert.That(settings.Size, Is.EqualTo(RestaurantSize.Counter));
            Assert.That(settings.FeeScale, Is.EqualTo(1f), "the grill's fee is the one players already know");
            Assert.That(settings.Seats, Is.GreaterThan(0));
        }

        // The grill, played up to day 9, and a dining room bought later, played twice.
        private static CampaignSave TwoRestaurants()
        {
            var campaign = new CampaignSave { Balance = -250f, Loan = 1200f };
            campaign.Recipes.AddRange(new[] { "steak", "onions" });

            var grill = campaign.Add(new RestaurantSave { DefinitionId = Version1Restaurant.GrillId, Name = "The Copper Pan", Day = 9, DaysTowardsFee = 4, Period = new[] { 100f, -20f } });
            grill.Stations.Add(new StationRecord { FurnitureId = "hob", SceneName = "Hob_01", X = 1f, Z = 2f });
            grill.Items.Add(new ItemRecord { Station = 0, TypeId = "Steak", Kind = ItemKind.Ingredient });
            grill.Staff.Add(new StaffRecord { Name = "Ana", Role = 1, DailyWage = 30f });
            grill.Reviews.Add(new ReviewRecord { Day = 8, Stars = 4f, Line = "Good steak." });
            grill.Dirt.Add(new DirtRecord { Kind = 0, Station = -1, X = 1f, Z = 1f, Amount = 0.4f, Peak = 0.4f });
            grill.Prices.Add(new Game.Simulation.Cookbook.PriceRecord { Recipe = "steak", Price = 19 });

            var harbour = campaign.Add(new RestaurantSave { DefinitionId = "harbour", Name = "Harbour Room", Day = 2, DaysTowardsFee = 1 });
            harbour.Stations.Add(new StationRecord { FurnitureId = "table", SceneName = "Table_01", Chairs = 4 });
            harbour.Stations.Add(new StationRecord { FurnitureId = "table", SceneName = "Table_02", Chairs = 2 });
            harbour.Reviews.Add(new ReviewRecord { Day = 1, Stars = 5f, Line = "Fresh fish." });
            return campaign;
        }
    }
}
