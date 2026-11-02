using System.Collections.Generic;
using Game.Data;
using Game.Simulation.Items;
using UnityEngine;

namespace Game.Gameplay.Items
{
    /// <summary>
    /// Owns the one scene object per simulated item. Created by the scene's composition root.
    /// </summary>
    public sealed class ItemViewRegistry
    {
        // Size of the magenta stand-in cube for item types missing from the catalog.
        private const float MissingItemSize = 0.25f;

        private readonly ItemCatalog _catalog;
        private readonly Dictionary<Item, ItemView> _views = new Dictionary<Item, ItemView>();

        public ItemViewRegistry(ItemCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>Returns the item's view, creating it at <paramref name="spawnPosition"/> the first time.</summary>
        public ItemView GetOrCreate(Item item, Vector3 spawnPosition)
        {
            if (_views.TryGetValue(item, out var existing))
                return existing;

            var prefab = _catalog.GetPrefab(item.TypeId);
            var instance = prefab != null
                ? Object.Instantiate(prefab, spawnPosition, Quaternion.identity)
                : CreateMissingItem(item, spawnPosition);
            instance.name = $"Item_{item.TypeId}";

            var view = instance.GetComponent<ItemView>();
            if (view == null)
                view = instance.AddComponent<ItemView>();

            view.Initialize(item);
            _views.Add(item, view);
            return view;
        }

        // A loud placeholder so a missing catalog entry is obvious in play instead of invisible.
        private static GameObject CreateMissingItem(Item item, Vector3 position)
        {
            Debug.LogWarning($"[ItemViewRegistry] No prefab for item type '{item.TypeId}' in the item catalog.");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(cube.GetComponent<Collider>());
            cube.transform.position = position;
            cube.transform.localScale = Vector3.one * MissingItemSize;
            cube.GetComponent<Renderer>().material.SetColor("_BaseColor", Color.magenta);
            return cube;
        }
    }
}
