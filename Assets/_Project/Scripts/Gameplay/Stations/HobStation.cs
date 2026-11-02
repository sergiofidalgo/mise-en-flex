using Game.Simulation.Cooking;
using Game.Simulation.Items;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A hob. It starts with a pan on it; the pan can be lifted off and carried, and any pan
    /// can be put back on an empty hob. While facing it, the triggers turn the heat and Use
    /// flips the food in the pan.
    /// </summary>
    public sealed class HobStation : Interactable, IUsable, IHeatControl
    {
        private HobSlot _slot;
        private KitchenServices _services;

        public Hob Hob => _slot?.Hob;

        public override IItemContainer Container => _slot;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            var hob = services.Cooking.AddHob(services.Equipment.Hob);
            _slot = new HobSlot(hob);

            var pan = services.Cooking.AddPan(services.Equipment.Pan);
            _slot.Put(pan);
            services.ItemViews.PlaceImmediately(pan, ItemAnchor);
        }

        public bool TryUse(PlayerInteractor player) => Hob != null && HobControls.TryFlip(Hob);

        public void AdjustHeat(float heatDown, float heatUp, float realDeltaTime)
        {
            if (Hob != null)
                HobControls.AdjustHeat(Hob, heatDown, heatUp, _services.Tuning.HeatDialSpeed, realDeltaTime);
        }
    }
}
