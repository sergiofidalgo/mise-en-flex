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
        [TestCase("SM_Shelf", "ShelfBody", 1.05f)]
        public void StorageIsTallWithinItsFootprint(string model, string mesh, float height)
        {
            var bounds = Model(model, mesh).bounds;
            Assert.That(bounds.size.x, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(bounds.size.y, Is.EqualTo(height).Within(Tolerance));
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(-0.5f - Tolerance));
            Assert.That(bounds.max.z, Is.LessThanOrEqualTo(0.5f + Tolerance));
        }

        [TestCase("FrontDoor", "Left")]
        [TestCase("FrontDoor", "Right")]
        [TestCase("BackDoor", "Left")]
        [TestCase("BackDoor", "Right")]
        public void FridgePaneIsGlassInAFrameAndStaysInsideTheFridge(string door, string side)
        {
            var bounds = Model("SM_Fridge", $"FridgePane{side}Frame").bounds;
            Assert.That(bounds.size.x, Is.EqualTo(0.48f).Within(Tolerance), "Half the opening, plus an overlap.");
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(Tolerance), "Its origin is its bottom centre.");
            Assert.That(bounds.center.x, Is.EqualTo(0f).Within(Tolerance));

            var fridge = Prefab(FridgePath).transform;
            var pane = fridge.Find($"{door}/Pane{side}");
            var at = fridge.InverseTransformPoint(pane.position);
            Assert.That(Mathf.Abs(at.x) + bounds.extents.x, Is.LessThanOrEqualTo(0.5f + Tolerance), "Shut, it's inside the footprint.");
            Assert.That(Mathf.Abs(at.z) + bounds.extents.z, Is.LessThanOrEqualTo(0.5f + Tolerance));

            var glass = pane.Find("Glass").GetComponent<Renderer>().sharedMaterial;
            Assert.That(glass.renderQueue, Is.GreaterThanOrEqualTo(3000), "The glass is see-through.");
            Assert.That(glass.color.a, Is.LessThan(0.5f));
        }

        [Test]
        public void TheBackDoorIsTheFrontTurnedRound()
        {
            var fridge = Prefab(FridgePath).transform;
            var front = fridge.Find("FrontDoor");
            var back = fridge.Find("BackDoor");
            Assert.That(fridge.InverseTransformPoint(front.Find("PaneRight").position).z, Is.LessThan(-0.4f), "The front door is at the front.");
            Assert.That(fridge.InverseTransformPoint(back.Find("PaneRight").position).z, Is.GreaterThan(0.4f), "The back door is at the back.");
            Assert.That(Quaternion.Angle(front.localRotation, back.localRotation), Is.EqualTo(180f).Within(0.1f));
        }

        [Test]
        public void TheFridgeHasAGlassTop()
        {
            var bounds = Model("SM_Fridge", "FridgeGlass").bounds;
            Assert.That(bounds.size.x, Is.GreaterThan(0.9f), "From side to side.");
            Assert.That(bounds.max.y, Is.GreaterThan(1.7f), "Up to the top.");
            var glass = Prefab(FridgePath).transform.Find("Glass").GetComponent<Renderer>().sharedMaterial;
            Assert.That(glass.renderQueue, Is.GreaterThanOrEqualTo(3000), "See-through.");
        }

        // Each side is glass (or a door) facing open floor, and solid against a wall or furniture
        // (decision 2026-11-08).
        private static (GameObject Fridge, FridgeShellView Shell, GameObject[] Scenery) FridgeWith(params Vector3[] blocks)
        {
            var fridge = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(FridgePath));
            fridge.transform.position = new Vector3(100f, 0f, 100f); // clear of anything open in the editor
            var scenery = new System.Collections.Generic.List<GameObject> { fridge };
            foreach (var at in blocks)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.transform.position = fridge.transform.position + at + Vector3.up;
                block.transform.localScale = new Vector3(1f, 2f, 1f);
                scenery.Add(block);
            }
            Physics.SyncTransforms();
            var shell = fridge.GetComponent<FridgeShellView>();
            shell.Refresh();
            return (fridge, shell, scenery.ToArray());
        }

        private static void Clear(GameObject[] scenery)
        {
            foreach (var thing in scenery)
                Object.DestroyImmediate(thing);
        }

        [Test]
        public void InOpenFloorEverySideIsSeeThrough()
        {
            var (fridge, shell, scenery) = FridgeWith();
            try
            {
                foreach (FridgeShellView.Facing facing in System.Enum.GetValues(typeof(FridgeShellView.Facing)))
                    Assert.That(shell.IsOpen(facing), Is.True, facing.ToString());
                Assert.That(fridge.transform.Find("LeftSide/Glass").gameObject.activeSelf, Is.True);
                Assert.That(fridge.transform.Find("LeftSide/Solid").gameObject.activeSelf, Is.False);
                Assert.That(fridge.transform.Find("BackDoor/PaneLeft").gameObject.activeSelf, Is.True);
                Assert.That(fridge.transform.Find("BackDoor/Solid").gameObject.activeSelf, Is.False);
            }
            finally
            {
                Clear(scenery);
            }
        }

        [Test]
        public void SidesAgainstSomethingAreSolid()
        {
            // A wall-like block flush behind it, and a counter flush on its left.
            var (fridge, shell, scenery) = FridgeWith(new Vector3(0f, 0f, 1f), new Vector3(-1f, 0f, 0f));
            try
            {
                Assert.That(shell.IsOpen(FridgeShellView.Facing.Back), Is.False);
                Assert.That(shell.IsOpen(FridgeShellView.Facing.Left), Is.False);
                Assert.That(shell.IsOpen(FridgeShellView.Facing.Front), Is.True);
                Assert.That(shell.IsOpen(FridgeShellView.Facing.Right), Is.True);
                Assert.That(fridge.transform.Find("BackDoor/Solid").gameObject.activeSelf, Is.True);
                Assert.That(fridge.transform.Find("BackDoor/PaneLeft").gameObject.activeSelf, Is.False);
                Assert.That(fridge.transform.Find("LeftSide/Solid").gameObject.activeSelf, Is.True);
                Assert.That(fridge.transform.Find("RightSide/Glass").gameObject.activeSelf, Is.True);

                // Facing the solid back, a chef opens the front door instead.
                var door = fridge.GetComponent<FridgeDoorView>();
                Assert.That(door.OpensFor(fridge.transform.TransformPoint(new Vector3(0.6f, 0f, 0.2f))).Back, Is.False);
            }
            finally
            {
                Clear(scenery);
            }
        }

        [Test]
        public void ItFollowsTheFridgeTurning()
        {
            // A block on its right; turned a quarter to face west, that block is behind it instead.
            var (fridge, shell, scenery) = FridgeWith(new Vector3(1f, 0f, 0f));
            try
            {
                Assert.That(shell.IsOpen(FridgeShellView.Facing.Right), Is.False);
                fridge.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                Physics.SyncTransforms();
                shell.Refresh();
                Assert.That(shell.IsOpen(FridgeShellView.Facing.Back), Is.False);
                Assert.That(shell.IsOpen(FridgeShellView.Facing.Right), Is.True);
            }
            finally
            {
                Clear(scenery);
            }
        }

        [Test]
        public void ThePanesRunOnTwoTracksSoOneCanPassTheOther()
        {
            var door = Prefab(FridgePath).transform.Find("FrontDoor");
            var left = door.Find("PaneLeft");
            var right = door.Find("PaneRight");
            var thickness = Model("SM_Fridge", "FridgePaneLeftFrame").bounds.size.z;
            Assert.That(Mathf.Abs(left.localPosition.z - right.localPosition.z), Is.GreaterThan(thickness), "Apart by more than a pane's thickness.");
            Assert.That(right.localPosition.z, Is.LessThan(left.localPosition.z), "The right pane is outside, towards whoever faces the door.");
        }

        [Test]
        public void FridgeIsWiredToOpenAndShelfIsNot()
        {
            Assert.That(Prefab(FridgePath).GetComponent<FridgeDoorView>(), Is.Not.Null);
            Assert.That(Prefab(ShelfPath).GetComponent<FridgeDoorView>(), Is.Null);
        }

        // The door on the opener's side opens the half in front of them, by sliding one pane over
        // the other, and never leaves the fridge's width (decision 2026-11-08).
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void OpeningSlidesOnePaneOverTheOtherAndStaysInsideTheFridge(bool back, bool opensRight)
        {
            var fridge = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(FridgePath));
            try
            {
                var view = fridge.GetComponent<FridgeDoorView>();
                var door = fridge.transform.Find(back ? "BackDoor" : "FrontDoor");
                var other = fridge.transform.Find(back ? "FrontDoor" : "BackDoor");
                var left = door.Find("PaneLeft");
                var right = door.Find("PaneRight");
                var leftShut = left.localPosition;
                var rightShut = right.localPosition;
                var otherShut = other.Find("PaneRight").localPosition;
                var halfWidth = Model("SM_Fridge", "FridgePaneLeftFrame").bounds.extents.x;

                view.Show(1f, back, opensRight);

                var moved = opensRight ? right : left;
                var stayed = opensRight ? left : right;
                Assert.That(stayed.localPosition, Is.EqualTo(opensRight ? leftShut : rightShut), "The other pane stays put.");
                Assert.That(moved.localPosition.x, Is.EqualTo(stayed.localPosition.x).Within(0.05f), "It slides over the other.");
                Assert.That(Mathf.Abs(moved.localPosition.x) + halfWidth, Is.LessThanOrEqualTo(0.5f + Tolerance), "Open, it's still inside the fridge.");
                Assert.That(moved.localPosition.z, Is.EqualTo((opensRight ? rightShut : leftShut).z), "It stays on its track.");
                Assert.That(other.Find("PaneRight").localPosition, Is.EqualTo(otherShut), "The other door stays shut.");

                view.Show(0f, back, opensRight);
                Assert.That(left.localPosition, Is.EqualTo(leftShut));
                Assert.That(right.localPosition, Is.EqualTo(rightShut));
            }
            finally
            {
                Object.DestroyImmediate(fridge);
            }
        }

        [Test]
        public void TheDoorAndHalfThatOpenAreTheOnesInFrontOfTheChef()
        {
            var fridge = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(FridgePath));
            try
            {
                fridge.transform.SetPositionAndRotation(new Vector3(100f, 0f, 100f), Quaternion.Euler(0f, 90f, 0f));
                var door = fridge.GetComponent<FridgeDoorView>();
                Vector3 At(float x, float z) => fridge.transform.TransformPoint(new Vector3(x, 0f, z));
                Assert.That(door.OpensFor(At(0.3f, -0.9f)), Is.EqualTo((false, true)), "In front, on its right.");
                Assert.That(door.OpensFor(At(-0.3f, -0.9f)), Is.EqualTo((false, false)), "In front, on its left.");
                Assert.That(door.OpensFor(At(-0.3f, 0.9f)), Is.EqualTo((true, true)), "Behind: the fridge's left is on their right.");
                Assert.That(door.OpensFor(At(0.3f, 0.9f)), Is.EqualTo((true, false)));
            }
            finally
            {
                Object.DestroyImmediate(fridge);
            }
        }

        // Stock shows from any side, however the piece is turned (decision 2026-11-08).
        [TestCase(FridgePath)]
        [TestCase(ShelfPath)]
        public void StockSitsInTheMiddleSoItShowsFromAnySide(string path)
        {
            var storage = Prefab(path);
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
