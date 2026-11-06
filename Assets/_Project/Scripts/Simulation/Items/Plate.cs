using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;

namespace Game.Simulation.Items
{
    /// <summary>
    /// A plate: carried whole, holds a few food items, never another container. Food on a plate has
    /// stopped cooking: plating is how a chef says it's done.
    /// </summary>
    public sealed class Plate : Item, IItemCollection
    {
        public const string PlateTypeId = "Plate";

        private readonly List<Item> _items = new List<Item>();

        public Plate(int capacity)
            : base(PlateTypeId)
        {
            Capacity = capacity;
        }

        public int Capacity { get; }

        public IReadOnlyList<Item> Items => _items;

        /// <summary>Used and not washed yet. A dirty plate takes no food.</summary>
        public bool IsDirty { get; private set; }

        /// <summary>The customer has finished: whatever was left goes, and the plate is dirty.</summary>
        public void MarkDirty()
        {
            _items.Clear();
            IsDirty = true;
        }

        public void Clean() => IsDirty = false;

        public bool CanTake => _items.Count > 0;

        public bool CanAccept(Item item) =>
            !IsDirty && item != null && !(item is IItemCollection) && _items.Count < Capacity && !_items.Contains(item);

        public Item Take()
        {
            if (_items.Count == 0)
                throw new InvalidOperationException("Plate is empty.");

            var item = _items[_items.Count - 1];
            _items.RemoveAt(_items.Count - 1);
            if (item is Ingredient food)
                food.IsPlated = false;
            return item;
        }

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("Plate can't accept this item.");
            _items.Add(item);
            if (item is Ingredient food)
                food.IsPlated = true;
        }
    }
}
