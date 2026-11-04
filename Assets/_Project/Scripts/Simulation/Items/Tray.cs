using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;

namespace Game.Simulation.Items
{
    /// <summary>
    /// A tray (decision 2026-11-04): carries up to four things in any mix, ingredients or plates
    /// (empty, filled or dirty). It takes up a counter when put down, and food loose on it ages
    /// faster than anywhere else, so it's for moving things, not keeping them.
    /// </summary>
    public sealed class Tray : Item, IItemCollection
    {
        public const string TrayTypeId = "Tray";

        private readonly List<Item> _items = new List<Item>();
        private readonly float _freshnessFactor;

        public Tray(int capacity, float freshnessFactor)
            : base(TrayTypeId)
        {
            Capacity = capacity;
            _freshnessFactor = freshnessFactor;
        }

        public int Capacity { get; }

        public IReadOnlyList<Item> Items => _items;

        public bool IsEmpty => _items.Count == 0;

        public bool HasRoom => _items.Count < Capacity;

        public bool CanTake => _items.Count > 0;

        public bool CanAccept(Item item) => (item is Ingredient || item is Plate) && HasRoom && !_items.Contains(item);

        /// <summary>Takes what went on last.</summary>
        public Item Take()
        {
            if (_items.Count == 0)
                throw new InvalidOperationException("Tray is empty.");
            return TakeAt(_items.Count - 1);
        }

        /// <summary>Takes one chosen item off the tray.</summary>
        public Item TakeAt(int index)
        {
            var item = _items[index];
            _items.RemoveAt(index);
            if (item is Ingredient ingredient)
                ingredient.FreshnessFactor = 1f;
            return item;
        }

        public void Put(Item item) => InsertAt(_items.Count, item);

        /// <summary>Puts an item back where it was, so a choice that couldn't happen changes nothing.</summary>
        public void InsertAt(int index, Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("Tray can't accept this item.");
            if (item is Ingredient ingredient)
                ingredient.FreshnessFactor = _freshnessFactor;
            _items.Insert(index, item);
        }
    }

    /// <summary>
    /// Somewhere a tray can pick plates up from: the plate stack, the sink's clean plates, a pass
    /// counter. Plates aren't handed to trays by anything else.
    /// </summary>
    public interface IPlateSource : IItemContainer
    {
    }
}
