using System.Collections.Generic;
using Game.Simulation.Items;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// Where the knife lives. A takes it into the chef's tool slot (even while carrying food) and
    /// puts it back. Food can't go on the rack.
    /// </summary>
    public sealed class KnifeRackStation : Interactable, IToolStation, IStationPrompt
    {
        private KitchenServices _services;
        private Knife _knife;

        // The rack takes no part in the Interact rules; A is handled as a tool exchange.
        public override IItemContainer Container => null;

        public override Item HeldItem => _knife;

        public bool PromptAsDiamond => false;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            _knife = new Knife();
            services.ItemViews.PlaceImmediately(_knife, ItemAnchor);
        }

        public bool TryExchangeTool(PlayerInteractor player)
        {
            if (_knife != null && player.Tool == null)
            {
                player.ReceiveTool(_knife, ItemAnchor.position);
                _knife = null;
                return true;
            }

            if (_knife == null && player.Tool is Knife knife)
            {
                _knife = (Knife)player.ReleaseTool();
                _services.ItemViews.Place(_knife, ItemAnchor, _services.Tuning.ItemMove, ItemAnchor.position);
                return true;
            }

            return false;
        }

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            if (_knife != null && player.Tool == null)
                lines.Add(new PromptLine(PromptControl.South, "take knife"));
            else if (_knife == null && player.Tool is Knife)
                lines.Add(new PromptLine(PromptControl.South, "put knife back"));
        }
    }

    /// <summary>A station that hands tools to, and takes them back from, the chef's tool slot.</summary>
    public interface IToolStation
    {
        bool TryExchangeTool(PlayerInteractor player);
    }
}
