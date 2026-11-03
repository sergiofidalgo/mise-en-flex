using System.Collections.Generic;
using Game.Simulation.Items;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// The sink: drop dirty plates in, scrub with Use, take clean ones back to the stack.
    /// </summary>
    public sealed class SinkStation : Interactable, IUsable, IStationPrompt
    {
        private KitchenServices _services;

        public Sink Sink { get; private set; }

        public override IItemContainer Container => Sink;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            Sink = new Sink();
            Sink.Received += HandleReceived;
        }

        private void OnDestroy()
        {
            if (Sink != null)
                Sink.Received -= HandleReceived;
        }

        // Plates in the sink are shown by its view, not each as an item.
        private void HandleReceived(Plate plate) => _services.ItemViews.Release(plate);

        public bool TryUse(PlayerInteractor player)
        {
            if (Sink.DirtyCount == 0)
                return false;
            Sink.Wash(1f / _services.Tuning.ScrubsPerPlate);
            return true;
        }

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            var scrubs = _services.Tuning.ScrubsPerPlate;
            var done = Mathf.RoundToInt(Sink.WashProgress * scrubs);
            lines.Add(new PromptLine(PromptControl.West, Sink.DirtyCount > 0 ? $"wash {done}/{scrubs}" : "nothing to wash", Sink.DirtyCount > 0));

            var held = player.Hands.Current;
            if (held != null)
                lines.Add(new PromptLine(PromptControl.South, "drop plate", Sink.CanAccept(held)));
            else if (Sink.CanTake)
                lines.Add(new PromptLine(PromptControl.South, $"take clean ({Sink.CleanCount})"));
        }
    }
}
