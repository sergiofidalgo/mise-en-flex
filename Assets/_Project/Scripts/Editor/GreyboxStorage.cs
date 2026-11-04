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
    /// buttons (Y north, A south, X west, B east, as seen from the camera), a sample of each
    /// slot's ingredient, and the slot setup (ingredient, capacity, starting stock).
    /// </summary>
    public static class GreyboxStorage
    {
        public sealed class Unit
        {
            public string Name;
            public Vector3 Position;
            public bool Refrigerated;
            public bool GivesCrates;

            // Portions delivered each morning, per slot.
            public int[] Deliveries = new int[4];
            public Color Color;

            // In FaceButton order: South (A), East (B), West (X), North (Y). Null = unused slot.
            public (string TypeId, int Capacity, int StartStock)?[] Slots = new (string, int, int)?[4];
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

        public static void Build(Unit unit, float height, StationFactory<StorageStation> createStation)
        {
            var station = createStation(unit.Name, unit.Position, GreyboxAssets.EnsureMaterial(unit.Name, unit.Color));
            var binMaterial = GreyboxAssets.EnsureMaterial("StorageBin", BinColor);

            var bins = new Transform[BinOffsets.Length];
            var samples = new GameObject[BinOffsets.Length];
            for (var i = 0; i < BinOffsets.Length; i++)
            {
                var bin = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, $"Bin_{(FaceButton)i}", station.transform, binMaterial, keepCollider: false);
                bin.transform.localScale = BinScale;
                bin.transform.localPosition = BinOffsets[i] + Vector3.up * (height + BinScale.y / 2f);
                bins[i] = bin.transform;

                // Every bin gets a sample object so references are never empty; unused bins' stay empty.
                var slot = unit.Slots[i];
                var type = slot.HasValue ? GreyboxIngredients.Types.FirstOrDefault(t => t.Id == slot.Value.TypeId) : null;
                GameObject sample;
                if (type != null)
                {
                    sample = GreyboxIngredients.CreateShape(type, $"Sample_{type.Id}", station.transform);
                    sample.transform.localScale = type.Scale * SampleScale;
                    sample.transform.localPosition = BinOffsets[i] + Vector3.up * (height + BinScale.y + type.CentreHeight * SampleScale);
                }
                else
                {
                    sample = new GameObject("Sample_None");
                    sample.transform.SetParent(station.transform, false);
                }
                samples[i] = sample;
            }

            var so = new SerializedObject(station);
            so.FindProperty("_refrigerated").boolValue = unit.Refrigerated;
            so.FindProperty("_givesCrates").boolValue = unit.GivesCrates;
            var slots = so.FindProperty("_slots");
            slots.arraySize = BinOffsets.Length;
            for (var i = 0; i < BinOffsets.Length; i++)
            {
                var element = slots.GetArrayElementAtIndex(i);
                var slot = unit.Slots[i];
                element.FindPropertyRelative("_typeId").stringValue = slot?.TypeId ?? string.Empty;
                element.FindPropertyRelative("_capacity").intValue = slot?.Capacity ?? 0;
                element.FindPropertyRelative("_startStock").intValue = slot?.StartStock ?? 0;
                element.FindPropertyRelative("_dailyDelivery").intValue = unit.Deliveries[i];
                element.FindPropertyRelative("_bin").objectReferenceValue = bins[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var view = station.gameObject.AddComponent<StorageView>();
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("_storage").objectReferenceValue = station;
            var list = viewSo.FindProperty("_samples");
            list.arraySize = samples.Length;
            for (var i = 0; i < samples.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = samples[i];
            viewSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
