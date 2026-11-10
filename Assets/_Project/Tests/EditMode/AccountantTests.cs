using System.IO;
using Game.Data;
using Game.Gameplay.Office;
using Game.Gameplay.Saving;
using Game.Simulation.Campaign;
using Game.Simulation.Saving;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Borrowing at the accountant saves at once (§15.4), whether or not a day has been played. It goes
    /// through the office's one copy of the campaign (<see cref="OfficeCampaign"/>), which the
    /// miniature buys with too, so neither writes over the other.
    /// </summary>
    public class AccountantTests
    {
        private string _directory;
        private GameObject _office;
        private MiniatureConfig _config;

        [SetUp]
        public void MakeFolder()
        {
            _directory = Path.Combine(Path.GetTempPath(), "MiseEnFlexAccountantTest_" + System.Guid.NewGuid().ToString("N"));
            SaveFile.DirectoryOverride = _directory;
        }

        [TearDown]
        public void RemoveFolder()
        {
            if (_office != null)
                Object.DestroyImmediate(_office);
            if (_config != null)
                Object.DestroyImmediate(_config);
            SaveFile.DirectoryOverride = null;
            CampaignLaunch.Reset();
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        // The office's campaign, starting new campaigns with this loan from the boss.
        private OfficeCampaign Office(float startingLoan)
        {
            _config = ScriptableObject.CreateInstance<MiniatureConfig>();
            _config.Set(null, startingLoan, null);
            _office = new GameObject("Office");
            var office = _office.AddComponent<OfficeCampaign>();
            var wiring = new SerializedObject(office);
            wiring.FindProperty("_config").objectReferenceValue = _config;
            wiring.ApplyModifiedPropertiesWithoutUndo();
            return office;
        }

        [Test]
        public void ALoanIsSavedAtOnceWithTheMoneyAndTheDebt()
        {
            var campaign = new CampaignSave { Balance = 120f };
            campaign.Add(new RestaurantSave { DefinitionId = "grill", Day = 3 });
            SaveSlots.File(1).Write(campaign);
            CampaignLaunch.Continue(1);

            Assert.IsTrue(Office(500f).Lend(500f, new LoanSettings()));

            var back = SaveSlots.File(1).Read();
            Assert.AreEqual(620f, back.Balance);
            Assert.AreEqual(600f, back.Loan);
            Assert.AreEqual(120f, back.LoanInstalment);
            Assert.AreEqual(3, back.Current.Day, "the restaurant is untouched");
            Assert.IsTrue(CampaignLaunch.WantsSave && CampaignLaunch.Slot == 1, "the kitchen loads it next");
        }

        [Test]
        public void ANewCampaignBorrowsOnTopOfItsStartingLoan()
        {
            CampaignLaunch.StartNew(0);
            Assert.IsTrue(Office(500f).Lend(250f, new LoanSettings()));

            var back = SaveSlots.File(0).Read();
            Assert.AreEqual(750f, back.Balance, "500 from the start + 250 borrowed");
            Assert.AreEqual(800f, back.Loan, "500 to start + 250 with the boss's 20%");
            Assert.AreEqual(160f, back.LoanInstalment, "the whole debt over five collections");
            Assert.IsNull(back.Current, "no restaurant yet");
            Assert.IsTrue(CampaignLaunch.WantsSave && CampaignLaunch.Slot == 0, "the kitchen loads it next");
        }

        [Test]
        public void AnAmountNotOnOfferChangesNothing()
        {
            CampaignLaunch.StartNew(0);
            var office = Office(100f);
            Assert.IsFalse(office.Lend(333f, new LoanSettings()));
            Assert.IsFalse(SaveSlots.File(0).Exists);
            Assert.AreEqual(100f, office.Campaign.Balance);
        }
    }
}
