using Game.Gameplay.Stations;
using Game.Presentation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The fridge and shelf: where their four slots are, and their models when those exist. The
    /// slots copy the face buttons' diamond so the stock reads from the furniture itself: on the
    /// shelf, two tiers of two seen from the front, Y and B on top, X and A below; in the fridge, a
    /// chest at counter height, one level seen from above, Y and B at the back, X and A at the front,
    /// under a glass lid. A shelf gets sacks on its bottom board. Without the models it stays greybox.
    /// </summary>
    public static class StorageArt
    {
        public const string FridgeModel = "SM_Fridge";
        public const string ShelfModel = "SM_Shelf";

        // Heights must match FRIDGE_HEIGHT, SHELF_HEIGHT, TIER_TOPS and FRIDGE_FLOOR in blender/build_pilot_models.py.
        public const float FridgeHeight = 0.9f; // a chest at counter height (decision 2026-11-08)
        public const float ShelfHeight = 1.05f; // the upper tier's board is its top
        private const float LowerTier = 0.45f;
        private const float UpperTier = 1.05f;
        // The shelf's two slots per tier, either side of its middle.
        private const float SlotX = 0.22f;
        // The shelf's stock sits in the middle, so it shows the same from any side: the rack is open.
        private const float ShelfSlotZ = 0f;
        // The chest's four slots sit on one level, its floor, near the top so they show through the
        // lid from above on any side (decision 2026-11-08): Y and B at the back, X and A at the front.
        public const float ChestFloor = 0.7f;
        // The chest's baskets fit a steak and are gathered towards the middle, so stock shows from
        // every side (decision 2026-11-08). Must match FridgeBasket's location in blender/build_pilot_models.py.
        private const float ChestSlotX = 0.18f;
        private const float ChestRowZ = 0.15f;

        /// <summary>Height of a modelled basket, which food sits on.</summary>
        public const float BasketHeight = 0.03f;

        // The glass lid's hinge, along the back of the top. Must match FRIDGE_LID_HINGE in blender/build_pilot_models.py.
        public static readonly Vector3 LidHinge = new Vector3(0f, 0.9f, 0.44f);
        private static readonly Color LinerColor = new Color(0.9f, 0.94f, 0.97f);
        private static readonly Color Enamel = new Color(0.95f, 0.94f, 0.9f);
        private static readonly Color ShelfWood = new Color(0.6f, 0.4f, 0.24f);
        private static readonly Color ShelfGrain = new Color(0.52f, 0.34f, 0.2f);
        private static readonly Color FridgeBasket = new Color(0.82f, 0.86f, 0.9f);
        private static readonly Color Wicker = new Color(0.78f, 0.62f, 0.4f);

        /// <summary>A basket's material: pale plastic in the fridge, wicker on the shelf.</summary>
        public static Material BasketMaterial(bool refrigerated) => refrigerated
            ? StyleTest.EnsureToonMaterial("FridgeBasket", FridgeBasket)
            : StyleTest.EnsureToonMaterial("ShelfBasket", Wicker);
        private static readonly Color GoodsColor = new Color(0.66f, 0.55f, 0.38f);
        private static readonly Color GlassColor = new Color(0.8f, 0.92f, 1f, 0.18f);

        // Cold air: a soft, slow puff that spills over the front edge as the lid opens, and sinks.
        // A white breath with a cool blue tint, like cold air, and so it shows against the white
        // fridge (the user's reference, 2026-11-08).
        private static readonly Color MistColor = new Color(0.8f, 0.9f, 1f, 0.85f);
        private static readonly Vector3 MistPosition = new Vector3(0f, 0.92f, -0.3f);
        private const float MistLifetime = 1f;
        private const float MistSpeed = 0.3f;
        private const float MistRadius = 0.3f;
        private const float MistGravity = 0.04f;

        public static float Height(bool refrigerated) => refrigerated ? FridgeHeight : ShelfHeight;

        public static string ModelFor(bool refrigerated) => refrigerated ? FridgeModel : ShelfModel;

        public static string BasketMesh(bool refrigerated) => refrigerated ? "FridgeBasket" : "ShelfBasket";

        /// <summary>Where a slot's basket sits, in FaceButton order: A, B, X, Y.</summary>
        public static Vector3 SlotPosition(bool refrigerated, int slot)
        {
            if (refrigerated)
            {
                switch ((FaceButton)slot)
                {
                    case FaceButton.South: return new Vector3(ChestSlotX, ChestFloor, -ChestRowZ);  // A: front right
                    case FaceButton.East: return new Vector3(ChestSlotX, ChestFloor, ChestRowZ);    // B: back right
                    case FaceButton.West: return new Vector3(-ChestSlotX, ChestFloor, -ChestRowZ);  // X: front left
                    default: return new Vector3(-ChestSlotX, ChestFloor, ChestRowZ);                // Y: back left
                }
            }

            var z = ShelfSlotZ;
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
            // Toon shaded like the rest of the kitchen (decision 2026-11-08, the art overhaul).
            body.GetComponent<Renderer>().sharedMaterial = refrigerated
                ? StyleTest.EnsureToonMaterial("FridgeEnamel", Enamel)
                : StyleTest.EnsureToonMaterial("ShelfWood", ShelfWood, ShelfGrain, grainScale: 14f);

            if (refrigerated)
                DressFridge(station, body.GetComponent<Renderer>().sharedMaterial);
            else
                CreatePart(station.transform, "Goods", ShelfModel, "ShelfGoods",
                    StyleTest.EnsureToonMaterial("ShelfGoods", GoodsColor), Vector3.zero);
        }

        private static void DressFridge(StorageStation station, Material bodyMaterial)
        {
            CreatePart(station.transform, "Liner", FridgeModel, "FridgeLiner",
                StyleTest.EnsureToonMaterial("FridgeLiner", LinerColor), Vector3.zero);

            // The glass lid turns about its hinge along the back; its frame and glass hang from it.
            var lid = new GameObject("Lid").transform;
            lid.SetParent(station.transform, false);
            lid.localPosition = LidHinge;
            var frame = CreatePart(lid, "Frame", FridgeModel, "FridgeLidFrame", bodyMaterial, Vector3.zero);
            var glass = CreatePart(lid, "Glass", FridgeModel, "FridgeLidGlass", PilotArt.EnsureGlassMaterial("FridgeGlass", GlassColor), Vector3.zero);
            glass.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // The frame lights up with the body when the fridge is targeted.
            var highlight = new SerializedObject(station.GetComponent<InteractableHighlightView>());
            var renderers = highlight.FindProperty("_renderers");
            renderers.arraySize++;
            renderers.GetArrayElementAtIndex(renderers.arraySize - 1).objectReferenceValue = frame.GetComponent<Renderer>();
            highlight.ApplyModifiedPropertiesWithoutUndo();

            var mist = GreyboxKitchenware.CreateParticles(station.transform, "ColdMist", MistPosition.y, MistRadius,
                MistLifetime, MistSpeed, minSize: 0.14f, maxSize: 0.28f, growth: 2.2f, maxParticles: 40);
            mist.transform.localPosition = MistPosition;
            mist.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.4f, -1f)); // over the front edge
            var main = mist.main;
            main.startColor = MistColor;
            mist.GetComponent<ParticleSystemRenderer>().sharedMaterial = PilotArt.EnsureMistMaterial();
            main.gravityModifier = MistGravity; // cold air sinks

            var view = new SerializedObject(station.gameObject.AddComponent<FridgeDoorView>());
            view.FindProperty("_lid").objectReferenceValue = lid;
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
