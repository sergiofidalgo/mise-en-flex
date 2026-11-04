using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;

namespace Game.Simulation.Items
{
    /// <summary>
    /// A container on a counter (decision 2026-11-04): holds portions of one ingredient at a time
    /// and keeps them fresher than a tray, though not as well as a fridge. It takes up a counter, so
    /// where they go is the kitchen's plan. Portions come out one at a time, oldest first.
    /// </summary>
    public sealed class FoodContainer : Item, IItemCollection
    {
        public const string ContainerTypeId = "Container";

        private readonly List<Item> _items = new List<Item>();
        private readonly float _freshnessFactor;

        public FoodContainer(int capacity, float freshnessFactor)
            : base(ContainerTypeId)
        {
            Capacity = capacity;
            _freshnessFactor = freshnessFactor;
        }

        public int Capacity { get; }

        public IReadOnlyList<Item> Items => _items;

        public bool IsEmpty => _items.Count == 0;

        /// <summary>The ingredient it's holding, or null when empty and free for anything.</summary>
        public string HoldsTypeId => _items.Count > 0 ? _items[0].TypeId : null;

        public bool CanTake => _items.Count > 0;

        public bool CanAccept(Item item) =>
            item is Ingredient && _items.Count < Capacity && !_items.Contains(item)
            && (_items.Count == 0 || _items[0].TypeId == item.TypeId);

        public Item Take()
        {
            if (_items.Count == 0)
                throw new InvalidOperationException("Container is empty.");
            var item = _items[0];
            _items.RemoveAt(0);
            ((Ingredient)item).FreshnessFactor = 1f;
            return item;
        }

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("Container can't accept this item.");
            ((Ingredient)item).FreshnessFactor = _freshnessFactor;
            _items.Add(item);
        }
    }
}
