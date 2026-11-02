using System.Collections.Generic;
using Game.Data;
using Game.Simulation.Items;
using UnityEngine;

namespace Game.Gameplay.Items
{
    /// <summary>
    /// Owns the one scene object per simulated item, and places views so they match where the
    /// simulation says items are. Created by the scene's composition root.
    /// </summary>
    public sealed class ItemViewRegistry
    {
        // Size of the magenta stand-in cube for item types missing from the catalog.
        private const float MissingItemSize = 0.25f;

        private readonly ItemCatalog _catalog;
        private readonly ItemMoveSettings _snap = new ItemMoveSettings(0f, 0f, 0f);
        private readonly Dictionary<Item, ItemView> _views = new Dictionary<Item, ItemView>();

        public ItemViewRegistry(ItemCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>
        /// Sends <paramref name="item"/>'s view to <paramref name="anchor"/>, and the views of anything
        /// inside it to its slots. Views that are already in place don't move; new views appear at
        /// <paramref name="spawnPosition"/> and fly in.
        /// </summary>
        public void Place(Item item, Transform anchor, ItemMoveSettings move, Vector3 spawnPosition)
        {
            if (item == null)
                return;

            var view = GetOrCreate(item, spawnPosition);
            if (view.Anchor != anchor)
                view.MoveTo(anchor, move);

            if (item is IItemCollection collection)
            {
                for (var i = 0; i < collection.Items.Count; i++)
                    Place(collection.Items[i], view.SlotAnchor(i), move, view.transform.position);
            }
        }

        /// <summary>Places an item instantly, e.g. a pan set on a hob when the scene starts.</summary>
        public void PlaceImmediately(Item item, Transform anchor) => Place(item, anchor, _snap, anchor.position);

        /// <summary>Destroys the view of an item that left the game (binned, served).</summary>
        public void Release(Item item)
        {
            if (!_views.TryGetValue(item, out var view))
                return;

            _views.Remove(item);
            if (view != null)
                Object.Destroy(view.gameObject);
        }

        private ItemView GetOrCreate(Item item, Vector3 spawnPosition)
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
