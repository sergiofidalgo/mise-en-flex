using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;

namespace Game.Simulation.Items
{
    /// <summary>
    /// A crate from a street stall (Phase 5): a few portions of one raw ingredient, carried in by
    /// hand. Interact at storage unloads as many as fit; at a counter it's put down whole. Portions
    /// come out oldest first. It doesn't keep food any better or worse than the air around it.
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

        /// <summary>The ingredient it's holding, or null when empty.</summary>
        public string HoldsTypeId => _items.Count > 0 ? _items[0].TypeId : null;

        public bool CanTake => _items.Count > 0;

        // Raw food only: a crate is how stock arrives, not somewhere to keep cooked food.
        public bool CanAccept(Item item) =>
            item is Ingredient ingredient && !ingredient.HasBeenCooked && _items.Count < Capacity && !_items.Contains(item)
            && (_items.Count == 0 || _items[0].TypeId == item.TypeId);

        public Item Take()
        {
            if (_items.Count == 0)
                throw new InvalidOperationException("Crate is empty.");
            var item = _items[0];
            _items.RemoveAt(0);
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
