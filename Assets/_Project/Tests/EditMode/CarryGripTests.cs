using System.Linq;
using Game.Gameplay.Items;
using Game.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The chef's hands hold what's carried from outside it (the user's note, 2026-11-10: hands
    /// went through a tray): with each real item in a real player's hands, both hands sit outside
    /// the item's shape, and the item stays clear of the chef's body.
    /// </summary>
    public class CarryGripTests
    {
        private const string PlayerPath = "Assets/_Project/Prefabs/PF_Player.prefab";
        private const string ItemsFolder = "Assets/_Project/Prefabs/Items";

        private GameObject _player;
        private GameObject _item;

        [TearDown]
        public void TearDown()
        {
            if (_item != null)
                Object.DestroyImmediate(_item);
            if (_player != null)
                Object.DestroyImmediate(_player);
        }

        [TestCase("Tray", ItemView.CarryGrip.Under)]
        [TestCase("Plate", ItemView.CarryGrip.Under)]
        [TestCase("Container", ItemView.CarryGrip.Sides)]
        [TestCase("Onion", ItemView.CarryGrip.Sides)]
        [TestCase("Tomato", ItemView.CarryGrip.Sides)]
        [TestCase("Steak", ItemView.CarryGrip.Sides)]
        public void HandsHoldItFromOutsideAndItClearsTheBody(string typeId, ItemView.CarryGrip grip)
        {
            _player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
            var arms = _player.GetComponentInChildren<ChefArmsView>(true);
            Assert.That(arms, Is.Not.Null, "The player has no ChefArmsView.");
            var wiring = new SerializedObject(arms);
            var anchor = (Transform)wiring.FindProperty("_carryAnchor").objectReferenceValue;
            var shoulders = new[]
            {
                (Transform)wiring.FindProperty("_leftShoulder").objectReferenceValue,
                (Transform)wiring.FindProperty("_rightShoulder").objectReferenceValue,
            };
            var palm = wiring.FindProperty("_handRadius").floatValue * shoulders[0].lossyScale.y;
            var clearance = wiring.FindProperty("_bodyClearance").floatValue * shoulders[0].lossyScale.y;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ItemsFolder}/PF_Item_{typeId}.prefab");
            Assert.That(prefab, Is.Not.Null, $"No item prefab for {typeId}.");
            _item = Object.Instantiate(prefab, anchor, false);
            var view = _item.GetComponent<ItemView>();
            Assert.That(view.Grip, Is.EqualTo(grip), $"{typeId} is held the wrong way.");
            arms.PreviewHolding(pan: false);

            var shape = view.Shape;
            var bodyFront = arms.transform.InverseTransformPoint(
                (shoulders[0].position + shoulders[1].position) * 0.5f).z + clearance / arms.transform.lossyScale.z;
            foreach (var shoulder in shoulders)
            {
                var hand = shoulder.GetComponentsInChildren<MeshFilter>(true)
                    .First(f => f.sharedMesh != null && f.sharedMesh.name.StartsWith("CharHand"));
                var centre = hand.transform.TransformPoint(hand.sharedMesh.bounds.center);
                // How far the hand's middle is from the item's shape, in metres.
                var local = _item.transform.InverseTransformPoint(centre);
                var closest = _item.transform.TransformPoint(shape.ClosestPoint(local));
                var inside = shape.Contains(local);
                Assert.That(inside, Is.False, $"{shoulder.name}'s hand is inside the {typeId}.");
                Assert.That(Vector3.Distance(centre, closest), Is.GreaterThanOrEqualTo(palm * 0.8f),
                    $"{shoulder.name}'s hand sinks into the {typeId}.");
            }

            // The item's back, in front of the chef's front.
            var back = arms.transform.InverseTransformPoint(_item.transform.TransformPoint(
                new Vector3(shape.center.x, shape.center.y, shape.min.z))).z;
            Assert.That(back, Is.GreaterThanOrEqualTo(bodyFront - 0.01f), $"The {typeId} goes into the chef.");
        }
    }
}
