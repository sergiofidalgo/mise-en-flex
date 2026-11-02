using Game.Gameplay;
using Game.Gameplay.Stations;
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
        public void CarriesItemsInFrontAtChestHeight()
        {
            var interactor = _prefab.GetComponent<PlayerInteractor>();
            Assert.That(interactor, Is.Not.Null);

            var anchor = (Transform)new SerializedObject(interactor).FindProperty("_carryAnchor").objectReferenceValue;
            Assert.That(anchor, Is.Not.Null, "PlayerInteractor has no carry anchor");
            Assert.That(anchor.localPosition.z, Is.GreaterThan(0f), "Carried items should be in front of the chef");
            Assert.That(anchor.localPosition.y, Is.InRange(0.8f, 1.4f), "Carried items should be at chest height");
        }

        // Regression: Unity's default 0.3 m step and 45° slope limit let chefs climb onto crates.
        [Test]
        public void ChefCantStepOrClimb()
        {
            var controller = _prefab.GetComponent<CharacterController>();
            Assert.That(controller.stepOffset, Is.LessThanOrEqualTo(0.05f));
            Assert.That(controller.slopeLimit, Is.LessThanOrEqualTo(25f));
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
