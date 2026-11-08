using System;

namespace Game.Simulation.Items
{
    /// <summary>
    /// The bin: takes single items and discards them, until its bag is full. Containers aren't
    /// thrown away; <see cref="ItemTransfer"/> scrapes their contents in instead, one item at a
    /// time, so a plate scraped into a nearly full bin keeps whatever didn't fit.
    /// </summary>
    public sealed class ItemSink : IItemContainer
    {
        /// <param name="capacity">How many things fill the bag; unlimited if left out.</param>
        public ItemSink(int capacity = int.MaxValue)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity), "A bin holds at least one thing.");
            Capacity = capacity;
        }

        public int Capacity { get; }

        /// <summary>How many things are in the bag now.</summary>
        public int Count { get; private set; }

        public bool IsFull => Count >= Capacity;

        /// <summary>How full the bag is, from 0 (empty) to 1 (full).</summary>
        public float Fill => Capacity == int.MaxValue ? 0f : (float)Count / Capacity;

        public bool CanTake => false;

        public bool CanAccept(Item item) => item != null && !(item is IItemCollection) && !IsFull;

        /// <summary>Raised for every discarded item, so its simulation and view can be cleaned up.</summary>
        public event Action<Item> Discarded;

        /// <summary>Raised when the bag is emptied.</summary>
        public event Action Emptied;

        public Item Take() => throw new InvalidOperationException("Nothing comes back out of the bin.");

        public void Put(Item item)
        {
            if (item is IItemCollection)
                throw new InvalidOperationException("The bin doesn't take containers.");
            if (!CanAccept(item))
                throw new InvalidOperationException("The bin is full.");
            Count++;
            Discarded?.Invoke(item);
        }

        /// <summary>
        /// A fresh bag. TODO: for now this happens each new day; later players carry the full bag
        /// out to the street containers (docs/ideas.md).
        /// </summary>
        public void Empty()
        {
            if (Count == 0)
                return;
            Count = 0;
            Emptied?.Invoke();
        }
    }
}
