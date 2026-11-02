using Game.Simulation.Items;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A stack of clean plates; hands out a new one each time. Endless for now: washing up and
    /// running out of plates come with the dishwasher in Phase 3.
    /// </summary>
    public sealed class PlateStackStation : Interactable
    {
        private ItemDispenser _dispenser;

        public override IItemContainer Container => _dispenser;

        public override void Bind(KitchenServices services)
        {
            _dispenser = new ItemDispenser(Plate.PlateTypeId, () => new Plate(services.Equipment.PlateCapacity));
        }
    }
}
