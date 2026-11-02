using Game.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class PlayerPrefabTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/PF_Player.prefab";

        private GameObject _prefab;

        [SetUp]
        public void LoadPrefab()
        {
            _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(_prefab, Is.Not.Null, $"Missing player prefab at {PlayerPrefabPath}; run Tools/Game/Build Greybox Kitchen");
        }

        [Test]
        public void HasMovementWithTuningAssigned()
        {
            var movement = _prefab.GetComponent<PlayerMovement>();
            Assert.That(movement, Is.Not.Null);

            var tuning = new SerializedObject(movement).FindProperty("_tuning").objectReferenceValue;
            Assert.That(tuning, Is.Not.Null, "PlayerMovement has no TuningConfig");
        }

        [Test]
        public void CharacterControllerStandsOnTheFloor()
        {
            // Origin at the feet (Blender pipeline rule): the capsule's bottom sits at y = 0.
            var controller = _prefab.GetComponent<CharacterController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.center.y - controller.height / 2f, Is.EqualTo(0f).Within(1e-4f));
        }
    }
}
