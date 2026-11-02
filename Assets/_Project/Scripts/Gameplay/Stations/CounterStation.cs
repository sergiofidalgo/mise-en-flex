using Game.Simulation.Items;

namespace Game.Gameplay.Stations
{
    /// <summary>A plain counter top that holds one item.</summary>
    public sealed class CounterStation : Interactable
    {
        private readonly ItemSlot _slot = new ItemSlot();

        public override IItemContainer Container => _slot;
    }
}
