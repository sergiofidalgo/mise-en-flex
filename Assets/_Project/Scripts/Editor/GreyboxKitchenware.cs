using Game.Gameplay.Items;
using Game.Simulation.Items;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds the pan and plate prefabs: a primitive body plus a slot per item they can hold.
    /// Stylised, slightly oversized proportions so they read from the overhead camera.
    /// </summary>
    public static class GreyboxKitchenware
    {
        public const string PanTypeId = "Pan";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Items";

        private const float PanDiameter = 0.44f;
        private const float PanDepth = 0.04f;
        private static readonly Vector3 PanHandleScale = new Vector3(0.05f, 0.03f, 0.22f);
        private static readonly Vector3[] PanSlots = { new Vector3(-0.1f, 0f, 0f), new Vector3(0.1f, 0f, 0f) };

        private const float PlateDiameter = 0.32f;
        private const float PlateThickness = 0.015f;
        private static readonly Vector3[] PlateSlots =
        {
            new Vector3(0f, 0f, 0.05f), new Vector3(-0.07f, 0f, -0.05f), new Vector3(0.07f, 0f, -0.05f),
        };

        public static readonly Color PanColor = new Color(0.30f, 0.30f, 0.33f);
        public static readonly Color PlateColor = new Color(0.95f, 0.95f, 0.93f);

        /// <summary>Builds the pan prefab. Returns the root, its body renderer, and the prefab path.</summary>
        public static GameObject BuildPan(System.Action<GameObject, Renderer> decorate)
        {
            var root = new GameObject($"PF_Item_{PanTypeId}");
            var body = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cylinder, "Body", root.transform, GreyboxAssets.EnsureMaterial("Pan", PanColor), keepCollider: false);
            body.transform.localScale = new Vector3(PanDiameter, PanDepth / 2f, PanDiameter); // cylinder is 2 units tall
            body.transform.localPosition = new Vector3(0f, PanDepth / 2f, 0f);

            // Handle pointing towards the camera (south), so the pan reads as a pan from above.
            var handle = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cube, "Handle", root.transform, GreyboxAssets.EnsureMaterial("Pan", PanColor), keepCollider: false);
            handle.transform.localScale = PanHandleScale;
            handle.transform.localPosition = new Vector3(0f, PanDepth * 0.75f, -(PanDiameter + PanHandleScale.z) / 2f);

            AddSlots(root, PanSlots, PanDepth);
            decorate?.Invoke(root, body.GetComponent<Renderer>());
            return Save(root, PanTypeId);
        }

        public static GameObject BuildPlate()
        {
            var root = new GameObject($"PF_Item_{Plate.PlateTypeId}");
            var body = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cylinder, "Body", root.transform, GreyboxAssets.EnsureMaterial("Plate", PlateColor), keepCollider: false);
            body.transform.localScale = new Vector3(PlateDiameter, PlateThickness / 2f, PlateDiameter);
            body.transform.localPosition = new Vector3(0f, PlateThickness / 2f, 0f);

            AddSlots(root, PlateSlots, PlateThickness);
            return Save(root, Plate.PlateTypeId);
        }

        /// <summary>A short stack of plate discs, as a label for the plate stack station.</summary>
        public static void CreatePlateStackVisual(Transform parent, float baseHeight, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var disc = GreyboxAssets.CreatePrimitive(
                    PrimitiveType.Cylinder, $"Plate_{i + 1}", parent, GreyboxAssets.EnsureMaterial("Plate", PlateColor), keepCollider: false);
                disc.transform.localScale = new Vector3(PlateDiameter, PlateThickness / 2f, PlateDiameter);
                disc.transform.localPosition = new Vector3(0f, baseHeight + PlateThickness * (i + 0.5f) * 1.6f, 0f);
            }
        }

        private static void AddSlots(GameObject root, Vector3[] positions, float height)
        {
            var slots = new Transform[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                slots[i] = new GameObject($"Slot_{i + 1}").transform;
                slots[i].SetParent(root.transform, false);
                slots[i].localPosition = positions[i] + Vector3.up * height;
            }

            var view = root.AddComponent<ItemView>();
            var so = new SerializedObject(view);
            var list = so.FindProperty("_slotAnchors");
            list.arraySize = slots.Length;
            for (var i = 0; i < slots.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Save(GameObject root, string typeId)
        {
            GreyboxAssets.EnsureFolder(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/PF_Item_{typeId}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
