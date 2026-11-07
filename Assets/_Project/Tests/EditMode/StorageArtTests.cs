using System.Linq;
using Game.Data;
using Game.Gameplay.Stations;
using Game.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The tall fridge and shelf, their slots and the pictures in their menu, and the chef that
    /// bends at them: models fit their stations, and the motions are wired and timed as intended.
    /// </summary>
    public class StorageArtTests
    {
        private const string ModelsFolder = "Assets/_Project/Art/Models";
        private const string FridgePath = "Assets/_Project/Prefabs/Furniture/PF_Fridge.prefab";
        private const string ShelfPath = "Assets/_Project/Prefabs/Furniture/PF_Shelf.prefab";
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

        private static Vector3 Slot(GameObject storage, FaceButton button) =>
            storage.transform.Find($"Bin_{button}").localPosition;

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

        [TestCase("SM_Fridge", "FridgeBody", 1.8f)]
        [TestCase("SM_Shelf", "ShelfBody", 1.5f)]
        public void StorageIsTallWithinItsFootprint(string model, string mesh, float height)
        {
            var bounds = Model(model, mesh).bounds;
            Assert.That(bounds.size.x, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(bounds.size.y, Is.EqualTo(height).Within(Tolerance));
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(-0.5f - Tolerance));
            Assert.That(bounds.max.z, Is.LessThanOrEqualTo(0.5f + Tolerance));
        }

        [Test]
        public void FridgeDoorIsAGlassPanelInAFrameThatOpensTowardsTheCamera()
        {
            var bounds = Model("SM_Fridge", "FridgeDoorFrame").bounds;
            Assert.That(bounds.min.x, Is.EqualTo(0f).Within(Tolerance), "The hinge is at the door's origin, on its left.");
            Assert.That(bounds.max.x, Is.EqualTo(0.96f).Within(Tolerance));
            Assert.That(bounds.max.z, Is.LessThanOrEqualTo(Tolerance), "The door sits in front of its hinge, towards the camera.");

            var fridge = Prefab(FridgePath);
            var door = fridge.transform.Find("Door");
            Assert.That(door.localRotation, Is.EqualTo(Quaternion.identity), "Built shut.");
            Assert.That(door.localPosition.x + bounds.max.x, Is.LessThanOrEqualTo(0.5f + Tolerance), "The shut door stays within the footprint.");
            Assert.That(door.localPosition.z + bounds.min.z, Is.GreaterThanOrEqualTo(-0.5f - Tolerance));

            var glass = door.Find("Glass").GetComponent<Renderer>().sharedMaterial;
            Assert.That(glass.renderQueue, Is.GreaterThanOrEqualTo(3000), "The glass is see-through.");
            Assert.That(glass.color.a, Is.LessThan(0.5f));
        }

        [Test]
        public void FridgeIsWiredToOpenAndShelfIsNot()
        {
            Assert.That(Prefab(FridgePath).GetComponent<FridgeDoorView>(), Is.Not.Null);
            Assert.That(Prefab(ShelfPath).GetComponent<FridgeDoorView>(), Is.Null);
        }

        [Test]
        public void FreeAngleStopsAtTheFirstBlockedStep()
        {
            Assert.That(Swing.FreeAngle(105f, 5f, angle => false), Is.EqualTo(105f));
            Assert.That(Swing.FreeAngle(105f, 5f, angle => angle >= 88f), Is.EqualTo(85f));
            Assert.That(Swing.FreeAngle(105f, 5f, angle => true), Is.EqualTo(0f));
        }

        // A fridge with boxes standing in for walls or furniture on either side of it.
        private static (FridgeDoorView Door, GameObject[] Scenery) FridgeBetween(bool wallLeft, bool wallRight)
        {
            var fridge = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(FridgePath));
            fridge.transform.position = new Vector3(100f, 0f, 100f); // clear of anything open in the editor
            var scenery = new System.Collections.Generic.List<GameObject> { fridge };
            void Wall(float x)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(100f + x, 1f, 100f - 0.5f);
                wall.transform.localScale = new Vector3(0.2f, 2f, 3f);
                scenery.Add(wall);
            }
            if (wallLeft)
                Wall(-0.62f);
            if (wallRight)
                Wall(0.62f);
            Physics.SyncTransforms();
            return (fridge.GetComponent<FridgeDoorView>(), scenery.ToArray());
        }

        [Test]
        public void FridgeDoorOpensFullyWithRoomOnItsHingeSide()
        {
            var (door, scenery) = FridgeBetween(wallLeft: false, wallRight: false);
            try
            {
                door.FindRoomToOpen();
                Assert.That(door.HingedRight, Is.False);
                Assert.That(door.OpenLimit, Is.GreaterThan(100f));
            }
            finally
            {
                foreach (var thing in scenery)
                    Object.DestroyImmediate(thing);
            }
        }

        [Test]
        public void FridgeDoorHingesOnTheOtherSideOfAWall()
        {
            var (door, scenery) = FridgeBetween(wallLeft: true, wallRight: false);
            try
            {
                door.FindRoomToOpen();
                Assert.That(door.HingedRight, Is.True, "The wall on the left would stop it short of a right angle.");
                Assert.That(door.OpenLimit, Is.GreaterThan(100f));
            }
            finally
            {
                foreach (var thing in scenery)
                    Object.DestroyImmediate(thing);
            }
        }

        [Test]
        public void FridgeDoorBoxedInStopsShortOfTheWall()
        {
            var (door, scenery) = FridgeBetween(wallLeft: true, wallRight: true);
            try
            {
                door.FindRoomToOpen();
                Assert.That(door.OpenLimit, Is.LessThan(95f), "It can't swing past the walls beside it.");
                Assert.That(door.OpenLimit, Is.GreaterThan(45f), "It still opens enough to reach in.");
            }
            finally
            {
                foreach (var thing in scenery)
                    Object.DestroyImmediate(thing);
            }
        }

        [TestCase(FridgePath)]
        [TestCase(ShelfPath)]
        public void SlotsSitInTwoTiersWithYAndBOnTop(string path)
        {
            var storage = Prefab(path);
            var y = Slot(storage, FaceButton.North);
            var b = Slot(storage, FaceButton.East);
            var x = Slot(storage, FaceButton.West);
            var a = Slot(storage, FaceButton.South);

            Assert.That(y.y, Is.EqualTo(b.y).Within(Tolerance), "Y and B share the top tier.");
            Assert.That(x.y, Is.EqualTo(a.y).Within(Tolerance), "X and A share the bottom tier.");
            Assert.That(y.y, Is.GreaterThan(x.y));
            Assert.That(y.x, Is.LessThan(b.x), "Y is on the left, B on the right.");
            Assert.That(x.x, Is.LessThan(a.x), "X is on the left, A on the right.");
        }

        [Test]
        public void StoredSteakLooksLikeTheSteakModel()
        {
            var sample = Prefab(FridgePath).transform.Find($"Sample_{FaceButton.North}_Steak");
            Assert.That(sample.GetComponent<MeshFilter>().sharedMesh.name, Is.EqualTo("SM_Steak"));
        }

        [Test]
        public void EveryIngredientHasAPictureOnAClearBackground()
        {
            var icons = AssetDatabase.LoadAssetAtPath<IngredientIcons>("Assets/_Project/Data/UI/IngredientIcons.asset");
            Assert.That(icons, Is.Not.Null);

            var typeIds = AssetDatabase.FindAssets("t:IngredientDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<IngredientDefinition>(AssetDatabase.GUIDToAssetPath(guid)).TypeId)
                .ToArray();
            Assert.That(typeIds, Is.Not.Empty);
            foreach (var typeId in typeIds)
            {
                var icon = icons.Find(typeId);
                Assert.That(icon, Is.Not.Null, $"{typeId} has no picture.");

                var texture = icon.texture;
                Assert.That(texture.GetPixel(1, 1).a, Is.LessThan(0.1f), $"{typeId}'s picture should have a clear background.");
                Assert.That(texture.GetPixel(texture.width / 2, texture.height / 2).a, Is.GreaterThan(0.5f), $"{typeId}'s picture should show the food.");
            }
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
