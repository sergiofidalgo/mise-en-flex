using Game.Presentation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Models for plates, the plate stack's rack, the sink and the cutting board, swapped in by the
    /// builders when the models exist. Only meshes change: footprints, anchors, slots and colliders
    /// stay as the greybox builders made them, so gameplay is untouched.
    /// </summary>
    public static class KitchenwareArt
    {
        private const string PlateModel = "SM_Plate";
        private const string RackModel = "SM_PlateRack";
        private const string SinkModel = "SM_Sink";
        private const string BoardModel = "SM_Board";

        // Must match PLATE_RADIUS in blender/build_pilot_models.py; the plate's origin is its base.
        private const float PlateRadius = 0.16f;
        private const float PlateHeight = 0.035f;
        // Plates in a rack stand one behind the other, resting on rails this high.
        private const float RackSpacing = 0.055f;
        private const float RackRailHeight = 0.037f;
        // A loose pile is nudged and turned a little per plate, so it reads as dirty dishes.
        private const float MessyNudge = 0.03f;

        /// <summary>The sink's basin floor, where dirty plates sit; must match build_sink().</summary>
        public const float SinkBasinFloor = 0.75f;
        /// <summary>The basin's centre across the sink, in the game's x: one big basin, in the middle.</summary>
        public const float SinkBasinX = 0f;

        private static readonly Color RimColor = new Color(0.8f, 0.64f, 0.3f);
        private static readonly Color RackColor = new Color(0.08f, 0.08f, 0.09f);
        private static readonly Color SteelColor = new Color(0.72f, 0.74f, 0.77f);
        private static readonly Color BambooColor = new Color(0.96f, 0.84f, 0.56f);

        public enum PlateLayout
        {
            /// <summary>Standing in a wire rack, facing the camera, from the back to the front.</summary>
            Rack,
            /// <summary>A neat flat stack.</summary>
            Stack,
            /// <summary>A flat stack, each plate nudged and turned a little.</summary>
            Messy,
        }

        public static bool HasPlateModel => PilotArt.FindMesh(PlateModel, "PlateBody") != null;

        /// <summary>
        /// Plates for a pile view (PlatePileView shows the first <c>n</c>), so the last one made is
        /// the first to go. Falls back to the greybox discs without the plate model.
        /// </summary>
        public static GameObject[] CreatePlates(Transform parent, float baseHeight, int count, Color color, string materialName,
            Vector3 offset, PlateLayout layout, bool dirty = false)
        {
            if (!HasPlateModel)
                return GreyboxKitchenware.CreatePlatePile(parent, baseHeight, count, color, materialName, offset);

            var material = EnsurePlateMaterial();
            if (layout == PlateLayout.Rack)
            {
                var rack = CreatePart(parent, $"{materialName}Rack", RackModel, count > 3 ? "PlateRack" : "PlateRackShort",
                    GreyboxAssets.EnsureMaterial("PlateRack", RackColor));
                rack.transform.localPosition = offset + Vector3.up * baseHeight;
            }

            var plates = new GameObject[count];
            for (var i = 0; i < count; i++)
            {
                var plate = CreatePlate(parent, $"{materialName}_{i + 1}", material);
                if (dirty)
                {
                    // A new mess each time it's shown.
                    var grime = new SerializedObject(plate.AddComponent<PlateGrime>());
                    grime.FindProperty("_body").objectReferenceValue = plate.GetComponent<Renderer>();
                    grime.ApplyModifiedPropertiesWithoutUndo();
                }
                switch (layout)
                {
                    case PlateLayout.Rack:
                        // Stood on its edge facing the camera: its base turns towards the camera.
                        plate.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                        plate.transform.localPosition = offset + new Vector3(0f, baseHeight + RackRailHeight + PlateRadius,
                            ((count - 1) / 2f - i) * RackSpacing);
                        break;
                    case PlateLayout.Messy:
                        plate.transform.localRotation = Quaternion.Euler(0f, Noise(i, 1) * 360f, 0f);
                        plate.transform.localPosition = offset + new Vector3((Noise(i, 2) - 0.5f) * 2f * MessyNudge,
                            baseHeight + i * PlateHeight, (Noise(i, 3) - 0.5f) * 2f * MessyNudge);
                        break;
                    default:
                        plate.transform.localPosition = offset + Vector3.up * (baseHeight + i * PlateHeight);
                        break;
                }
                plates[i] = plate;
            }
            return plates;
        }

        /// <summary>Gives a carried plate's body the plate model and its gold rim.</summary>
        public static void DressPlateItem(GameObject body)
        {
            if (!PilotArt.TryUseMesh(body, PlateModel, "PlateBody"))
                return;
            body.GetComponent<Renderer>().sharedMaterial = EnsurePlateMaterial();
            AddRim(body.transform);
        }

        /// <summary>The sink unit's model, on the station's body. False if there's no model.</summary>
        public static bool DressSink(GameObject body)
        {
            if (!PilotArt.TryUseMesh(body, SinkModel, "SinkBody"))
                return false;
            body.GetComponent<Renderer>().sharedMaterial = GreyboxAssets.EnsureMaterial("SinkSteel", SteelColor);
            return true;
        }

        /// <summary>
        /// The bamboo board's model, on the board's own object, which stays where it is so its
        /// highlight and shake keep working. The model's base sits on the counter.
        /// </summary>
        public static void DressBoard(GameObject board, float counterHeight)
        {
            if (!PilotArt.TryUseMesh(board, BoardModel, "BoardBody"))
                return;
            board.transform.localPosition = Vector3.up * counterHeight;
            board.GetComponent<Renderer>().sharedMaterial = GreyboxAssets.EnsureMaterial("Bamboo", BambooColor);
        }

        /// <summary>The plate shader's material (MAT_Plate): clean, until a view makes it dirty.</summary>
        public static Material EnsurePlateMaterial()
        {
            const string path = "Assets/_Project/Art/Materials/MAT_Plate.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Game/Kitchen/Plate"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject CreatePlate(Transform parent, string name, Material material)
        {
            var plate = CreatePart(parent, name, PlateModel, "PlateBody", material);
            AddRim(plate.transform);
            return plate;
        }

        private static void AddRim(Transform plate) =>
            CreatePart(plate, "Rim", PlateModel, "PlateRim", GreyboxAssets.EnsureMaterial("PlateRim", RimColor));

        private static GameObject CreatePart(Transform parent, string name, string model, string mesh, Material material)
        {
            var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cylinder, name, parent, material, keepCollider: false);
            PilotArt.TryUseMesh(part, model, mesh);
            return part;
        }

        // A repeatable 0..1 per plate and channel, so the messy pile looks the same every build.
        private static float Noise(int index, int channel)
        {
            unchecked
            {
                var hash = (uint)(index * 73856093) ^ (uint)(channel * 19349663);
                hash *= 2654435761u;
                return (hash >> 8) / 16777216f;
            }
        }
    }
}
