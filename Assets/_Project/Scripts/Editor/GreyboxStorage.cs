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
    /// Builds storage units: a tall body with four baskets in two tiers of two seen from the front
    /// (Y and B on top, X and A below), a sample of every ingredient on each basket (the view shows
    /// the one in the slot), and the slot setup. Models and slot positions come from StorageArt.
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

            // What's here on a new campaign's first morning, once; after that it's bought at the stalls.
            public (string TypeId, int Count)[] Deliveries = System.Array.Empty<(string, int)>();
        }

        private const int SlotCount = 4; // one per face button
        private static readonly Vector3 BinScale = new Vector3(0.24f, 0.03f, 0.24f);
        private static readonly Color BinColor = new Color(0.55f, 0.57f, 0.60f);
        private const float SampleScale = 0.8f;

        public delegate T StationFactory<T>(string name, Vector3 position, Material material) where T : Interactable;

        public static StorageStation Build(Unit unit, StationFactory<StorageStation> createStation)
        {
            var station = createStation(unit.Name, unit.Position, GreyboxAssets.EnsureMaterial(unit.Name, unit.Color));
            var binMaterial = GreyboxAssets.EnsureMaterial("StorageBin", BinColor);

            var bins = new Transform[SlotCount];
            var viewSo = new SerializedObject(station.gameObject.AddComponent<StorageView>());
            viewSo.FindProperty("_storage").objectReferenceValue = station;
            var samples = viewSo.FindProperty("_samples");
            samples.arraySize = SlotCount * GreyboxIngredients.Types.Length;
            var sampleIndex = 0;

            for (var i = 0; i < SlotCount; i++)
            {
                // Two tiers of two, seen from the front (StorageArt.SlotPosition).
                var slot = StorageArt.SlotPosition(unit.Refrigerated, i);
                var bin = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, $"Bin_{(FaceButton)i}", station.transform, binMaterial, keepCollider: false);
                bin.transform.localScale = BinScale;
                bin.transform.localPosition = slot + Vector3.up * (BinScale.y / 2f);
                var binTop = BinScale.y;
                // A modelled basket has its origin at its base, sitting on the shelf.
                if (PilotArt.TryUseMesh(bin, StorageArt.ModelFor(unit.Refrigerated), StorageArt.BasketMesh(unit.Refrigerated)))
                {
                    bin.GetComponent<Renderer>().sharedMaterial = StorageArt.BasketMaterial(unit.Refrigerated);
                    bin.transform.localPosition = slot;
                    binTop = StorageArt.BasketHeight;
                }
                bins[i] = bin.transform;

                foreach (var type in GreyboxIngredients.Types)
                {
                    var shape = StorageArt.CreateSample(type, $"Sample_{(FaceButton)i}_{type.Id}", station.transform, SampleScale, out var baseOffset);
                    shape.transform.localPosition = slot + Vector3.up * (binTop + baseOffset);
                    shape.SetActive(false);

                    var sample = samples.GetArrayElementAtIndex(sampleIndex++);
                    sample.FindPropertyRelative("_slot").intValue = i;
                    sample.FindPropertyRelative("_typeId").stringValue = type.Id;
                    sample.FindPropertyRelative("_shape").objectReferenceValue = shape;
                }
            }
            viewSo.ApplyModifiedPropertiesWithoutUndo();
            StorageArt.Dress(station, unit.Refrigerated);

            var so = new SerializedObject(station);
            so.FindProperty("_refrigerated").boolValue = unit.Refrigerated;
            var slots = so.FindProperty("_slots");
            slots.arraySize = SlotCount;
            for (var i = 0; i < SlotCount; i++)
            {
                var element = slots.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_capacity").intValue = unit.SlotCapacity;
                element.FindPropertyRelative("_bin").objectReferenceValue = bins[i];
            }
            var deliveries = so.FindProperty("_starterStock");
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
