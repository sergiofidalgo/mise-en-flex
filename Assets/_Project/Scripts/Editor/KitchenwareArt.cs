using System.Collections.Generic;
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
        private const float PlateHeight = 0.042f;
        // Plates in the rack stand on edge, one behind the other, on rails this high. Must match
        // RACK_SPACING and RACK_WIRE in blender/build_pilot_models.py.
        private const float RackSpacing = 0.08f;
        private const float RackRailHeight = 0.016f;
        // A loose pile is nudged and turned a little per plate, so it reads as dirty dishes.
        private const float MessyNudge = 0.03f;

        /// <summary>The sink's basin floor, where dirty plates sit; must match build_sink().</summary>
        public const float SinkBasinFloor = 0.75f;
        /// <summary>The basin's centre across the sink, in the game's x: one big basin, in the middle.</summary>
        public const float SinkBasinX = 0f;

        // A soft blue line round a white plate, and a light steel rack (decision 2026-11-08).
        private static readonly Color RimColor = new Color(0.66f, 0.78f, 0.92f);
        private static readonly Color RackSteel = new Color(0.62f, 0.66f, 0.72f);
        // Thin outlines on small things, so six plates and a wire rack don't turn into a tangle of black.
        private const float PlateOutline = 0.0025f;
        private const float WireOutline = 0.0012f;
        private static readonly Color SteelColor = new Color(0.74f, 0.76f, 0.8f);
        private const string BinModel = "SM_Bin";
        private static readonly Color BinGrey = new Color(0.45f, 0.47f, 0.5f);
        private static readonly Color BinBagBlack = new Color(0.12f, 0.12f, 0.13f);

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

            var material = Outlined(EnsurePlateMaterial(), PlateOutline);
            if (layout == PlateLayout.Rack)
            {
                var rack = CreatePart(parent, $"{materialName}Rack", RackModel, "PlateRack",
                    Outlined(StyleTest.EnsureToonMaterial("PlateRack", RackSteel), WireOutline));
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

        /// <summary>
        /// The sink's steel top, basin and tap, on its cabinet (CabinetArt, underSink): a part under
        /// the station's body, so it shakes and lights up with it. Null if there's no model.
        /// </summary>
        public static Renderer DressSink(Transform body)
        {
            var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, "SinkTop", body,
                StyleTest.EnsureToonMaterial("SinkSteel", SteelColor), keepCollider: false);
            if (PilotArt.TryUseMesh(part, SinkModel, "SinkBody"))
                return part.GetComponent<Renderer>();
            Object.DestroyImmediate(part);
            return null;
        }

        /// <summary>
        /// The round bin's model on the bin station's body, grey with a black bag over its rim.
        /// Returns its other parts' renderers, for the station's highlight; empty without the model.
        /// </summary>
        public static List<Renderer> DressBin(Transform body)
        {
            var parts = new List<Renderer>();
            if (!PilotArt.TryUseMesh(body.gameObject, BinModel, "BinBody"))
                return parts;
            body.GetComponent<Renderer>().sharedMaterial = StyleTest.EnsureToonMaterial("BinBody", BinGrey);
            AddBinPart("Bag", "BinBag", StyleTest.EnsureToonMaterial("BinBag", BinBagBlack));
            return parts;

            void AddBinPart(string name, string mesh, Material material)
            {
                var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, body, material, keepCollider: false);
                if (PilotArt.TryUseMesh(part, BinModel, mesh))
                    parts.Add(part.GetComponent<Renderer>());
                else
                    Object.DestroyImmediate(part);
            }
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
            board.GetComponent<Renderer>().sharedMaterial = StyleTest.BoardMaterial(); // wood grain, toon shaded
        }

        private static Material Outlined(Material material, float width)
        {
            material.SetFloat("_OutlineWidth", width);
            EditorUtility.SetDirty(material);
            return material;
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
            CreatePart(plate, "Rim", PlateModel, "PlateRim", Outlined(StyleTest.EnsureToonMaterial("PlateRim", RimColor), PlateOutline));

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
