using System.Collections.Generic;

namespace Game.Simulation.Items
{
    /// <summary>
    /// A carryable item that holds other items, like a pan or a plate. Containers are always
    /// picked up whole with their contents (decision 2026-11-02).
    /// <see cref="IItemContainer.Take"/> returns the most recently added item.
    /// </summary>
    public interface IItemCollection : IItemContainer
    {
        IReadOnlyList<Item> Items { get; }
    }

    /// <summary>A station surface that shows a single item: a counter top, a hob's pan position.</summary>
    public interface IItemHolder
    {
        Item Current { get; }
    }
}
