using System;
using System.Linq;
using Game.Gameplay.Stations;
using Game.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>
    /// Builds storage units: a counter-height body with four bins on top laid out like the face
    /// buttons (Y north, A south, X west, B east, as seen from the camera), a sample of every
    /// ingredient on each bin (the view shows the one in the slot), and the slot setup.
    /// </summary>
    public static class GreyboxStorage
    {
        public sealed class Unit
        {
            public string Name;
            public string FurnitureId;
            public Vector3 Position;
            public bool Refrigerated;
            public Color Color;

            // Portions each of the four slots holds. Any ingredient can go in any slot.
            public int SlotCapacity;

            // What arrives each morning, until shopping (Phase 5).
            public (string TypeId, int Count)[] Deliveries = System.Array.Empty<(string, int)>();
        }

        private const float BinOffset = 0.25f;
        private static readonly Vector3 BinScale = new Vector3(0.24f, 0.03f, 0.24f);
        private static readonly Color BinColor = new Color(0.55f, 0.57f, 0.60f);
        private const float SampleScale = 0.8f;

        // Bin positions in FaceButton order, matching the controller's diamond from the camera.
        private static readonly Vector3[] BinOffsets =
        {
            new Vector3(0f, 0f, -BinOffset), // South: A
            new Vector3(BinOffset, 0f, 0f),  // East: B
            new Vector3(-BinOffset, 0f, 0f), // West: X
            new Vector3(0f, 0f, BinOffset),  // North: Y
        };

        public delegate T StationFactory<T>(string name, Vector3 position, Material material) where T : Interactable;

        public static StorageStation Build(Unit unit, float height, StationFactory<StorageStation> createStation)
        {
            var station = createStation(unit.Name, unit.Position, GreyboxAssets.EnsureMaterial(unit.Name, unit.Color));
            var binMaterial = GreyboxAssets.EnsureMaterial("StorageBin", BinColor);

            var bins = new Transform[BinOffsets.Length];
            var viewSo = new SerializedObject(station.gameObject.AddComponent<StorageView>());
            viewSo.FindProperty("_storage").objectReferenceValue = station;
            var samples = viewSo.FindProperty("_samples");
            samples.arraySize = BinOffsets.Length * GreyboxIngredients.Types.Length;
            var sampleIndex = 0;

            for (var i = 0; i < BinOffsets.Length; i++)
            {
                var bin = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, $"Bin_{(FaceButton)i}", station.transform, binMaterial, keepCollider: false);
                bin.transform.localScale = BinScale;
                bin.transform.localPosition = BinOffsets[i] + Vector3.up * (height + BinScale.y / 2f);
                bins[i] = bin.transform;

                foreach (var type in GreyboxIngredients.Types)
                {
                    var shape = GreyboxIngredients.CreateShape(type, $"Sample_{(FaceButton)i}_{type.Id}", station.transform);
                    shape.transform.localScale = type.Scale * SampleScale;
                    shape.transform.localPosition = BinOffsets[i] + Vector3.up * (height + BinScale.y + type.CentreHeight * SampleScale);
                    shape.SetActive(false);

                    var sample = samples.GetArrayElementAtIndex(sampleIndex++);
                    sample.FindPropertyRelative("_slot").intValue = i;
                    sample.FindPropertyRelative("_typeId").stringValue = type.Id;
                    sample.FindPropertyRelative("_shape").objectReferenceValue = shape;
                }
            }
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(station);
            so.FindProperty("_refrigerated").boolValue = unit.Refrigerated;
            var slots = so.FindProperty("_slots");
            slots.arraySize = BinOffsets.Length;
            for (var i = 0; i < BinOffsets.Length; i++)
            {
                var element = slots.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_capacity").intValue = unit.SlotCapacity;
                element.FindPropertyRelative("_bin").objectReferenceValue = bins[i];
            }
            var deliveries = so.FindProperty("_deliveries");
            deliveries.arraySize = unit.Deliveries.Length;
            for (var i = 0; i < unit.Deliveries.Length; i++)
            {
                var element = deliveries.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_typeId").stringValue = unit.Deliveries[i].TypeId;
                element.FindPropertyRelative("_count").intValue = unit.Deliveries[i].Count;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return station;
        }
    }
}
