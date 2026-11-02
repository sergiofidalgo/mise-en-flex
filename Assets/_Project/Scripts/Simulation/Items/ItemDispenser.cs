using System;

namespace Game.Simulation.Items
{
    /// <summary>
    /// An endless supply of one item type, like an ingredient crate. Phase 0 placeholder:
    /// replaced by the 4-slot fridge with limited stock in Phase 1, milestone 6.
    /// </summary>
    public sealed class ItemDispenser : IItemContainer
    {
        private readonly Func<Item> _create;

        /// <param name="create">Makes each new item (e.g. registers an ingredient with the cooking
        /// simulation). Defaults to a plain <see cref="Item"/> of <paramref name="typeId"/>.</param>
        public ItemDispenser(string typeId, Func<Item> create = null)
        {
            TypeId = typeId;
            _create = create ?? (() => new Item(typeId));
        }

        public string TypeId { get; }

        public bool CanTake => true;

        // Crates don't take things back; putting food down needs a counter.
        public bool CanAccept(Item item) => false;

        public Item Take() => _create();

        public void Put(Item item) => throw new InvalidOperationException("Dispensers don't accept items.");
    }
}
