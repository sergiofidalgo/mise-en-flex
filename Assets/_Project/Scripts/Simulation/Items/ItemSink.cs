using System;

namespace Game.Simulation.Items
{
    /// <summary>
    /// The bin: takes single items and discards them. Containers aren't thrown away;
    /// <see cref="ItemTransfer"/> scrapes their contents in instead.
    /// </summary>
    public sealed class ItemSink : IItemContainer
    {
        public bool CanTake => false;

        public bool CanAccept(Item item) => item != null && !(item is IItemCollection);

        /// <summary>Raised for every discarded item, so its simulation and view can be cleaned up.</summary>
        public event Action<Item> Discarded;

        public Item Take() => throw new InvalidOperationException("Nothing comes back out of the bin.");

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("The bin doesn't take containers.");
            Discarded?.Invoke(item);
        }
    }
}
