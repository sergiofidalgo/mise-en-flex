using Game.Data;
using System.Collections.Generic;
using Game.Simulation.Items;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>Where the kitchen's empty trays or containers live.</summary>
    public sealed class RackStation : Interactable, IStationPrompt, Saving.ISavedCount
    {
        public enum RackKind
        {
            Trays,
            Containers,
        }

        [SerializeField] private RackKind _kind;

        private KitchenServices _services;
        private System.Func<Item> _create;

        public RackKind Kind => _kind;

        public ItemRack Rack { get; private set; }

        public override IItemContainer Container => Rack;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            var equipment = services.Equipment;
            _create = _kind == RackKind.Trays
                ? () => new Tray(equipment.TrayCapacity, equipment.TrayFreshnessFactor)
                : (System.Func<Item>)(() => new FoodContainer(equipment.ContainerCapacity, equipment.ContainerFreshnessFactor));
            Rack = _kind == RackKind.Trays
                ? new ItemRack(equipment.TrayCount, _create, item => item is Tray tray && tray.IsEmpty)
                : new ItemRack(equipment.ContainerCount, _create, item => item is FoodContainer container && container.IsEmpty);
            Rack.Returned += HandleReturned;
        }

        public int SavedCount => Rack.Count;

        // The rest are wherever they were left, saved with what's in them.
        public void RestoreCount(int count)
        {
            while (Rack.Count > count)
                Rack.Take();
            while (Rack.Count < count)
                Rack.Put(_create());
        }

        private void OnDestroy()
        {
            if (Rack != null)
                Rack.Returned -= HandleReturned;
        }

        private void HandleReturned(Item item) => _services.ItemViews.Release(item);

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            var name = _kind == RackKind.Trays ? Tr.Get("tray") : Tr.Get("container");
            var held = player.Hands.Current;
            if (held != null)
                lines.Add(new PromptLine(PromptControl.South, Tr.Format("put {what} back", ("what", name)), Rack.CanAccept(held)));
            else
                lines.Add(new PromptLine(PromptControl.South, Tr.Format("take {what} ({count})", ("what", name), ("count", Rack.Count)), Rack.CanTake));
        }
    }
}
