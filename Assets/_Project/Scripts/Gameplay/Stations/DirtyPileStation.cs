using System.Collections.Generic;
using Game.Simulation.Items;

namespace Game.Gameplay.Stations
{
    /// <summary>The pile of dirty plates waiting for the sink.</summary>
    public sealed class DirtyPileStation : Interactable, IStationPrompt
    {
        private KitchenServices _services;

        public DirtyPile Pile { get; private set; }

        public override IItemContainer Container => Pile;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            Pile = new DirtyPile(services.Equipment.DirtyPileCapacity);
            Pile.Received += HandleReceived;
        }

        private void OnDestroy()
        {
            if (Pile != null)
                Pile.Received -= HandleReceived;
        }

        // Plates on the pile are shown by its view, not each as an item.
        private void HandleReceived(Plate plate) => _services.ItemViews.Release(plate);

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            var held = player.Hands.Current;
            if (held is Plate plate && plate.IsDirty)
                lines.Add(new PromptLine(PromptControl.South, Pile.CanAccept(held) ? $"add to pile ({Pile.Count}/{Pile.Capacity})" : "pile is full", Pile.CanAccept(held)));
            else if (held == null)
                lines.Add(new PromptLine(PromptControl.South, $"take dirty plate ({Pile.Count})", Pile.CanTake));
        }
    }
}
