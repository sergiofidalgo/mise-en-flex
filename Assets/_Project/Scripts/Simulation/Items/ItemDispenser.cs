using System;

namespace Game.Simulation.Items
{
    /// <summary>
    /// An endless supply of one item type, like an ingredient crate. Phase 0 placeholder:
    /// stock, cost and suppliers come with the economy in Phase 5.
    /// </summary>
    public sealed class ItemDispenser : IItemContainer
    {
        public ItemDispenser(string typeId)
        {
            TypeId = typeId;
        }

        public string TypeId { get; }

        public bool CanTake => true;

        // Crates don't take things back; putting food down needs a counter.
        public bool CanAccept(Item item) => false;

        public Item Take() => new Item(TypeId);

        public void Put(Item item) => throw new InvalidOperationException("Dispensers don't accept items.");
    }
}
