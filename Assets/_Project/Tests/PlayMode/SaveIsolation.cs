using System.IO;
using Game.Gameplay.Saving;
using NUnit.Framework;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Every play test saves into a throwaway folder, never the player's real campaign, and starts
    /// as a new campaign with no title card.
    /// </summary>
    [SetUpFixture]
    public sealed class SaveIsolation
    {
        private string _directory;

        [OneTimeSetUp]
        public void UseTemporarySaves()
        {
            _directory = Path.Combine(Path.GetTempPath(), "MiseEnFlexPlayTests_" + System.Guid.NewGuid().ToString("N"));
            SaveFile.DirectoryOverride = _directory;
            CampaignLaunch.StartNew();
            // These tests drive a new kitchen's day by hand from Plan; the real start straight into
            // Prep is tested on its own (ForkcastSummaryPlayTests).
            Game.Gameplay.DayLoop.NewRestaurantsStartInPrep = false;
            // Their services open at once; the 4-3-2-1 before service is tested on its own
            // (ServiceCountdownPlayTests).
            Game.Gameplay.KitchenCompositionRoot.CountdownSecondsOverride = 0f;
        }

        [OneTimeTearDown]
        public void CleanUp()
        {
            SaveFile.DirectoryOverride = null;
            CampaignLaunch.Reset();
            Game.Gameplay.DayLoop.NewRestaurantsStartInPrep = true;
            Game.Gameplay.KitchenCompositionRoot.CountdownSecondsOverride = null;
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }
    }
}
