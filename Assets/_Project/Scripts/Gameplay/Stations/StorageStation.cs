using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Storage;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A fridge or shelf with four slots, one per face button. Holding RT opens its radial menu;
    /// while it's open, a face button takes one portion from that slot (empty hands). With food in
    /// hand, Interact puts it back into the slot for that ingredient, no menu needed, and a crate
    /// empties into it. The storeroom is one of these that hands out crates and takes deliveries
    /// (buying and placing units come in Phases 4–5).
    /// </summary>
    public sealed class StorageStation : Interactable, IFaceButtonTarget, IStationPrompt, ISlotMenuStation
    {
        [Serializable]
        public sealed class SlotSetup
        {
            [Tooltip("Ingredient type id, or empty for an unused slot.")]
            [SerializeField] private string _typeId;
            [SerializeField, Min(0)] private int _capacity;
            [Tooltip("Portions in the slot when the scene starts.")]
            [SerializeField, Min(0)] private int _startStock;
            [Tooltip("Portions delivered each morning (the storeroom), up to the capacity.")]
            [SerializeField, Min(0)] private int _dailyDelivery;
            [Tooltip("Where this slot's bin is; food appears here when taken.")]
            [SerializeField] private Transform _bin;

            public string TypeId => _typeId;
            public int Capacity => _capacity;
            public int StartStock => _startStock;
            public int DailyDelivery => _dailyDelivery;
            public Transform Bin => _bin;
        }

        [Tooltip("Fridges keep stock at the fridge temperature; shelves at kitchen temperature.")]
        [SerializeField] private bool _refrigerated;

        [Tooltip("Hands out crates instead of single portions: the storeroom.")]
        [SerializeField] private bool _givesCrates;

        [Tooltip("One per face button, in FaceButton order: South (A), East (B), West (X), North (Y).")]
        [SerializeField] private SlotSetup[] _slots = new SlotSetup[4];

        private StorageUnit _unit;
        private StoreBack _container;
        private KitchenServices _services;

        public StorageUnit Unit => _unit;

        public bool IsRefrigerated => _refrigerated;

        public override IItemContainer Container => _container;

        public bool PromptAsDiamond(PlayerInteractor player) => player.IsSlotMenuOpen;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            var temperature = _refrigerated ? services.Tuning.FridgeTemperature : services.Tuning.AmbientTemperature;
            var slots = new StorageUnit.Slot[_slots.Length];
            for (var i = 0; i < _slots.Length; i++)
                slots[i] = new StorageUnit.Slot(_slots[i]?.TypeId, _slots[i]?.Capacity ?? 0);

            _unit = new StorageUnit(temperature, slots);
            for (var i = 0; i < _slots.Length; i++)
            {
                var setup = _slots[i];
                if (setup == null || string.IsNullOrEmpty(setup.TypeId))
                    continue;
                _unit.Stock(i, () => (Ingredient)services.CreateItem(setup.TypeId, temperature), setup.StartStock);
            }

            _container = new StoreBack(_unit, services);
        }

        /// <summary>The morning delivery, up to each slot's capacity.</summary>
        /// <returns>What arrived, as (type id, portions).</returns>
        public List<(string TypeId, int Count)> TakeDelivery()
        {
            var delivered = new List<(string, int)>();
            for (var i = 0; i < _slots.Length; i++)
            {
                var setup = _slots[i];
                if (setup == null || string.IsNullOrEmpty(setup.TypeId) || setup.DailyDelivery <= 0)
                    continue;
                var count = _unit.Stock(i, () => (Ingredient)_services.CreateItem(setup.TypeId, _unit.Temperature), setup.DailyDelivery);
                if (count > 0)
                    delivered.Add((setup.TypeId, count));
            }
            return delivered;
        }

        public Transform BinAnchor(int slotIndex) =>
            slotIndex >= 0 && slotIndex < _slots.Length && _slots[slotIndex]?.Bin != null ? _slots[slotIndex].Bin : ItemAnchor;

        public bool TryPress(FaceButton button, PlayerInteractor player)
        {
            var index = (int)button;
            if (_unit == null || !player.IsSlotMenuOpen || !player.Hands.IsEmpty || !_unit.TryTake(index, out var ingredient))
                return false;

            if (!_givesCrates)
            {
                player.ReceiveItem(ingredient, BinAnchor(index).position);
                return true;
            }

            // A crate of the oldest portions in this slot.
            var crate = new Crate(_services.Equipment.CrateCapacity);
            crate.Put(ingredient);
            while (crate.Items.Count < crate.Capacity && _unit.TryTake(index, out ingredient))
                crate.Put(ingredient);
            player.ReceiveItem(crate, BinAnchor(index).position);
            return true;
        }

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            if (_unit == null)
                return;

            if (!player.IsSlotMenuOpen)
            {
                lines.Add(new PromptLine(PromptControl.Triggers, "open", player.Hands.IsEmpty));
                var held = player.Hands.Current;
                if (held is Crate crate)
                    lines.Add(new PromptLine(PromptControl.South, "restock", !crate.IsEmpty && _unit.CanAccept(crate.Items[crate.Items.Count - 1])));
                else if (held != null)
                    lines.Add(new PromptLine(PromptControl.South, "put back", _unit.CanAccept(held)));
                return;
            }

            for (var i = 0; i < _unit.Slots.Count; i++)
            {
                var slot = _unit.Slots[i];
                var control = (PromptControl)i;
                lines.Add(slot.IsUsed
                    ? new PromptLine(control, $"{(_givesCrates ? "crate: " : string.Empty)}{slot.TypeId} ×{slot.Count}", slot.Count > 0 && player.Hands.IsEmpty)
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
