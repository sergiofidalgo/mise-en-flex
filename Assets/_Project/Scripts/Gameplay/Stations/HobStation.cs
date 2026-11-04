using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A hob. It starts with a pan on it; the pan can be lifted off and carried, and any pan
    /// can be put back on an empty hob. While facing it, the triggers turn the heat and Use
    /// flips the food in the pan.
    /// </summary>
    public sealed class HobStation : Interactable, IUsable, IHeatControl, IStationPrompt
    {
        private HobSlot _slot;
        private KitchenServices _services;

        public Hob Hob => _slot?.Hob;

        /// <summary>Null until bound.</summary>
        public KitchenServices Services => _services;

        public override IItemContainer Container => _slot;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        // Its own pan goes with it, so the pan has to be home and empty.
        public override bool IsClear => Hob?.Pan != null && Hob.Pan.Contents.Count == 0;

        public override void Retire(KitchenServices services)
        {
            var pan = Hob.Pan;
            services.ItemViews.Release(pan);
            services.Cooking.Remove(pan);
            services.Cooking.Remove(Hob);
        }

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

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            lines.Add(new PromptLine(PromptControl.Triggers, "heat"));
            var hasFood = Hob?.Pan != null && Hob.Pan.Contents.Count > 0;
            lines.Add(new PromptLine(PromptControl.West, "flip", hasFood));
            if (Hob?.Pan != null && player.Hands.IsEmpty)
                lines.Add(new PromptLine(PromptControl.South, "take pan"));
            else if (Hob != null && Hob.Pan == null && player.Hands.Current is Pan)
                lines.Add(new PromptLine(PromptControl.South, "put pan"));
        }

        public void AdjustHeat(float heatDown, float heatUp, float realDeltaTime)
        {
            if (Hob != null)
                HobControls.AdjustHeat(Hob, heatDown, heatUp, _services.Tuning.HeatDialSpeed, realDeltaTime);
        }
    }
}
