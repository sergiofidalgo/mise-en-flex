using System.Collections.Generic;
using Game.Simulation.Items;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A plain counter top that holds one item: a plate, a tray, a container. Counters are precious:
    /// each thing kept here is one less place to work (decision 2026-11-04).
    /// </summary>
    public sealed class CounterStation : Interactable, IStationPrompt
    {
        private readonly ItemSlot _slot = new ItemSlot();

        public override IItemContainer Container => _slot;

        /// <summary>What's on this counter, for staff who work from it.</summary>
        public ItemSlot Slot => _slot;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            var held = player.Hands.Current;
            if (_slot.Current is FoodContainer container)
            {
                var what = container.IsEmpty ? "empty" : $"{container.HoldsTypeId} ×{container.Items.Count}/{container.Capacity}";
                if (held == null)
                    lines.Add(new PromptLine(PromptControl.South, container.IsEmpty ? "take container" : $"take: {what}"));
                else if (held is IItemCollection)
                    lines.Add(new PromptLine(PromptControl.South, what));
                else
                    lines.Add(new PromptLine(PromptControl.South, $"add: {what}", container.CanAccept(held)));
                return;
            }

            if (held != null && _slot.Current == null)
                lines.Add(new PromptLine(PromptControl.South, "put down"));
            else if (held == null && _slot.Current != null)
                lines.Add(new PromptLine(PromptControl.South, "take"));
        }
    }
}
