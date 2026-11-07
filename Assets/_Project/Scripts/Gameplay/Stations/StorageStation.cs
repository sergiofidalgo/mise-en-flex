using System;
using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Storage;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A fridge or shelf with four slots, one per face button, in two tiers seen from the front. Holding RT opens its radial menu;
    /// while it's open, a face button takes one portion from that slot (empty hands). With food in
    /// hand, Interact puts it in a slot with that ingredient or a free one, no menu needed: anything can go anywhere, though meat on a shelf spoils fast. With a tray,
    /// the menu loads onto the tray. Purchases arrive straight here (decision 2026-11-04): free
    /// each morning until shopping arrives in Phase 5.
    /// </summary>
    public sealed class StorageStation : Interactable, IFaceButtonTarget, IStationPrompt, ISlotMenuStation
    {
        [Serializable]
        public sealed class SlotSetup
        {
            [SerializeField, Min(0)] private int _capacity;
            [Tooltip("Where this slot's bin is; food appears here when taken.")]
            [SerializeField] private Transform _bin;

            public int Capacity => _capacity;
            public Transform Bin => _bin;
        }

        [Serializable]
        public sealed class Delivery
        {
            [SerializeField] private string _typeId;
            [SerializeField, Min(0)] private int _count;

            public string TypeId => _typeId;
            public int Count => _count;
        }

        [Tooltip("Fridges keep stock at the fridge temperature; shelves at kitchen temperature.")]
        [SerializeField] private bool _refrigerated;

        [Tooltip("One per face button, in FaceButton order: South (A), East (B), West (X), North (Y).")]
        [SerializeField] private SlotSetup[] _slots = new SlotSetup[4];

        [Tooltip("What arrives here each morning, while there's room. Until shopping (Phase 5).")]
        [SerializeField] private Delivery[] _deliveries = Array.Empty<Delivery>();

        private StorageUnit _unit;
        private StoreBack _container;
        private KitchenServices _services;

        public StorageUnit Unit => _unit;

        public bool IsRefrigerated => _refrigerated;

        public override bool IsClear => _unit != null && _unit.Slots.All(slot => slot.Count == 0);

        /// <summary>This one's being sold: what arrives here each morning arrives there instead.</summary>
        public void PassDeliveriesTo(StorageStation other)
        {
            other._deliveries = other._deliveries.Concat(_deliveries).ToArray();
            _deliveries = Array.Empty<Delivery>();
        }

        public override IItemContainer Container => _container;

        public bool PromptAsDiamond(PlayerInteractor player) => player.IsSlotMenuOpen;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            var temperature = _refrigerated ? services.Tuning.FridgeTemperature : services.Tuning.AmbientTemperature;
            var slots = new StorageUnit.Slot[_slots.Length];
            for (var i = 0; i < _slots.Length; i++)
                slots[i] = new StorageUnit.Slot(_slots[i]?.Capacity ?? 0);

            _unit = new StorageUnit(temperature, slots);
            _container = new StoreBack(_unit, services);
        }

        /// <summary>The morning delivery, up to each slot's capacity.</summary>
        /// <returns>What arrived, as (type id, portions).</returns>
        public List<(string TypeId, int Count)> TakeDelivery()
        {
            var delivered = new List<(string, int)>();
            foreach (var delivery in _deliveries)
            {
                var count = _unit.Stock(delivery.TypeId, () => (Ingredient)_services.CreateItem(delivery.TypeId, _unit.Temperature), delivery.Count);
                if (count > 0)
                    delivered.Add((delivery.TypeId, count));
            }
            return delivered;
        }

        public Transform BinAnchor(int slotIndex) =>
            slotIndex >= 0 && slotIndex < _slots.Length && _slots[slotIndex]?.Bin != null ? _slots[slotIndex].Bin : ItemAnchor;

        public bool TryPress(FaceButton button, PlayerInteractor player)
        {
            var index = (int)button;
            if (_unit == null || !player.IsSlotMenuOpen)
                return false;

            // Onto a held tray, or into empty hands.
            var tray = player.Hands.Current as Tray;
            if (tray != null ? !tray.HasRoom : !player.Hands.IsEmpty)
                return false;
            if (!_unit.TryTake(index, out var ingredient))
                return false;

            if (tray != null)
            {
                tray.Put(ingredient);
                player.RefreshHeld(BinAnchor(index).position);
            }
            else
            {
                player.ReceiveItem(ingredient, BinAnchor(index).position);
            }
            return true;
        }

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            if (_unit == null)
                return;

            if (!player.IsSlotMenuOpen)
            {
                var held = player.Hands.Current;
                lines.Add(new PromptLine(PromptControl.Triggers, "open", held == null || (held is Tray openTray && openTray.HasRoom)));
                if (held is Tray tray)
                    lines.Add(new PromptLine(PromptControl.South, "put back", tray.Items.Any(_unit.CanAccept)));
                else if (held != null)
                    lines.Add(new PromptLine(PromptControl.South, "put back", _unit.CanAccept(held)));
                return;
            }

            for (var i = 0; i < _unit.Slots.Count; i++)
            {
                var slot = _unit.Slots[i];
                var control = (PromptControl)i;
                lines.Add(slot.Count > 0
                    ? new PromptLine(control, $"{slot.TypeId} ×{slot.Count}", player.Hands.IsEmpty || (player.Hands.Current is Tray room && room.HasRoom),
                        iconId: slot.TypeId, iconLabel: $"×{slot.Count}")
                    : new PromptLine(control, "—", available: false));
            }
        }

        // Putting food back: the unit decides which slot; the item's view goes away while stored.
        private sealed class StoreBack : IItemContainer
        {
            private readonly StorageUnit _unit;
            private readonly KitchenServices _services;

            public StoreBack(StorageUnit unit, KitchenServices services)
            {
                _unit = unit;
                _services = services;
            }

            public bool CanTake => false;

            public bool CanAccept(Item item) => _unit.CanAccept(item);

            public Item Take() => _unit.Take();

            public void Put(Item item)
            {
                _unit.Put(item);
                _services.ItemViews.Release(item);
            }
        }
    }
}
