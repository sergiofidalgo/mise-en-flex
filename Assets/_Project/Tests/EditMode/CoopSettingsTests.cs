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
        public void DefaultsToTwoPlayers()
        {
            Assert.That(_settings.MaxPlayers, Is.EqualTo(2));
        }

        [Test]
        public void FirstTwoPlayersGetDifferentColours()
        {
            Assert.That(_settings.GetPlayerColor(0), Is.Not.EqualTo(_settings.GetPlayerColor(1)));
        }

        [Test]
        public void PlayerColoursWrapInsteadOfThrowing()
        {
            Assert.That(_settings.GetPlayerColor(100), Is.EqualTo(_settings.GetPlayerColor(100 % 4)));
        }
    }
}
