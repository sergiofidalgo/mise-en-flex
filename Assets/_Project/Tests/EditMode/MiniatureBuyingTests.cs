using System;
using System.Linq;
using Game.Data;
using Game.Simulation.Campaign;
using Game.Simulation.Saving;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The city miniature and buying (Phase 6, milestone 2; decision 2026-11-10): a new campaign
    /// starts in the office owning nothing, with the grill's price and a first morning borrowed;
    /// buying names the restaurant (keeping the old name keeps its reviews), sets its style unless
    /// it's fixed, and spends campaign money, even into debt; choosing an owned one makes it current.
    /// </summary>
    public class MiniatureBuyingTests
    {
        private static RestaurantSettings Grill() => new RestaurantSettings
        {
            Id = Version1Restaurant.GrillId,
            Name = "The Copper Pan",
            Size = RestaurantSize.Counter,
            Seats = 3,
            Price = 400f,
            PreviousStyle = "Grill",
            FixedStyle = true,
            Street = "Old Town",
            OldReviews = new[] { new OldReview { Stars = 3f, Line = "Decent burger.", Guest = "Dave K." } },
        };

        private static RestaurantSettings DiningRoom() => new RestaurantSettings
        {
            Id = "casa-lucia",
            Name = "Casa Lucía",
            Size = RestaurantSize.DiningRoom,
            Seats = 16,
            Price = 6000f,
            PreviousStyle = "Spanish home cooking",
            Street = "Riverside",
            OldReviews = new[]
            {
                new OldReview { Stars = 4.5f, Line = "Tortilla like my grandmother's.", Guest = "Carmen L." },
                new OldReview { Stars = 2f, Line = "Cold croquetas.", Guest = "Ana M." },
            },
        };

        // ---- A new campaign ----

        [Test]
        public void ANewCampaignOwnsNothingAndHasBorrowedTheStartingLoan()
        {
            var campaign = RestaurantPurchase.NewCampaign(500f);
            Assert.That(campaign.Restaurants, Is.Empty);
            Assert.That(campaign.Current, Is.Null);
            Assert.That(campaign.Balance, Is.EqualTo(500f), "the loan is in the pocket");
            Assert.That(campaign.Loan, Is.EqualTo(500f), "and owed to the boss");
        }

        [Test]
        public void TheStartingLoanIsPaidBackInFiveInstalmentsWithoutTheBossesCut()
        {
            var campaign = RestaurantPurchase.NewCampaign(500f);
            var loan = campaign.ToLoan();
            Assert.That(loan.NextInstalment, Is.EqualTo(100f), "a fifth of it each collection");
            for (var i = 0; i < 5; i++)
                loan.TakeInstalment();
            Assert.That(loan.Owed, Is.EqualTo(0f), "five collections clear it");
        }

        [Test]
        public void BorrowingAtTheAccountantAndBuyingChangeTheSameCampaign()
        {
            var campaign = RestaurantPurchase.NewCampaign(500f);
            Assert.That(Game.Gameplay.Debt.AccountantBooks.Borrow(campaign, 250f, new LoanSettings()), Is.True);
            RestaurantPurchase.Buy(campaign, Grill(), "Golden Fork");

            Assert.That(campaign.Balance, Is.EqualTo(350f), "500 + 250 borrowed - 400 for the grill");
            Assert.That(campaign.Loan, Is.EqualTo(800f), "500 to start + 250 with the boss's 20%");
            Assert.That(campaign.ToLoan().NextInstalment, Is.EqualTo(160f), "the whole debt spread over five more");
        }

        [Test]
        public void BuyingTheGrillLeavesAFirstMorningsMoneyAndMakesItCurrent()
        {
            var campaign = RestaurantPurchase.NewCampaign(500f);
            var grill = RestaurantPurchase.Buy(campaign, Grill(), "Golden Fork");

            Assert.That(campaign.Balance, Is.EqualTo(100f));
            Assert.That(campaign.Loan, Is.EqualTo(500f), "buying doesn't repay anything");
            Assert.That(campaign.Restaurants.Single(), Is.SameAs(grill));
            Assert.That(campaign.Current, Is.SameAs(grill), "the first restaurant bought is the one played");
            Assert.That((grill.DefinitionId, grill.Area, grill.Day), Is.EqualTo((Version1Restaurant.GrillId, "Old Town", 1)));
        }

        [Test]
        public void BuyingCanGoIntoDebtWithTheBoss()
        {
            var campaign = RestaurantPurchase.NewCampaign(500f);
            RestaurantPurchase.Buy(campaign, Grill(), "");
            RestaurantPurchase.Buy(campaign, DiningRoom(), "");
            Assert.That(campaign.Balance, Is.EqualTo(100f - 6000f), "nothing is refused for its price: the boss is owed the rest");
            Assert.That(campaign.Restaurants.Count, Is.EqualTo(2));
        }

        [Test]
        public void ARestaurantCantBeBoughtTwice()
        {
            var campaign = RestaurantPurchase.NewCampaign(500f);
            RestaurantPurchase.Buy(campaign, Grill(), "");
            Assert.That(RestaurantPurchase.Owns(campaign, Grill()), Is.True);
            Assert.Throws<InvalidOperationException>(() => RestaurantPurchase.Buy(campaign, Grill(), "Again"));
            Assert.That(campaign.Balance, Is.EqualTo(100f), "the second attempt spent nothing");
        }

        // ---- Naming and old reviews ----

        [Test]
        public void KeepingTheOldNameKeepsTheOldReviews()
        {
            var campaign = RestaurantPurchase.NewCampaign(500f);
            var kept = RestaurantPurchase.Buy(campaign, DiningRoom(), "Casa Lucía");
            Assert.That(kept.Name, Is.EqualTo("Casa Lucía"));
            Assert.That(kept.Reviews.Select(r => r.Line), Is.EqualTo(new[] { "Tortilla like my grandmother's.", "Cold croquetas." }));
            Assert.That(kept.Reviews.All(r => r.Day == 0), Is.True, "written before the campaign: day 0");
        }

        [Test]
        public void AnEmptyNameOrTheOldOneWrittenDifferentlyKeepsTheOldName()
        {
            var empty = RestaurantPurchase.Buy(RestaurantPurchase.NewCampaign(0f), DiningRoom(), "   ");
            Assert.That((empty.Name, empty.Reviews.Count), Is.EqualTo(("Casa Lucía", 2)));

            var spaced = RestaurantPurchase.Buy(RestaurantPurchase.NewCampaign(0f), DiningRoom(), "  casa   lucía ");
            Assert.That((spaced.Name, spaced.Reviews.Count), Is.EqualTo(("Casa Lucía", 2)), "the sign keeps the old spelling");
        }

        [Test]
        public void ANewNameStartsWithNoReviews()
        {
            var renamed = RestaurantPurchase.Buy(RestaurantPurchase.NewCampaign(0f), DiningRoom(), "  Salt   &  Ember ");
            Assert.That(renamed.Name, Is.EqualTo("Salt & Ember"), "spaces tidied");
            Assert.That(renamed.Reviews, Is.Empty);
        }

        [Test]
        public void SuggestedNamesAreSeededAndLeaveOutTakenOnes()
        {
            var pool = new[] { "Golden Fork", "Crumb", "Casa Lucía", "Open Flame", "Lucky Spoon", "crumb" };
            var first = RestaurantNaming.Suggest(pool, seed: 7, count: 3, taken: new[] { "Casa Lucía" });
            Assert.That(first.Count, Is.EqualTo(3));
            Assert.That(first, Does.Not.Contain("Casa Lucía"));
            Assert.That(first.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(3), "no name twice, whatever its case");
            Assert.That(RestaurantNaming.Suggest(pool, seed: 7, count: 3, taken: new[] { "Casa Lucía" }), Is.EqualTo(first), "the same seed, the same names");
            Assert.That(RestaurantNaming.Suggest(pool, 1, 10).Count, Is.EqualTo(5), "only as many distinct names as the pool has");
        }

        // ---- The name picker ----

        [Test]
        public void ThePickerOffersTheOldNameFirstAndPicksItAtOnce()
        {
            var picker = new NamePicker("Casa Lucía", new[] { "Crumb", "Open Flame", "Lucky Spoon" }, 18);
            Assert.That(picker.Rows[0].Select(k => k.Kind), Is.EqualTo(new[] { NameKeyKind.Keep }));
            Assert.That(picker.Rows[1].Select(k => k.Text), Is.EqualTo(new[] { "Crumb", "Open Flame", "Lucky Spoon" }));
            Assert.That((picker.Row, picker.Column), Is.EqualTo((0, 0)), "keeping the name is one press away");
            Assert.That(picker.Press(), Is.EqualTo("Casa Lucía"));
            picker.Move(0, 1);
            Assert.That(picker.Selected.Kind, Is.EqualTo(NameKeyKind.Suggestion));
            Assert.That(picker.Press(), Is.EqualTo(picker.Selected.Text), "a suggestion is chosen at once too");
        }

        [Test]
        public void TypingCapitalisesEachWordAndFinishesWithDone()
        {
            var picker = new NamePicker("", Array.Empty<string>(), 18);
            Assert.That(picker.Selected.Kind, Is.EqualTo(NameKeyKind.Letter), "with nothing to pick, the cursor starts on the letters");
            Assert.That(picker.Move(0, -1), Is.False, "and the empty rows above are skipped");
            foreach (var c in "SALT")
                picker.Type(c.ToString());
            Go(picker, NameKeyKind.Space);
            Assert.That(picker.Press(), Is.Null);
            foreach (var c in "PAN")
                picker.Type(c.ToString());
            Assert.That(picker.Text, Is.EqualTo("Salt Pan"));
            Go(picker, NameKeyKind.Done);
            Assert.That(picker.Press(), Is.EqualTo("Salt Pan"));
        }

        [Test]
        public void DoneNeedsANameAndNamesStopAtTheLongest()
        {
            var picker = new NamePicker("", null, 5);
            Go(picker, NameKeyKind.Space);
            picker.Press();
            Assert.That(picker.Text, Is.Empty, "no leading space");
            Go(picker, NameKeyKind.Done);
            Assert.That(picker.Press(), Is.Null, "nothing typed: Done does nothing");
            foreach (var c in "ABCDEFG")
                picker.Type(c.ToString());
            Assert.That(picker.Text, Is.EqualTo("Abcde"));
            Assert.That(picker.Delete(), Is.True);
            Assert.That(picker.Text, Is.EqualTo("Abcd"));
        }

        [Test]
        public void TheCursorWrapsAlongRowsAndStopsAtTheTopAndBottom()
        {
            var picker = new NamePicker("Old", new[] { "A", "B" }, 18);
            Assert.That(picker.Move(0, -1), Is.False, "nothing above the old name");
            picker.Move(0, 1);
            picker.Move(-1, 0);
            Assert.That((picker.Row, picker.Column), Is.EqualTo((1, 1)), "left from the first suggestion wraps to the last");
            for (var i = 0; i < 10; i++)
                picker.Move(0, 1);
            Assert.That(new[] { NameKeyKind.Space, NameKeyKind.Delete, NameKeyKind.Done }, Does.Contain(picker.Selected.Kind));
            Assert.That(picker.Row, Is.EqualTo(picker.Rows.Count - 1));
        }

        // ---- Style ----

        [Test]
        public void TheGrillKeepsItsFixedStyleWhateverIsChosen()
        {
            var grill = RestaurantPurchase.Buy(RestaurantPurchase.NewCampaign(500f), Grill(), "Crumb", "Fine dining");
            Assert.That(grill.StyleName, Is.EqualTo("Grill"));
        }

        [Test]
        public void OtherRestaurantsTakeTheChosenStyleOrKeepTheirOwn()
        {
            Assert.That(RestaurantPurchase.Buy(RestaurantPurchase.NewCampaign(0f), DiningRoom(), "", "Bistro").StyleName, Is.EqualTo("Bistro"));
            Assert.That(RestaurantPurchase.Buy(RestaurantPurchase.NewCampaign(0f), DiningRoom(), "", null).StyleName, Is.EqualTo("Spanish home cooking"));
        }

        // ---- Choosing the current restaurant ----

        [Test]
        public void ChoosingAnOwnedRestaurantMakesItCurrent()
        {
            var campaign = RestaurantPurchase.NewCampaign(500f);
            var grill = RestaurantPurchase.Buy(campaign, Grill(), "");
            var dining = RestaurantPurchase.Buy(campaign, DiningRoom(), "");
            Assert.That(campaign.Current, Is.SameAs(grill), "buying a second one doesn't switch to it");

            Assert.That(RestaurantPurchase.Choose(campaign, "casa-lucia"), Is.True);
            Assert.That(campaign.Current, Is.SameAs(dining));
            Assert.That(RestaurantPurchase.Choose(campaign, Version1Restaurant.GrillId), Is.True);
            Assert.That(campaign.Current, Is.SameAs(grill));
        }

        [Test]
        public void ChoosingOneNotOwnedChangesNothing()
        {
            var campaign = RestaurantPurchase.NewCampaign(500f);
            Assert.That(RestaurantPurchase.Choose(campaign, "casa-lucia"), Is.False);
            RestaurantPurchase.Buy(campaign, Grill(), "");
            Assert.That(RestaurantPurchase.Choose(campaign, "grand-vesper"), Is.False);
            Assert.That(campaign.CurrentRestaurant, Is.EqualTo(0));
        }

        [Test]
        public void ABoughtRestaurantHasNeverOpenedUntilItsFirstDayIsSaved()
        {
            var record = RestaurantPurchase.Buy(RestaurantPurchase.NewCampaign(500f), Grill(), "");
            Assert.That(RestaurantPurchase.NeverOpened(record), Is.True, "its kitchen opens as built, on day 1");
            record.Stations.Add(new StationRecord { FurnitureId = "hob" });
            Assert.That(RestaurantPurchase.NeverOpened(record), Is.False);
        }

        [Test]
        public void ABoughtCampaignSurvivesTheSaveFile()
        {
            var campaign = RestaurantPurchase.NewCampaign(500f);
            RestaurantPurchase.Buy(campaign, Grill(), "Crumb");
            RestaurantPurchase.Buy(campaign, DiningRoom(), "");
            RestaurantPurchase.Choose(campaign, "casa-lucia");

            var read = Game.Gameplay.Saving.SaveFile.Parse(JsonUtility.ToJson(campaign));
            Assert.That(read.Restaurants.Select(r => r.Name), Is.EqualTo(new[] { "Crumb", "Casa Lucía" }));
            Assert.That(read.Current.DefinitionId, Is.EqualTo("casa-lucia"));
            Assert.That((read.Balance, read.Loan), Is.EqualTo((100f - 6000f, 500f)));
            Assert.That(read.Restaurants[1].Reviews.Count, Is.EqualTo(2));
        }

        // ---- Browsing the lots ----

        [Test]
        public void TheStickMovesToTheNearestLotThatWay()
        {
            // Three along the front, one behind the middle.
            var lots = new[] { (-1f, 0f), (0f, 0f), (1f, 0f), (0f, 1f) };
            Assert.That(LotNavigation.Next(lots, 1, 1f, 0f), Is.EqualTo(2));
            Assert.That(LotNavigation.Next(lots, 1, -1f, 0f), Is.EqualTo(0));
            Assert.That(LotNavigation.Next(lots, 1, 0f, 1f), Is.EqualTo(3));
            Assert.That(LotNavigation.Next(lots, 0, 0f, -1f), Is.EqualTo(0), "nothing that way: stay");
            Assert.That(LotNavigation.Next(lots, 3, 0.3f, -1f), Is.EqualTo(1), "a push a little off straight still finds the lot ahead");
        }

        // ---- The data ----

        [Test]
        public void TheMiniatureHasSixLotsCheapestFirstStartingWithAnAffordableGrill()
        {
            var config = AssetDatabase.LoadAssetAtPath<MiniatureConfig>("Assets/_Project/Data/Restaurants/MiniatureConfig.asset");
            Assert.That(config, Is.Not.Null, "run Tools/Game/Build Restaurant Definitions");
            var lots = config.LotSettings();
            Assert.That(lots.Count, Is.InRange(5, 6));
            Assert.That(lots[0].Id, Is.EqualTo(Version1Restaurant.GrillId));
            Assert.That(lots[0].FixedStyle, Is.True, "the first restaurant's style is fixed");
            Assert.That(lots.Select(l => l.Price), Is.Ordered, "cheapest first");
            Assert.That(lots.Select(l => l.Id).Distinct().Count(), Is.EqualTo(lots.Count), "ids are unique");
            Assert.That(config.StartingLoan, Is.GreaterThanOrEqualTo(lots[0].Price), "a new campaign can buy the grill");
            Assert.That(lots.Any(l => l.Size == RestaurantSize.DiningRoom), Is.True, "a mid-size dining room");
            Assert.That(lots.All(l => l.Seats > 0 && l.InheritedAppliances.Count > 0 && l.PreviousStyle.Length > 0), Is.True);
            Assert.That(lots.Skip(1).All(l => l.OldReviews.Count > 0), Is.True, "every lot on sale has old reviews to keep or lose");
            Assert.That(config.SuggestedNames.Count, Is.GreaterThanOrEqualTo(config.SuggestionsShown * 2), "enough for a reroll");
            Assert.That(config.SuggestedNames.All(n => n.Length <= config.MaxNameLength), Is.True);
            Assert.That(lots.All(l => l.Name.Length <= config.MaxNameLength), Is.True, "old names fit the card and sign");
        }

        // Moves the cursor onto the first key of that kind.
        private static void Go(NamePicker picker, NameKeyKind kind)
        {
            for (var r = 0; r < picker.Rows.Count; r++)
            for (var c = 0; c < picker.Rows[r].Count; c++)
            {
                if (picker.Rows[r][c].Kind != kind)
                    continue;
                while (picker.Row != r)
                    picker.Move(0, Math.Sign(r - picker.Row));
                while (picker.Column != c)
                    picker.Move(1, 0);
                return;
            }
            Assert.Fail($"No {kind} key");
        }
    }
}
