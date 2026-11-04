using System;
using System.Collections.Generic;

namespace Game.Simulation.Items
{
    /// <summary>
    /// Carries several portions of one ingredient from the storeroom to the line in one trip
    /// (decision 2026-11-03). Emptied with the Interact rules, like a pan or plate.
    /// </summary>
    public sealed class Crate : Item, IItemCollection
    {
        public const string CrateTypeId = "Crate";

        private readonly List<Item> _items = new List<Item>();

        public Crate(int capacity)
            : base(CrateTypeId)
        {
            Capacity = capacity;
        }

        public int Capacity { get; }

        public IReadOnlyList<Item> Items => _items;

        public bool IsEmpty => _items.Count == 0;

        public bool CanTake => _items.Count > 0;

        public bool CanAccept(Item item) =>
            item != null && !(item is IItemCollection) && _items.Count < Capacity && !_items.Contains(item)
            && (_items.Count == 0 || _items[0].TypeId == item.TypeId);

        public Item Take()
        {
            if (_items.Count == 0)
                throw new InvalidOperationException("Crate is empty.");
            var item = _items[_items.Count - 1];
            _items.RemoveAt(_items.Count - 1);
            return item;
        }

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("Crate can't accept this item.");
            _items.Add(item);
        }
    }
}
