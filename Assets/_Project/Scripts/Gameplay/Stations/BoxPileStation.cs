using System.Collections.Generic;
using Game.Data;
using Game.Simulation.Items;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// The pile of delivery boxes (decision 2026-11-12): free, and it never runs out. A box is
    /// filled and carried as a plate is, so nothing about boxing is new to learn; food in hand
    /// goes straight into a fresh one.
    /// </summary>
    public sealed class BoxPileStation : Interactable, IStationPrompt
    {
        private KitchenServices _services;

        public BoxPile Pile { get; private set; }

        public override IItemContainer Container => Pile;

        public override bool IsClear => true;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            Pile = new BoxPile(services.Equipment.PlateCapacity);
            Pile.Returned += HandleReturned;
        }

        private void OnDestroy()
        {
            if (Pile != null)
                Pile.Returned -= HandleReturned;
        }

        // A box back on the pile is shown by the pile, not as an item.
        private void HandleReturned(Plate box) => _services.ItemViews.Release(box);

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            var held = player.Hands.Current;
            if (held is Tray tray)
            {
                lines.Add(new PromptLine(PromptControl.South, Tr.Get("take box"), tray.HasRoom));
                return;
            }
            if (held == null)
                lines.Add(new PromptLine(PromptControl.South, Tr.Get("take box")));
            else if (ItemTransfer.CanPlate(Pile, held))
                lines.Add(new PromptLine(PromptControl.South, Tr.Get("box it")));
            else
                lines.Add(new PromptLine(PromptControl.South, Tr.Get("put box back"), Pile.CanAccept(held)));
        }
    }
}
