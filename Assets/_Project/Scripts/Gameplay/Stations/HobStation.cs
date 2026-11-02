using Game.Simulation.Cooking;
using Game.Simulation.Items;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A hob. It starts with a pan on it; the pan can be lifted off and carried, and any pan
    /// can be put back on an empty hob. The heat is set in the hob's close-up (Use).
    /// </summary>
    public sealed class HobStation : Interactable
    {
        private HobSlot _slot;

        public Hob Hob => _slot?.Hob;

        public override IItemContainer Container => _slot;

        public override void Bind(KitchenServices services)
        {
            var hob = services.Cooking.AddHob(services.Equipment.Hob);
            _slot = new HobSlot(hob);

            var pan = services.Cooking.AddPan(services.Equipment.Pan);
            _slot.Put(pan);
            services.ItemViews.PlaceImmediately(pan, ItemAnchor);
        }
    }
}
