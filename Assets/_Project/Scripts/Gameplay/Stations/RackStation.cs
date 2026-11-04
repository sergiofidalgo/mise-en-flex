using System.Collections.Generic;
using Game.Simulation.Items;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>Where the kitchen's empty trays or containers live.</summary>
    public sealed class RackStation : Interactable, IStationPrompt
    {
        public enum RackKind
        {
            Trays,
            Containers,
        }

        [SerializeField] private RackKind _kind;

        private KitchenServices _services;

        public ItemRack Rack { get; private set; }

        public override IItemContainer Container => Rack;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            var equipment = services.Equipment;
            Rack = _kind == RackKind.Trays
                ? new ItemRack(equipment.TrayCount, () => new Tray(equipment.TrayCapacity, equipment.TrayFreshnessFactor), item => item is Tray tray && tray.IsEmpty)
                : new ItemRack(equipment.ContainerCount, () => new FoodContainer(equipment.ContainerCapacity, equipment.ContainerFreshnessFactor), item => item is FoodContainer container && container.IsEmpty);
            Rack.Returned += HandleReturned;
        }

        private void OnDestroy()
        {
            if (Rack != null)
                Rack.Returned -= HandleReturned;
        }

        private void HandleReturned(Item item) => _services.ItemViews.Release(item);

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            var name = _kind == RackKind.Trays ? "tray" : "container";
            var held = player.Hands.Current;
            if (held != null)
                lines.Add(new PromptLine(PromptControl.South, $"put {name} back", Rack.CanAccept(held)));
            else
                lines.Add(new PromptLine(PromptControl.South, $"take {name} ({Rack.Count})", Rack.CanTake));
        }
    }
}
