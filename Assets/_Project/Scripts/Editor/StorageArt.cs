using Game.Gameplay.Stations;
using Game.Presentation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The tall fridge and shelf: where their four slots are, and their models when those exist.
    /// Slots sit in two tiers of two seen from the front, Y and B on top, X and A below, so the
    /// stock reads from the furniture itself. A fridge gets a glass door that opens, cold mist and
    /// a pale lining; a shelf gets sacks on its bottom board. Without the models it stays greybox.
    /// </summary>
    public static class StorageArt
    {
        public const string FridgeModel = "SM_Fridge";
        public const string ShelfModel = "SM_Shelf";

        // Heights must match FRIDGE_HEIGHT, SHELF_HEIGHT and TIER_TOPS in blender/build_pilot_models.py.
        public const float FridgeHeight = 1.8f;
        public const float ShelfHeight = 1.5f;
        private const float LowerTier = 0.45f;
        private const float UpperTier = 1.05f;
        private const float SlotX = 0.22f;
        // Near the front, so both tiers show through the door under the camera's 55° view: further
        // back, the fridge's top hides the upper tier.
        private const float FridgeSlotZ = -0.28f;
        private const float ShelfSlotZ = -0.15f;

        /// <summary>Height of a modelled basket, which food sits on.</summary>
        public const float BasketHeight = 0.03f;

        // Where the door's hinge is; must match FRIDGE_HINGE in blender/build_pilot_models.py.
        private static readonly Vector3 DoorHinge = new Vector3(-0.48f, 0.15f, -0.45f);
        private static readonly Color LinerColor = new Color(0.9f, 0.94f, 0.97f);
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
            CreatePart(station.transform, "Liner", FridgeModel, "FridgeLiner",
                GreyboxAssets.EnsureMaterial("FridgeLiner", LinerColor), Vector3.zero);

            // The door turns about its hinge; its frame and glass hang from it.
            var door = new GameObject("Door").transform;
            door.SetParent(station.transform, false);
            door.localPosition = DoorHinge;
            var frame = CreatePart(door, "Frame", FridgeModel, "FridgeDoorFrame", bodyMaterial, Vector3.zero);
            var glass = CreatePart(door, "Glass", FridgeModel, "FridgeDoorGlass", PilotArt.EnsureGlassMaterial("FridgeGlass", GlassColor), Vector3.zero);
            glass.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // The frame lights up with the body when the fridge is targeted.
            var highlight = new SerializedObject(station.GetComponent<InteractableHighlightView>());
            var renderers = highlight.FindProperty("_renderers");
            renderers.arraySize++;
            renderers.GetArrayElementAtIndex(renderers.arraySize - 1).objectReferenceValue = frame.GetComponent<Renderer>();
            highlight.ApplyModifiedPropertiesWithoutUndo();

            var mist = GreyboxKitchenware.CreateParticles(station.transform, "ColdMist", MistPosition.y, MistRadius,
                MistLifetime, MistSpeed, minSize: 0.12f, maxSize: 0.22f, growth: 2.2f, maxParticles: 40);
            mist.transform.localPosition = MistPosition;
            mist.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.3f, -1f)); // out of the door
            var main = mist.main;
            main.startColor = MistColor;
            main.gravityModifier = MistGravity; // cold air sinks

            var view = new SerializedObject(station.gameObject.AddComponent<FridgeDoorView>());
            view.FindProperty("_door").objectReferenceValue = door;
            view.FindProperty("_mist").objectReferenceValue = mist;
            view.ApplyModifiedPropertiesWithoutUndo();
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
