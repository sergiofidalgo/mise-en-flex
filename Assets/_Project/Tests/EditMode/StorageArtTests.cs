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

        [TestCase("SM_Fridge", "FridgeBody", 0.9f)]
        [TestCase("SM_Shelf", "ShelfBody", 1.05f)]
        public void StorageStandsWithinItsFootprint(string model, string mesh, float height)
        {
            var bounds = Model(model, mesh).bounds;
            Assert.That(bounds.size.x, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(bounds.size.y, Is.EqualTo(height).Within(Tolerance));
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(-0.5f - Tolerance));
            Assert.That(bounds.max.z, Is.LessThanOrEqualTo(0.5f + Tolerance));
        }

        [Test]
        public void FridgeIsWiredToOpenAndShelfIsNot()
        {
            Assert.That(Prefab(FridgePath).GetComponent<FridgeDoorView>(), Is.Not.Null);
            Assert.That(Prefab(ShelfPath).GetComponent<FridgeDoorView>(), Is.Null);
        }

        // The fridge is a chest at counter height with a glass lid (decision 2026-11-08).
        [Test]
        public void TheFridgeIsAChestWithAGlassLidHingedAtTheBack()
        {
            var fridge = Prefab(FridgePath).transform;
            var lid = fridge.Find("Lid");
            Assert.That(lid.localPosition.y, Is.EqualTo(0.9f).Within(Tolerance), "At counter height.");
            Assert.That(lid.localPosition.z, Is.GreaterThan(0.4f), "Hinged along the back.");
            Assert.That(lid.localRotation, Is.EqualTo(Quaternion.identity), "Built shut.");
            var glass = lid.Find("Glass").GetComponent<Renderer>().sharedMaterial;
            Assert.That(glass.renderQueue, Is.GreaterThanOrEqualTo(3000), "The lid is see-through.");
            Assert.That(glass.color.a, Is.LessThan(0.5f));
        }

        [Test]
        public void TheOpenLidRisesAndNeverLeavesTheFootprint()
        {
            var fridge = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(FridgePath));
            try
            {
                var view = fridge.GetComponent<FridgeDoorView>();
                var frame = fridge.transform.Find("Lid/Frame").GetComponent<MeshFilter>().sharedMesh.bounds;
                foreach (var amount in new[] { 0.25f, 0.5f, 0.7f, 0.85f, 1f })
                {
                    view.Show(amount);
                    var lid = fridge.transform.Find("Lid/Frame");
                    for (var corner = 0; corner < 8; corner++)
                    {
                        var local = frame.center + Vector3.Scale(frame.extents, new Vector3(
                            (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                        var inFridge = fridge.transform.InverseTransformPoint(lid.TransformPoint(local));
                        Assert.That(Mathf.Abs(inFridge.x), Is.LessThanOrEqualTo(0.5f + Tolerance), $"at {amount}");
                        Assert.That(Mathf.Abs(inFridge.z), Is.LessThanOrEqualTo(0.5f + Tolerance), $"at {amount}: past the back or front");
                        Assert.That(inFridge.y, Is.GreaterThanOrEqualTo(0.9f - 0.05f), $"at {amount}: it rises");
                    }
                }
                Assert.That(fridge.transform.Find("Lid").localEulerAngles.x, Is.GreaterThan(60f), "Open, it's well up.");
            }
            finally
            {
                Object.DestroyImmediate(fridge);
            }
        }

        [Test]
        public void TheChestsFourSlotsSitOnOneLevelLikeTheFaceButtons()
        {
            var fridge = Prefab(FridgePath);
            var y = Slot(fridge, FaceButton.North);
            var b = Slot(fridge, FaceButton.East);
            var x = Slot(fridge, FaceButton.West);
            var a = Slot(fridge, FaceButton.South);

            Assert.That(new[] { b.y, x.y, a.y }, Is.All.EqualTo(y.y).Within(Tolerance), "One level.");
            Assert.That(y.z, Is.GreaterThan(x.z), "Y and B at the back, X and A at the front.");
            Assert.That(b.z, Is.GreaterThan(a.z));
            Assert.That(y.x, Is.LessThan(b.x), "Y is on the left, B on the right.");
            Assert.That(x.x, Is.LessThan(a.x), "X is on the left, A on the right.");
            Assert.That(y.y, Is.LessThan(0.9f), "Inside, under the lid.");
        }

        [Test]
        public void TheShelfsStockSitsInTheMiddleSoItShowsFromAnySide()
        {
            var storage = Prefab(ShelfPath);
            foreach (FaceButton button in System.Enum.GetValues(typeof(FaceButton)))
                Assert.That(Slot(storage, button).z, Is.EqualTo(0f).Within(Tolerance), button.ToString());
        }

        [Test]
        public void TheShelfIsAnOpenRackWithShallowBoardsAndNoTopBoard()
        {
            var bounds = Model("SM_Shelf", "ShelfBody").bounds;
            Assert.That(bounds.size.z, Is.LessThanOrEqualTo(0.45f), "Shallow boards hide little of the tier below.");
            Assert.That(bounds.center.z, Is.EqualTo(0f).Within(Tolerance), "Centred, the same from front and back.");
        }

        [Test]
        public void TheShelfsSlotsSitInTwoTiersWithYAndBOnTop()
        {
            var storage = Prefab(ShelfPath);
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
