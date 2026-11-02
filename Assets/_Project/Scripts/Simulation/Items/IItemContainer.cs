namespace Game.Simulation.Items
{
    /// <summary>
    /// Anything a player can take items from or put items into: hands, counters, crates.
    /// </summary>
    public interface IItemContainer
    {
        /// <summary>True if <see cref="Take"/> would return an item.</summary>
        bool CanTake { get; }

        /// <summary>True if <paramref name="item"/> can be put here right now.</summary>
        bool CanAccept(Item item);

        /// <summary>Removes and returns an item. Only valid when <see cref="CanTake"/> is true.</summary>
        Item Take();

        /// <summary>Stores <paramref name="item"/>. Only valid when <see cref="CanAccept"/> is true.</summary>
        void Put(Item item);
    }
}
