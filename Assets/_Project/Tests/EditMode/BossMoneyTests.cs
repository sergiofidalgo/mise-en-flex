using System.Linq;
using Game.Simulation.Campaign;
using Game.Simulation.Cookbook;
using Game.Simulation.Days;
using Game.Simulation.Saving;
using Game.Simulation.Service;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The boss and money trouble (Phase 6, milestone 4; decisions 2026-11-10): the accountant's
    /// loan, the collection (the restaurant's cut plus one instalment) and the escalation, one step
    /// per missed collection and reset by paying in full.
    /// </summary>
    public class BossMoneyTests
    {
        private static readonly LoanSettings Terms = new LoanSettings();

        // ---- The loan ----

        [Test]
        public void BorrowingAddsTheBossesTwentyPercentAndSpreadsItOverFiveInstalments()
        {
            var loan = new LoanAccount();
            Assert.IsTrue(loan.Borrow(1000f, Terms));
            Assert.AreEqual(1200f, loan.Owed);
            Assert.AreEqual(240f, loan.Instalment);
        }

        [Test]
        public void BorrowingAgainSpreadsTheWholeDebtOverFiveMore()
        {
            var loan = new LoanAccount();
            loan.Borrow(1000f, Terms);
            loan.TakeInstalment(); // 960 left
            loan.Borrow(250f, Terms); // + 300
            Assert.AreEqual(1260f, loan.Owed);
            Assert.AreEqual(252f, loan.Instalment);
        }

        [Test]
        public void AmountsComeInStepsOf250UpToTwoThousandBorrowed()
        {
            var loan = new LoanAccount();
            CollectionAssert.AreEqual(new[] { 250f, 500f, 750f, 1000f, 1250f, 1500f, 1750f, 2000f }, loan.AmountsOnOffer(Terms).ToArray());
            loan.Borrow(1500f, Terms);
            CollectionAssert.AreEqual(new[] { 250f, 500f }, loan.AmountsOnOffer(Terms).ToArray());
            loan.Borrow(500f, Terms);
            Assert.IsEmpty(loan.AmountsOnOffer(Terms));
        }

        [Test]
        public void OnlyWholeStepsWithinTheCapCanBeBorrowed()
        {
            var loan = new LoanAccount();
            Assert.IsFalse(loan.Borrow(300f, Terms));
            Assert.IsFalse(loan.Borrow(2250f, Terms));
            Assert.IsFalse(loan.Borrow(0f, Terms));
            Assert.AreEqual(0f, loan.Owed);
        }

        [Test]
        public void FiveInstalmentsPayItOffAndTheLastMayBeSmaller()
        {
            var loan = new LoanAccount();
            loan.Borrow(250f, Terms); // 300 owed, 60 each
            for (var i = 0; i < 5; i++)
                Assert.AreEqual(60f, loan.TakeInstalment());
            Assert.AreEqual(0f, loan.Owed);
            Assert.AreEqual(0f, loan.TakeInstalment());

            var odd = new LoanAccount(100f, 60f);
            Assert.AreEqual(60f, odd.TakeInstalment());
            Assert.AreEqual(40f, odd.NextInstalment);
        }

        // ---- The collection ----

        [Test]
        public void ACollectionIsTheCutPlusOneInstalment()
        {
            var loan = new LoanAccount(1200f, 240f);
            var collection = Collection.Take(1000f, 450f, loan);
            Assert.AreEqual(690f, collection.Due);
            Assert.AreEqual(310f, collection.BalanceAfter);
            Assert.AreEqual(690f, collection.Paid);
            Assert.IsTrue(collection.PaidInFull);
            Assert.AreEqual(960f, loan.Owed);
        }

        [Test]
        public void WithNoLoanItsJustTheCut()
        {
            var collection = Collection.Take(450f, 450f, new LoanAccount());
            Assert.AreEqual(450f, collection.Due);
            Assert.IsTrue(collection.PaidInFull, "exactly enough is enough");
        }

        [Test]
        public void PartialPaymentTakesWhatsThereAndTheRestIsOwed()
        {
            var collection = Collection.Take(300f, 450f, new LoanAccount(1200f, 240f));
            Assert.AreEqual(300f, collection.Paid);
            Assert.AreEqual(390f, collection.Shortfall);
            Assert.AreEqual(-390f, collection.BalanceAfter);
            Assert.IsFalse(collection.PaidInFull);
        }

        [Test]
        public void OldDebtMustBeClearedTooForAFullPayment()
        {
            var collection = Collection.Take(-100f, 450f, new LoanAccount());
            Assert.AreEqual(0f, collection.Paid);
            Assert.AreEqual(550f, collection.Shortfall);
            Assert.IsFalse(collection.PaidInFull);
        }

        [Test]
        public void APreviewLeavesTheLoanAlone()
        {
            var loan = new LoanAccount(1200f, 240f);
            var forecast = Collection.Preview(100f, 450f, loan);
            Assert.AreEqual(690f, forecast.Due);
            Assert.AreEqual(1200f, loan.Owed);
        }

        [Test]
        public void TheDayCycleTakesTheCollectionAsReviewStarts()
        {
            var days = new DayCycle(1000f, new FeeSettings { Amount = 450f, EveryDays = 2 }, new LoanAccount(1200f, 240f));
            PlayDay(days);
            Assert.IsNull(days.LastCollection);
            Assert.AreEqual(690f, days.NextCollection.Due);
            days.Advance();
            PlayDay(days);
            Assert.IsTrue(days.LastCollection.HasValue);
            Assert.AreEqual(310f, days.Balance);
            Assert.AreEqual(450f, days.Today.Of(LedgerLine.Fee));
            Assert.AreEqual(240f, days.Today.Of(LedgerLine.Loan));
            Assert.AreEqual(960f, days.Loan.Owed);
            days.Advance();
            Assert.IsNull(days.LastCollection, "a new day forgets yesterday's collection");
        }

        [Test]
        public void RepossessedAppliancesCountAsMoneyIn()
        {
            var days = new DayCycle(-300f);
            days.CreditRepossessed(200f);
            Assert.AreEqual(-100f, days.Balance);
            Assert.IsTrue(Ledger.IsIncome(LedgerLine.Repossessed));
            Assert.IsFalse(Ledger.IsIncome(LedgerLine.Loan));
        }

        // ---- Escalation ----

        private static Collection Missed => new Collection(450f, 0f, 100f);
        private static Collection Paid => new Collection(450f, 0f, 1000f);

        [Test]
        public void EachMissedCollectionIsOneStep()
        {
            var boss = new BossEscalation();
            Assert.AreEqual(BossVisit.Warning, boss.Record(Missed, 2, "grill"));
            Assert.AreEqual(BossVisit.Repossession, boss.Record(Missed, 2, "grill"));
            Assert.AreEqual(BossVisit.TakeRestaurant, boss.Record(Missed, 2, "diner"));
            Assert.AreEqual("diner", boss.PendingAt);
            Assert.AreEqual(BossVisit.Erasure, boss.Record(Missed, 1, "grill"));
            Assert.IsTrue(boss.IsErased);
            Assert.AreEqual(BossVisit.Erasure, boss.Record(Paid, 1, "grill"), "erasure is final");
        }

        [Test]
        public void WithOneRestaurantStepThreeStripsItInstead()
        {
            var boss = new BossEscalation(BossStep.Repossessed);
            Assert.AreEqual(BossVisit.StripToMinimum, boss.Record(Missed, 1, "grill"));
            Assert.AreEqual(BossStep.RestaurantTaken, boss.Step);
        }

        [Test]
        public void PayingInFullResetsItAndCancelsTheVisit()
        {
            var boss = new BossEscalation();
            boss.Record(Missed, 1, "grill");
            boss.Record(Missed, 1, "grill");
            Assert.AreEqual(BossVisit.None, boss.Record(Paid, 1, "grill"));
            Assert.AreEqual(BossStep.None, boss.Step);
            Assert.AreEqual(BossVisit.None, boss.Pending);
            Assert.AreEqual(BossVisit.Warning, boss.Record(Missed, 1, "grill"), "the ladder starts again");
        }

        [Test]
        public void AVisitMadeIsNoLongerPending()
        {
            var boss = new BossEscalation();
            boss.Record(Missed, 1, "grill");
            boss.Visited();
            Assert.AreEqual(BossVisit.None, boss.Pending);
            Assert.AreEqual(BossStep.Warned, boss.Step, "the step stays until paid in full");
        }

        [Test]
        public void TheCallMatchesWhatHappened()
        {
            Assert.AreEqual(BossCallKind.Paid, BossCalls.After(Paid, BossStep.None));
            Assert.AreEqual(BossCallKind.BackInGoodBooks, BossCalls.After(Paid, BossStep.Repossessed));
            Assert.AreEqual(BossCallKind.Missed, BossCalls.After(Missed, BossStep.None));
            Assert.IsTrue(BossCalls.ShouldWarnNear(1, Missed));
            Assert.IsFalse(BossCalls.ShouldWarnNear(1, Paid));
            Assert.IsFalse(BossCalls.ShouldWarnNear(3, Missed));
            foreach (BossVisit visit in System.Enum.GetValues(typeof(BossVisit)))
                if (visit != BossVisit.None)
                {
                    Assert.IsNotEmpty(BossCalls.Lines(BossCallKind.Missed, visit), visit.ToString());
                    Assert.IsNotEmpty(BossCalls.VisitLines(visit), visit.ToString());
                }
        }

        // ---- Repossession ----

        private static Appliance[] Room() => new[]
        {
            new Appliance(0, ApplianceRole.Hob, 120f),
            new Appliance(1, ApplianceRole.Hob, 200f),
            new Appliance(2, ApplianceRole.Board, 40f),
            new Appliance(3, ApplianceRole.Pass, 0f, forSale: false),
            new Appliance(4, ApplianceRole.Storage, 150f),
            new Appliance(5, ApplianceRole.Storage, 90f),
            new Appliance(6, ApplianceRole.Table, 60f),
            new Appliance(7, ApplianceRole.Table, 60f),
            new Appliance(8, ApplianceRole.Other, 30f),
        };

        [Test]
        public void RepossessionTakesTheMostValuableUntilTheShortfallIsCovered()
        {
            var taken = Repossession.ForShortfall(Room(), 250f);
            CollectionAssert.AreEqual(new[] { 1, 4 }, taken.Select(a => a.Index).ToArray());
            Assert.AreEqual(350f, Repossession.Credit(taken));
        }

        [Test]
        public void RepossessionNeverTakesTheBareMinimum()
        {
            var taken = Repossession.ForShortfall(Room(), 10000f).Select(a => a.Index).ToList();
            CollectionAssert.DoesNotContain(taken, 0, "the cheapest hob stays");
            CollectionAssert.DoesNotContain(taken, 2, "the only board stays");
            CollectionAssert.DoesNotContain(taken, 3, "the pass is the building's");
            CollectionAssert.DoesNotContain(taken, 5, "the cheapest storage stays");
            Assert.AreEqual(1, taken.Count(i => i == 6 || i == 7), "one table stays");
        }

        [Test]
        public void NoShortfallTakesNothing()
        {
            Assert.IsEmpty(Repossession.ForShortfall(Room(), 0f));
        }

        [Test]
        public void StrippingTakesEverythingButTheMinimum()
        {
            var taken = Repossession.StripToMinimum(Room()).Select(a => a.Index).OrderBy(i => i).ToArray();
            CollectionAssert.AreEqual(new[] { 1, 4, 7, 8 }, taken);
        }

        // ---- The save ----

        [Test]
        public void TheLoanAndEscalationRoundTripThroughJson()
        {
            var save = new CampaignSave();
            var loan = new LoanAccount();
            loan.Borrow(1000f, Terms);
            save.StoreLoan(loan);
            var boss = new BossEscalation();
            boss.Record(Missed, 1, "grill");
            save.StoreEscalation(boss);

            var back = JsonUtility.FromJson<CampaignSave>(JsonUtility.ToJson(save));
            Assert.AreEqual(1200f, back.ToLoan().Owed);
            Assert.AreEqual(240f, back.ToLoan().Instalment);
            Assert.AreEqual(BossStep.Warned, back.ToEscalation().Step);
            Assert.AreEqual(BossVisit.Warning, back.ToEscalation().Pending);
            Assert.AreEqual("grill", back.ToEscalation().PendingAt);
        }

        [Test]
        public void AFileFromBeforeTheBossLoadsWithNothingOwed()
        {
            var save = JsonUtility.FromJson<CampaignSave>("{\"Version\":2,\"Balance\":50}");
            Assert.IsTrue(SaveMigration.Upgrade(save));
            Assert.AreEqual(0f, save.ToLoan().Owed);
            Assert.AreEqual(BossStep.None, save.ToEscalation().Step);
            Assert.AreEqual(BossVisit.None, save.ToEscalation().Pending);
        }

        [Test]
        public void TheBossTakesARestaurantButNeverTheLast()
        {
            var save = new CampaignSave();
            save.Add(new RestaurantSave { DefinitionId = "grill" });
            save.Add(new RestaurantSave { DefinitionId = "diner" });
            save.Switch(1);
            Assert.IsTrue(save.Remove("diner"));
            Assert.AreEqual("grill", save.Current.DefinitionId);
            Assert.IsFalse(save.Remove("grill"), "the last one is stripped, not taken");
        }

        [Test]
        public void ErasedChefsOutliveCampaignsInTheCookbook()
        {
            var book = new SharedCookbook();
            book.Erased.Add(new ErasedChef { Restaurant = "The Copper Pan", Days = 12 });
            var back = JsonUtility.FromJson<SharedCookbook>(JsonUtility.ToJson(book));
            Assert.IsTrue(back.Upgrade());
            Assert.AreEqual("The Copper Pan", back.Erased.Single().Restaurant);

            var old = JsonUtility.FromJson<SharedCookbook>("{\"Version\":1,\"Unlocked\":[\"steak\"]}");
            Assert.IsTrue(old.Upgrade());
            Assert.IsNotNull(old.Erased);
        }

        private static void PlayDay(DayCycle days)
        {
            days.Advance();
            days.Advance();
            days.FinishService(ServiceSummary.From(new ServiceSimulation(new ServiceSettings(), seatCount: 1)));
        }
    }
}
