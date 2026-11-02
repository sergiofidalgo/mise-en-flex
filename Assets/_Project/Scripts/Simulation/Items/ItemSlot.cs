using System;

namespace Game.Simulation.Items
{
    /// <summary>
    /// Holds at most one item. Used for a player's hands and for counter tops.
    /// </summary>
    public sealed class ItemSlot : IItemContainer, IItemHolder
    {
        public Item Current { get; private set; }

        public bool IsEmpty => Current == null;

        public bool CanTake => Current != null;

        public event Action Changed;

        public bool CanAccept(Item item) => item != null && Current == null;

        public Item Take()
        {
            if (Current == null)
                throw new InvalidOperationException("Slot is empty.");

            var item = Current;
            Current = null;
            Changed?.Invoke();
            return item;
        }

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("Slot can't accept this item.");

            Current = item;
            Changed?.Invoke();
        }
    }
}
