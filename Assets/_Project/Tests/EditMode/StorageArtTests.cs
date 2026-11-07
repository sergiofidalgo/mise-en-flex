using System.Linq;
using Game.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The fridge, shelf and chef models fit their stations, and the motions that use them (the
    /// fridge door, the chef bending) are wired and timed as intended.
    /// </summary>
    public class StorageArtTests
    {
        private const string ModelsFolder = "Assets/_Project/Art/Models";
        private const float Tolerance = 0.005f;
        private const float Frame = 1f / 60f;

        private static Mesh Model(string file, string meshName)
        {
            var path = $"{ModelsFolder}/{file}.fbx";
            var mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault(m => m.name == meshName);
            Assert.That(mesh, Is.Not.Null, $"{path} has no mesh called {meshName}");
            return mesh;
        }

        private static GameObject Prefab(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        [Test]
        public void SwingReachesEachEndInItsOwnTime()
        {
            var amount = 0f;
            for (var t = 0f; t < 0.25f - Frame / 2f; t += Frame)
                amount = Swing.Step(amount, true, onSeconds: 0.25f, offSeconds: 0.5f, Frame);
            Assert.That(amount, Is.EqualTo(1f).Within(0.01f));

            for (var t = 0f; t < 0.25f - Frame / 2f; t += Frame)
                amount = Swing.Step(amount, false, onSeconds: 0.25f, offSeconds: 0.5f, Frame);
            Assert.That(amount, Is.EqualTo(0.5f).Within(0.02f), "Closing takes twice as long here.");
        }

        [Test]
        public void EaseOutBackStartsAndEndsExactlyAndOvershootsBetween()
        {
            Assert.That(Swing.EaseOutBack(0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(Swing.EaseOutBack(1f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(Swing.EaseOutBack(0.8f), Is.GreaterThan(1f));
        }

        [Test]
        public void StorageBodiesFillTheirStations([Values("SM_Fridge/FridgeBody", "SM_Shelf/ShelfBody")] string model)
        {
            var parts = model.Split('/');
            var bounds = Model(parts[0], parts[1]).bounds;
            Assert.That(bounds.size.x, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(bounds.size.y, Is.EqualTo(0.9f).Within(Tolerance));
            Assert.That(bounds.size.z, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void FridgeDoorHangsFromItsHingeAndOpensTowardsTheCamera()
        {
            var bounds = Model("SM_Fridge", "FridgeDoor").bounds;
            Assert.That(bounds.min.x, Is.EqualTo(0f).Within(Tolerance), "The hinge is at the door's origin, on its left.");
            Assert.That(bounds.max.x, Is.EqualTo(0.84f).Within(Tolerance));
            Assert.That(bounds.max.z, Is.LessThanOrEqualTo(Tolerance), "The door sits in front of its hinge, towards the camera.");

            var fridge = Prefab("Assets/_Project/Prefabs/Furniture/PF_Fridge.prefab");
            var door = fridge.transform.Find("Door");
            Assert.That(door.localPosition.x + bounds.max.x, Is.LessThanOrEqualTo(0.5f), "The closed door stays within the footprint.");
            Assert.That(door.localPosition.z - bounds.size.z, Is.GreaterThanOrEqualTo(-0.5f));
        }

        [Test]
        public void FridgeIsWiredToOpenAndShelfIsNot()
        {
            var fridge = Prefab("Assets/_Project/Prefabs/Furniture/PF_Fridge.prefab");
            var shelf = Prefab("Assets/_Project/Prefabs/Furniture/PF_Shelf.prefab");
            Assert.That(fridge.GetComponent<FridgeDoorView>(), Is.Not.Null);
            Assert.That(shelf.GetComponent<FridgeDoorView>(), Is.Null);
            Assert.That(fridge.transform.Find("Door").localRotation, Is.EqualTo(Quaternion.identity), "Built shut.");
        }

        [Test]
        public void ChefBendsAtTheWaist()
        {
            var legs = Model("SM_Chef", "ChefLegs").bounds;
            var torso = Model("SM_Chef", "ChefTorso").bounds;
            Assert.That(legs.min.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(legs.max.y, Is.GreaterThan(0.85f), "The legs reach past the waist, so no gap opens when bending.");
            Assert.That(torso.min.y, Is.LessThan(0f), "The torso's origin is the waist; it starts a little below.");

            var player = Prefab("Assets/_Project/Prefabs/PF_Player.prefab");
            var waist = player.transform.Find("Visuals/Waist");
            Assert.That(waist, Is.Not.Null);
            Assert.That(waist.localPosition.y, Is.EqualTo(0.85f).Within(Tolerance));
            Assert.That(waist.localPosition.y + torso.max.y, Is.EqualTo(1.7f).Within(0.01f), "The chef is as tall as before.");
            Assert.That(player.GetComponent<StorageUseView>(), Is.Not.Null);
        }
    }
}
