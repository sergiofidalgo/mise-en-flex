using Game.Gameplay.Stations;
using Game.Presentation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The tall fridge and shelf: where their four slots are, and their models when those exist.
    /// Slots sit in two tiers of two seen from the front, Y and B on top, X and A below, so the
    /// stock reads from the furniture itself. A fridge gets sliding glass panes, cold mist and
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

        // Where each sliding pane's bottom centre sits, shut: the left on the back track, the right in
        // front. Must match the FRIDGE_PANE_* and FRIDGE_*_TRACK values in blender/build_pilot_models.py.
        public static readonly Vector3 LeftPaneShut = new Vector3(-0.22f, 0.15f, -0.458f);
        public static readonly Vector3 RightPaneShut = new Vector3(0.22f, 0.15f, -0.482f);
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

            // Two sliding panes, each a frame around glass, on tracks across the front.
            var left = BuildPane(station, "PaneLeft", "Left", LeftPaneShut, bodyMaterial);
            var right = BuildPane(station, "PaneRight", "Right", RightPaneShut, bodyMaterial);

            var mist = GreyboxKitchenware.CreateParticles(station.transform, "ColdMist", MistPosition.y, MistRadius,
                MistLifetime, MistSpeed, minSize: 0.12f, maxSize: 0.22f, growth: 2.2f, maxParticles: 40);
            mist.transform.localPosition = MistPosition;
            mist.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.3f, -1f)); // out of the door
            var main = mist.main;
            main.startColor = MistColor;
            main.gravityModifier = MistGravity; // cold air sinks

            var view = new SerializedObject(station.gameObject.AddComponent<FridgeDoorView>());
            view.FindProperty("_leftPane").objectReferenceValue = left;
            view.FindProperty("_rightPane").objectReferenceValue = right;
            view.FindProperty("_mist").objectReferenceValue = mist;
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        // A pane slides as a whole; its frame lights up with the body when the fridge is targeted.
        private static Transform BuildPane(StorageStation station, string name, string side, Vector3 shut, Material bodyMaterial)
        {
            var pane = new GameObject(name).transform;
            pane.SetParent(station.transform, false);
            pane.localPosition = shut;
            var frame = CreatePart(pane, "Frame", FridgeModel, $"FridgePane{side}Frame", bodyMaterial, Vector3.zero);
            var glass = CreatePart(pane, "Glass", FridgeModel, $"FridgePane{side}Glass", PilotArt.EnsureGlassMaterial("FridgeGlass", GlassColor), Vector3.zero);
            glass.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var highlight = new SerializedObject(station.GetComponent<InteractableHighlightView>());
            var renderers = highlight.FindProperty("_renderers");
            renderers.arraySize++;
            renderers.GetArrayElementAtIndex(renderers.arraySize - 1).objectReferenceValue = frame.GetComponent<Renderer>();
            highlight.ApplyModifiedPropertiesWithoutUndo();
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
