using Game.Data;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class CoopSettingsTests
    {
        private CoopSettings _settings;

        [SetUp]
        public void CreateSettings() => _settings = ScriptableObject.CreateInstance<CoopSettings>();

        [TearDown]
        public void DestroySettings() => Object.DestroyImmediate(_settings);

        [Test]
        public void DefaultsToFourPlayers()
        {
            // Up to four local players (decision 2026-11-09).
            Assert.That(_settings.MaxPlayers, Is.EqualTo(4));
        }

        [Test]
        public void EveryPlayerGetsTheirOwnColour()
        {
            for (var a = 0; a < _settings.MaxPlayers; a++)
            for (var b = a + 1; b < _settings.MaxPlayers; b++)
                Assert.That(_settings.GetPlayerColor(a), Is.Not.EqualTo(_settings.GetPlayerColor(b)), $"P{a + 1} and P{b + 1}");
        }

        [Test]
        public void TheShippedSettingsAllowFourPlayers()
        {
            var shipped = UnityEditor.AssetDatabase.LoadAssetAtPath<CoopSettings>("Assets/_Project/Data/Settings/CoopSettings.asset");
            Assert.That(shipped, Is.Not.Null);
            Assert.That(shipped.MaxPlayers, Is.EqualTo(4));
        }

        [Test]
        public void PlayerColoursWrapInsteadOfThrowing()
        {
            Assert.That(_settings.GetPlayerColor(100), Is.EqualTo(_settings.GetPlayerColor(100 % 4)));
        }
    }
}
