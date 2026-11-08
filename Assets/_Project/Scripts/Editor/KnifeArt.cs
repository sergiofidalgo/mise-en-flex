using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The chef's knife and its angled knife block (the user's notes and references, 2026-11-08):
    /// a steel blade with a straight spine and a black, riveted handle that read in a chef's hand, and a
    /// block whose sloping face holds the knives standing up in slots, handles out towards the
    /// camera. Without the models, the greybox knife and block stay.
    /// </summary>
    public static class KnifeArt
    {
        private const string KnifeModel = "SM_Knife";
        private const string BlockModel = "SM_KnifeBlock";

        // The block's shape: must match build_knife_block() in blender/build_pilot_models.py.
        private const float BlockFrontHeight = 0.16f;
        private const float BlockBackHeight = 0.34f;
        private const float BlockDepth = 0.28f;
        // Its slots across the face (Blender's x is the game's -x). Must match KNIFE_SLOTS_X.
        private static readonly float[] SlotsX = { 0.15f, 0.05f, -0.05f, -0.15f };
        // The block faces the front, its sloping face towards the camera (the user's note,
        // 2026-11-08). It was turned a quarter to show the handles full length, but read better facing out.
        private static readonly Quaternion BlockTurn = Quaternion.identity;
        // How much of a knife's blade stands out of its slot (m, at the knife's own size; the knife
        // is 0.26 m of blade). More than at first, with the knives a little bigger in the block, so a
        // knife's handle stands out clearly and an empty slot reads as empty (the user's note,
        // 2026-11-08). The tip still stays inside the block.
        private const float BladeShowing = 0.1f;
        private const float KnifeInBlock = 1.5f;
        private static readonly Color Brass = new Color(0.85f, 0.66f, 0.3f);

        private static readonly Color Steel = new Color(0.86f, 0.89f, 0.93f);
        // A black handle with silver rivets and a steel bolster, like the user's reference.
        private static readonly Color HandleBlack = new Color(0.11f, 0.11f, 0.12f);
        private static readonly Color Silver = new Color(0.86f, 0.88f, 0.91f);
        private static readonly Color BlockWood = new Color(0.82f, 0.55f, 0.3f);
        private static readonly Color SlotDark = new Color(0.25f, 0.15f, 0.09f);

        /// <summary>Builds the knife's model under its item root. False without the model.</summary>
        public static bool Dress(Transform knife)
        {
            if (PilotArt.FindMesh(KnifeModel, "KnifeBlade") == null)
                return false;
            Part(knife, "Blade", KnifeModel, "KnifeBlade", StyleTest.EnsureToonMaterial("KnifeBlade", Steel));
            Part(knife, "Bolster", KnifeModel, "KnifeBolster", StyleTest.EnsureToonMaterial("KnifeBlade", Steel));
            Part(knife, "Handle", KnifeModel, "KnifeHandle", StyleTest.EnsureToonMaterial("KnifeHandle", HandleBlack));
            Part(knife, "Rivets", KnifeModel, "KnifeRivets", StyleTest.EnsureToonMaterial("KnifeRivets", Silver));
            return true;
        }

        /// <summary>
        /// Makes the knife rack's block the angled block, standing at <paramref name="position"/> (its
        /// base). Returns the slots' renderer, for the station's highlight, or null without the model.
        /// </summary>
        public static Renderer DressBlock(GameObject block, Vector3 position)
        {
            if (!PilotArt.TryUseMesh(block, BlockModel, "KnifeBlockBody"))
                return null;
            block.transform.localPosition = position;
            block.transform.localRotation = BlockTurn;
            block.GetComponent<Renderer>().sharedMaterial = StyleTest.EnsureToonMaterial("KnifeBlock", BlockWood);
            var slots = Part(block.transform, "Slots", BlockModel, "KnifeBlockSlots", StyleTest.EnsureToonMaterial("KnifeBlockSlots", SlotDark));
            Part(block.transform, "SlotRims", BlockModel, "KnifeBlockSlotRims", StyleTest.EnsureToonMaterial("KnifeBlockSlotRims", Brass));
            return slots == null ? null : slots.GetComponent<Renderer>();
        }

        /// <summary>
        /// Where knives sit in the block: one place per slot on its sloping face, turned so a
        /// knife's blade goes into the block and its handle stands up and out, its flat along the slope.
        /// </summary>
        public static Transform[] SlotPlaces(Transform rack, Vector3 blockPosition)
        {
            // The face's outward normal: up, and towards the camera (the game's -z) as the block
            // rises from its low front to its tall back.
            var normal = BlockTurn * new Vector3(0f, BlockDepth, -(BlockBackHeight - BlockFrontHeight)).normalized;
            var intoBlock = -normal;
            var places = new Transform[SlotsX.Length];
            for (var i = 0; i < SlotsX.Length; i++)
            {
                var place = new GameObject($"Place_{i + 1}").transform;
                place.SetParent(rack, false);
                // A little of the blade shows above the slot, as in a real block, and the tip
                // stays inside rather than poking out of the far side.
                place.localPosition = blockPosition + BlockTurn * new Vector3(SlotsX[i], (BlockFrontHeight + BlockBackHeight) / 2f, 0f)
                    + normal * (BladeShowing * KnifeInBlock);
                place.localRotation = Quaternion.LookRotation(intoBlock, BlockTurn * Vector3.right);
                place.localScale = Vector3.one * KnifeInBlock;
                places[i] = place;
            }
            return places;
        }

        private static GameObject Part(Transform parent, string name, string model, string mesh, Material material)
        {
            var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, parent, material, keepCollider: false);
            if (PilotArt.TryUseMesh(part, model, mesh))
                return part;
            Object.DestroyImmediate(part);
            return null;
        }
    }
}
