using System.Collections.Generic;

namespace Game.Simulation.Items
{
    /// <summary>
    /// A carryable item that holds other items, like a pan or a plate.
    /// <see cref="IItemContainer.Take"/> returns the most recently added item.
    /// </summary>
    public interface IItemCollection : IItemContainer
    {
        IReadOnlyList<Item> Items { get; }

        /// <summary>
        /// True if empty hands take the contents one by one before the container itself
        /// (pans: you lift food out with tongs). False if the container is carried whole (plates).
        /// </summary>
        bool TakeContentsFirst { get; }
    }

    /// <summary>A station surface that shows a single item: a counter top, a hob's pan position.</summary>
    public interface IItemHolder
    {
        Item Current { get; }
    }
}
