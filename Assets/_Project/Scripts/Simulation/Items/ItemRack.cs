using System;
using System.Collections.Generic;

namespace Game.Simulation.Items
{
    /// <summary>A rack of empty trays or containers: take one, or put an empty one back.</summary>
    public sealed class ItemRack : IItemContainer
    {
        private readonly Stack<Item> _items = new Stack<Item>();
        private readonly Func<Item, bool> _takesBack;

        /// <param name="takesBack">Whether an item belongs back here (the right kind, and empty).</param>
        public ItemRack(int count, Func<Item> create, Func<Item, bool> takesBack)
        {
            _takesBack = takesBack;
            for (var i = 0; i < count; i++)
                _items.Push(create());
        }

        public int Count => _items.Count;

        /// <summary>Raised when something goes back on the rack, so its view can go.</summary>
        public event Action<Item> Returned;

        public bool CanTake => _items.Count > 0;

        public bool CanAccept(Item item) => item != null && _takesBack(item);

        public Item Take()
        {
            if (_items.Count == 0)
                throw new InvalidOperationException("The rack is empty.");
            return _items.Pop();
        }

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("That doesn't go back on this rack.");
            _items.Push(item);
            Returned?.Invoke(item);
        }
    }
}
