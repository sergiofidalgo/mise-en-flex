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
    /// hand, Interact puts it back into the slot for that ingredient, no menu needed. Stock refills when the scene starts (restocking during Prep comes in
    /// Phase 3, buying and placing units in Phases 4–5).
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
            [Tooltip("Where this slot's bin is; food appears here when taken.")]
            [SerializeField] private Transform _bin;

            public string TypeId => _typeId;
            public int Capacity => _capacity;
            public int StartStock => _startStock;
            public Transform Bin => _bin;
        }

        [Tooltip("Fridges keep stock at the fridge temperature; shelves at kitchen temperature.")]
        [SerializeField] private bool _refrigerated;

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

        public Transform BinAnchor(int slotIndex) =>
            slotIndex >= 0 && slotIndex < _slots.Length && _slots[slotIndex]?.Bin != null ? _slots[slotIndex].Bin : ItemAnchor;

        public bool TryPress(FaceButton button, PlayerInteractor player)
        {
            var index = (int)button;
            if (_unit == null || !player.IsSlotMenuOpen || !player.Hands.IsEmpty || !_unit.TryTake(index, out var ingredient))
                return false;

            player.ReceiveItem(ingredient, BinAnchor(index).position);
            return true;
        }

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            if (_unit == null)
                return;

            if (!player.IsSlotMenuOpen)
            {
                lines.Add(new PromptLine(PromptControl.Triggers, "open", player.Hands.IsEmpty));
                if (!player.Hands.IsEmpty)
                    lines.Add(new PromptLine(PromptControl.South, "put back", _unit.CanAccept(player.Hands.Current)));
                return;
            }

            for (var i = 0; i < _unit.Slots.Count; i++)
            {
                var slot = _unit.Slots[i];
                var control = (PromptControl)i;
                lines.Add(slot.IsUsed
                    ? new PromptLine(control, $"{slot.TypeId} ×{slot.Count}", slot.Count > 0 && player.Hands.IsEmpty)
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
