using System;
using System.Collections.Generic;

namespace Game.Simulation.Items
{
    /// <summary>
    /// A plate: carried whole, holds a few food items, never another container.
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

        public bool CanTake => _items.Count > 0;

        public bool CanAccept(Item item) =>
            item != null && !(item is IItemCollection) && _items.Count < Capacity && !_items.Contains(item);

        public Item Take()
        {
            if (_items.Count == 0)
                throw new InvalidOperationException("Plate is empty.");

            var item = _items[_items.Count - 1];
            _items.RemoveAt(_items.Count - 1);
            return item;
        }

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("Plate can't accept this item.");
            _items.Add(item);
        }
    }
}
