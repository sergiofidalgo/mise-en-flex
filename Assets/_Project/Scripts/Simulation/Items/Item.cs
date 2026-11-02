namespace Game.Simulation.Items
{
    /// <summary>
    /// A single thing that can be carried and put down. Phase 0 items only have a type;
    /// cooking state (form, doneness, temperature, ...) arrives in Phase 1.
    /// </summary>
    public class Item
    {
        public Item(string typeId)
        {
            TypeId = typeId;
        }

        /// <summary>Which kind of item this is, e.g. "Tomato". Matches an entry in the item catalog.</summary>
        public string TypeId { get; }
    }
}
