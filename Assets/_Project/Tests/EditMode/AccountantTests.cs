using System.IO;
using Game.Gameplay.Debt;
using Game.Gameplay.Saving;
using Game.Simulation.Campaign;
using Game.Simulation.Saving;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Borrowing at the accountant saves at once (§15.4), whether or not a day has been played.</summary>
    public class AccountantTests
    {
        private string _directory;

        [SetUp]
        public void MakeFolder()
        {
            _directory = Path.Combine(Path.GetTempPath(), "MiseEnFlexAccountantTest_" + System.Guid.NewGuid().ToString("N"));
            SaveFile.DirectoryOverride = _directory;
        }

        [TearDown]
        public void RemoveFolder()
        {
            SaveFile.DirectoryOverride = null;
            CampaignLaunch.Reset();
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void ALoanIsSavedAtOnceWithTheMoneyAndTheDebt()
        {
            var campaign = new CampaignSave { Balance = 120f };
            campaign.Add(new RestaurantSave { DefinitionId = "grill", Day = 3 });
            SaveSlots.File(1).Write(campaign);

            var read = AccountantBooks.Read(1, 100f);
            Assert.IsTrue(AccountantBooks.Borrow(1, read, 500f, new LoanSettings()));

            var back = SaveSlots.File(1).Read();
            Assert.AreEqual(620f, back.Balance);
            Assert.AreEqual(600f, back.Loan);
            Assert.AreEqual(120f, back.LoanInstalment);
            Assert.AreEqual(3, back.Current.Day, "the restaurant is untouched");
            Assert.IsTrue(CampaignLaunch.WantsSave && CampaignLaunch.Slot == 1, "the kitchen loads it next");
        }

        [Test]
        public void ANewCampaignBorrowsOnTopOfItsStartingMoney()
        {
            var read = AccountantBooks.Read(0, 100f);
            Assert.IsTrue(AccountantBooks.Borrow(0, read, 250f, new LoanSettings()));
            var back = SaveSlots.File(0).Read();
            Assert.AreEqual(350f, back.Balance);
            Assert.AreEqual(300f, back.Loan);
            Assert.IsNull(back.Current, "no restaurant yet");
        }

        [Test]
        public void AnAmountNotOnOfferChangesNothing()
        {
            var read = AccountantBooks.Read(0, 100f);
            Assert.IsFalse(AccountantBooks.Borrow(0, read, 333f, new LoanSettings()));
            Assert.IsFalse(SaveSlots.File(0).Exists);
            Assert.AreEqual(100f, read.Balance);
        }
    }
}
