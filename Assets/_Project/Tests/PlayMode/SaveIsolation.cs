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
        }

        [OneTimeTearDown]
        public void CleanUp()
        {
            SaveFile.DirectoryOverride = null;
            CampaignLaunch.Reset();
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }
    }
}
