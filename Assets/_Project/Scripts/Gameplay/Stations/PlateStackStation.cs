using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Items;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// The stack of clean plates. Limited: used plates come back through the sink.
    /// </summary>
    public sealed class PlateStackStation : Interactable, IStationPrompt, Saving.ISavedCount
    {
        private KitchenServices _services;

        public PlateStack Stack { get; private set; }

        public override IItemContainer Container => Stack;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            Stack = new PlateStack(services.Equipment.PlateCount, services.Equipment.PlateCapacity, services.Equipment.CleanPileCapacity);
            Stack.Returned += HandleReturned;
        }

        public int SavedCount => Stack.Count;

        public void RestoreCount(int count)
        {
            while (Stack.Count > count)
                Stack.Take();
            if (Stack.Count < count)
                Stack.AddNew(count - Stack.Count, _services.Equipment.PlateCapacity);
        }

        private void OnDestroy()
        {
            if (Stack != null)
                Stack.Returned -= HandleReturned;
        }

        // Plates on the stack are shown by its view, not each as an item.
        private void HandleReturned(Plate plate) => _services.ItemViews.Release(plate);

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            var held = player.Hands.Current;
            if (held is Tray tray)
            {
                if (tray.Items.Any(Stack.CanAccept))
                    lines.Add(new PromptLine(PromptControl.South, "put plates back"));
                else
                    lines.Add(new PromptLine(PromptControl.South, $"take plate ({Stack.Count})", Stack.CanTake && tray.HasRoom));
                return;
            }

            if (held != null)
            {
                // Food in hand goes straight onto a fresh plate (decision 2026-11-08).
                if (ItemTransfer.CanPlate(Stack, held))
                    lines.Add(new PromptLine(PromptControl.South, "plate it"));
                else
                    lines.Add(new PromptLine(PromptControl.South, "put plate back", Stack.CanAccept(held)));
                return;
            }
            lines.Add(Stack.CanTake
                ? new PromptLine(PromptControl.South, $"take plate ({Stack.Count})")
                : new PromptLine(PromptControl.South, "no clean plates", false));
        }
    }
}
