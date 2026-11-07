using Game.Data;
using Game.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class SoundTests
    {
        private const string BankPath = "Assets/_Project/Data/Settings/SoundBank.asset";

        [Test]
        public void APanBelowTheSizzlePointIsSilent()
        {
            var level = SoundMath.SizzleLevel(90f, 110f, 240f);
            Assert.That(SoundMath.SizzleVolume(level, 0.2f, 0.65f), Is.EqualTo(0f));
        }

        [Test]
        public void ABarelySizzlingPanIsStillClearlyAudible()
        {
            var level = SoundMath.SizzleLevel(111f, 110f, 240f);
            Assert.That(SoundMath.SizzleVolume(level, 0.2f, 0.65f), Is.GreaterThanOrEqualTo(0.2f));
        }

        [Test]
        public void AHotterPanIsLouder()
        {
            var warm = SoundMath.SizzleVolume(SoundMath.SizzleLevel(150f, 110f, 240f), 0.2f, 0.65f);
            var hot = SoundMath.SizzleVolume(SoundMath.SizzleLevel(220f, 110f, 240f), 0.2f, 0.65f);
            Assert.That(hot, Is.GreaterThan(warm));
        }

        [Test]
        public void SizzleStopsRisingAtFull()
        {
            Assert.That(SoundMath.SizzleLevel(280f, 110f, 240f), Is.EqualTo(1f));
        }

        [TestCase(0.5f, 0f)]
        [TestCase(0f, -0.5f)]
        [TestCase(1f, 0.5f)]
        [TestCase(-2f, -0.5f)] // off screen stays at the edge
        [TestCase(3f, 0.5f)]
        public void SoundsPanWithTheirPlaceOnScreen(float viewportX, float pan)
        {
            Assert.That(SoundMath.StereoPan(viewportX, 0.5f), Is.EqualTo(pan).Within(0.0001f));
        }

        [Test]
        public void ACueCanAlwaysPlayTheFirstTime()
        {
            Assert.That(SoundMath.CanPlay(0f, null, 10f), Is.True);
        }

        [Test]
        public void ACueWaitsOutItsInterval()
        {
            Assert.That(SoundMath.CanPlay(1.05f, 1f, 0.1f), Is.False);
            Assert.That(SoundMath.CanPlay(1.2f, 1f, 0.1f), Is.True);
        }

        [Test]
        public void TheSameTakeNeverPlaysTwiceInARow()
        {
            for (var last = 0; last < 4; last++)
            {
                for (var roll = 0f; roll <= 1f; roll += 0.05f)
                {
                    var take = SoundMath.NextTake(4, last, roll);
                    Assert.That(take, Is.Not.EqualTo(last));
                    Assert.That(take, Is.InRange(0, 3));
                }
            }
        }

        [Test]
        public void EveryTakeCanComeUp()
        {
            Assert.That(SoundMath.NextTake(3, -1, 0f), Is.EqualTo(0));
            Assert.That(SoundMath.NextTake(3, -1, 0.5f), Is.EqualTo(1));
            Assert.That(SoundMath.NextTake(3, -1, 1f), Is.EqualTo(2));
        }

        [Test]
        public void ASingleTakeAlwaysPlays()
        {
            Assert.That(SoundMath.NextTake(1, 0, 0.7f), Is.EqualTo(0));
        }

        [Test]
        public void TheBankHasAClipForEverySound()
        {
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            Assert.That(bank, Is.Not.Null, "Run Tools/Game/Update Sound Bank.");

            foreach (var cue in new[] { bank.Bell, bank.ChopClean, bank.ChopOff, bank.ChopMiss, bank.DoorOpen, bank.DoorClose, bank.SizzleHit })
            {
                Assert.That(cue.Clips, Is.Not.Empty);
                Assert.That(cue.Clips, Has.None.Null);
            }
            Assert.That(bank.SizzleLoop, Is.Not.Null);
        }

        [Test]
        public void TheSizzleLoopIsStoredWithoutCompression()
        {
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(bank.SizzleLoop));
            Assert.That(importer.defaultSampleSettings.compressionFormat, Is.EqualTo(AudioCompressionFormat.PCM));
        }
    }
}
