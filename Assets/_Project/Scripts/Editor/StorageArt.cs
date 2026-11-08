using Game.Gameplay.Stations;
using Game.Presentation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The tall fridge and shelf: where their four slots are, and their models when those exist.
    /// Slots sit in two tiers of two seen from the front, Y and B on top, X and A below, so the
    /// stock reads from the furniture itself. A fridge is glass on top and on every side that
    /// faces open floor (sliding doors front and back), solid against walls, with glass shelves and cold mist; a shelf gets sacks on its bottom board. Without the models it stays greybox.
    /// </summary>
    public static class StorageArt
    {
        public const string FridgeModel = "SM_Fridge";
        public const string ShelfModel = "SM_Shelf";

        // Heights must match FRIDGE_HEIGHT, SHELF_HEIGHT and TIER_TOPS in blender/build_pilot_models.py.
        public const float FridgeHeight = 1.8f;
        public const float ShelfHeight = 1.05f; // the upper tier's board is its top
        private const float LowerTier = 0.45f;
        private const float UpperTier = 1.05f;
        private const float SlotX = 0.22f;
        // In the middle, so the stock shows the same from any side (decision 2026-11-08): the fridge
        // is glass all round and on top, and the shelf is open, with no top board.
        private const float FridgeSlotZ = 0f;
        private const float ShelfSlotZ = 0f;

        /// <summary>Height of a modelled basket, which food sits on.</summary>
        public const float BasketHeight = 0.03f;

        // Where each sliding pane's bottom centre sits, shut: the left on the back track, the right in
        // front. Must match the FRIDGE_PANE_* and FRIDGE_*_TRACK values in blender/build_pilot_models.py.
        public static readonly Vector3 LeftPaneShut = new Vector3(-0.22f, 0.15f, -0.458f);
        public static readonly Vector3 RightPaneShut = new Vector3(0.22f, 0.15f, -0.482f);
        private static readonly Color GoodsColor = new Color(0.66f, 0.55f, 0.38f);
        private static readonly Color GlassColor = new Color(0.8f, 0.92f, 1f, 0.18f);

        // Cold air: a soft, slow puff that rolls out of the door and sinks.
        private static readonly Color MistColor = new Color(0.86f, 0.94f, 1f, 0.55f);
        private static readonly Vector3 MistPosition = new Vector3(0f, 0.9f, -0.5f);
        private const float MistLifetime = 1.2f;
        private const float MistSpeed = 0.3f;
        private const float MistRadius = 0.3f;
        private const float MistGravity = 0.04f;

        public static float Height(bool refrigerated) => refrigerated ? FridgeHeight : ShelfHeight;

        public static string ModelFor(bool refrigerated) => refrigerated ? FridgeModel : ShelfModel;

        public static string BasketMesh(bool refrigerated) => refrigerated ? "FridgeBasket" : "ShelfBasket";

        /// <summary>Where a slot's basket sits, in FaceButton order: A, B, X, Y.</summary>
        public static Vector3 SlotPosition(bool refrigerated, int slot)
        {
            var z = refrigerated ? FridgeSlotZ : ShelfSlotZ;
            switch ((FaceButton)slot)
            {
                case FaceButton.South: return new Vector3(SlotX, LowerTier, z);  // A: bottom right
                case FaceButton.East: return new Vector3(SlotX, UpperTier, z);   // B: top right
                case FaceButton.West: return new Vector3(-SlotX, LowerTier, z);  // X: bottom left
                default: return new Vector3(-SlotX, UpperTier, z);               // Y: top left
            }
        }

        /// <summary>
        /// A collider-less picture of an ingredient for a slot: its model when it has one (base at
        /// the origin), or else its greybox shape. <paramref name="baseOffset"/> lifts it to sit on
        /// a surface.
        /// </summary>
        public static GameObject CreateSample(GreyboxIngredients.IngredientType type, string name, Transform parent,
            float scale, out float baseOffset)
        {
            var model = $"SM_{type.Id}";
            if (type.Layered && PilotArt.FindMesh(model, model) != null)
            {
                var part = CreatePart(parent, name, model, model, PilotArt.EnsureLayeredFoodMaterial(type.Id, type.Color), Vector3.zero);
                part.transform.localScale = Vector3.one * scale;
                baseOffset = 0f;
                return part;
            }

            var shape = GreyboxIngredients.CreateShape(type, name, parent);
            shape.transform.localScale = type.Scale * scale;
            baseOffset = type.CentreHeight * scale;
            return shape;
        }

        public static void Dress(StorageStation station, bool refrigerated)
        {
            var body = station.transform.Find("Body").gameObject;
            if (!PilotArt.TryUseMesh(body, ModelFor(refrigerated), refrigerated ? "FridgeBody" : "ShelfBody"))
                return;

            if (refrigerated)
                DressFridge(station, body.GetComponent<Renderer>().sharedMaterial);
            else
                CreatePart(station.transform, "Goods", ShelfModel, "ShelfGoods",
                    GreyboxAssets.EnsureMaterial("ShelfGoods", GoodsColor), Vector3.zero);
        }

        private static void DressFridge(StorageStation station, Material bodyMaterial)
        {
            // A glass top and shelves, so the stock shows from any angle.
            var top = CreatePart(station.transform, "Glass", FridgeModel, "FridgeGlass", GlassMaterial(), Vector3.zero);
            top.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Each side is open (glass, or a door of two sliding panes) or solid where it stands
            // against something. The back and right are the front and left turned round.
            var front = BuildDoor(station, "FrontDoor", 0f, bodyMaterial);
            var back = BuildDoor(station, "BackDoor", 180f, bodyMaterial);
            var left = BuildSide(station, "LeftSide", 0f, bodyMaterial);
            var right = BuildSide(station, "RightSide", 180f, bodyMaterial);
            var shellView = station.gameObject.AddComponent<FridgeShellView>();
            var shellSo = new SerializedObject(shellView);
            SetSide(shellSo.FindProperty("_front"), front.Root, new[] { front.Left.gameObject, front.Right.gameObject }, front.Solid, Vector3.back);
            SetSide(shellSo.FindProperty("_back"), back.Root, new[] { back.Left.gameObject, back.Right.gameObject }, back.Solid, Vector3.back);
            SetSide(shellSo.FindProperty("_left"), left.Root, new[] { left.Glass }, left.Solid, Vector3.left);
            SetSide(shellSo.FindProperty("_right"), right.Root, new[] { right.Glass }, right.Solid, Vector3.left);
            shellSo.ApplyModifiedPropertiesWithoutUndo();

            var mist = GreyboxKitchenware.CreateParticles(station.transform, "ColdMist", MistPosition.y, MistRadius,
                MistLifetime, MistSpeed, minSize: 0.12f, maxSize: 0.22f, growth: 2.2f, maxParticles: 40);
            mist.transform.localPosition = MistPosition;
            mist.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.3f, -1f)); // out of the door
            var main = mist.main;
            main.startColor = MistColor;
            main.gravityModifier = MistGravity; // cold air sinks

            var view = new SerializedObject(station.gameObject.AddComponent<FridgeDoorView>());
            view.FindProperty("_frontLeft").objectReferenceValue = front.Left;
            view.FindProperty("_frontRight").objectReferenceValue = front.Right;
            view.FindProperty("_backLeft").objectReferenceValue = back.Left;
            view.FindProperty("_backRight").objectReferenceValue = back.Right;
            view.FindProperty("_mist").objectReferenceValue = mist;
            view.FindProperty("_shell").objectReferenceValue = shellView;
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material GlassMaterial() => PilotArt.EnsureGlassMaterial("FridgeGlass", GlassColor);

        // A door's two panes and the solid end that stands in for it against a wall, built as for
        // the front and turned by <paramref name="degrees"/> about the middle.
        private static (Transform Root, Transform Left, Transform Right, GameObject Solid) BuildDoor(StorageStation station, string name, float degrees, Material bodyMaterial)
        {
            var door = new GameObject(name).transform;
            door.SetParent(station.transform, false);
            door.localRotation = Quaternion.Euler(0f, degrees, 0f);
            var solid = CreatePart(door, "Solid", FridgeModel, "FridgeEndPanel", bodyMaterial, Vector3.zero);
            Highlight(station, solid.GetComponent<Renderer>());
            return (door, BuildPane(station, door, "PaneLeft", "Left", LeftPaneShut, bodyMaterial),
                BuildPane(station, door, "PaneRight", "Right", RightPaneShut, bodyMaterial), solid);
        }

        // A side's glass and the solid panel that stands in for it against a wall, built as for the
        // left and turned by <paramref name="degrees"/>.
        private static (Transform Root, GameObject Glass, GameObject Solid) BuildSide(StorageStation station, string name, float degrees, Material bodyMaterial)
        {
            var side = new GameObject(name).transform;
            side.SetParent(station.transform, false);
            side.localRotation = Quaternion.Euler(0f, degrees, 0f);
            var glass = CreatePart(side, "Glass", FridgeModel, "FridgeSideGlass", GlassMaterial(), Vector3.zero);
            glass.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var solid = CreatePart(side, "Solid", FridgeModel, "FridgeSidePanel", bodyMaterial, Vector3.zero);
            Highlight(station, solid.GetComponent<Renderer>());
            return (side, glass, solid);
        }

        private static void SetSide(SerializedProperty side, Transform root, GameObject[] open, GameObject solid, Vector3 outward)
        {
            side.FindPropertyRelative("Root").objectReferenceValue = root;
            var list = side.FindPropertyRelative("Open");
            list.arraySize = open.Length;
            for (var i = 0; i < open.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = open[i];
            side.FindPropertyRelative("Solid").objectReferenceValue = solid;
            side.FindPropertyRelative("Outward").vector3Value = outward;
        }

        // Lights up with the body when the fridge is targeted.
        private static void Highlight(StorageStation station, Renderer renderer)
        {
            var highlight = new SerializedObject(station.GetComponent<InteractableHighlightView>());
            var renderers = highlight.FindProperty("_renderers");
            renderers.arraySize++;
            renderers.GetArrayElementAtIndex(renderers.arraySize - 1).objectReferenceValue = renderer;
            highlight.ApplyModifiedPropertiesWithoutUndo();
        }

        // A pane slides as a whole; its frame lights up with the body when the fridge is targeted.
        private static Transform BuildPane(StorageStation station, Transform door, string name, string side, Vector3 shut, Material bodyMaterial)
        {
            var pane = new GameObject(name).transform;
            pane.SetParent(door, false);
            pane.localPosition = shut;
            var frame = CreatePart(pane, "Frame", FridgeModel, $"FridgePane{side}Frame", bodyMaterial, Vector3.zero);
            var glass = CreatePart(pane, "Glass", FridgeModel, $"FridgePane{side}Glass", GlassMaterial(), Vector3.zero);
            glass.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Highlight(station, frame.GetComponent<Renderer>());
            return pane;
        }

        private static GameObject CreatePart(Transform parent, string name, string model, string mesh, Material material, Vector3 position)
        {
            var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, parent, material, keepCollider: false);
            PilotArt.TryUseMesh(part, model, mesh);
            part.transform.localPosition = position;
            return part;
        }
    }
}
