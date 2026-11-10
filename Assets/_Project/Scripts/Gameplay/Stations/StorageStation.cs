using Game.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Storage;
using UnityEngine;
using UnityEngine.Serialization;
using Game.Gameplay.Saving;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A fridge or shelf with four slots, one per face button, in two tiers seen from the front. Holding RT opens its radial menu;
    /// while it's open, a face button takes one portion from that slot (empty hands). With food in
    /// hand, Interact puts it in a slot with that ingredient or a free one, no menu needed: anything can go anywhere, though meat on a shelf spoils fast. With a tray,
    /// the menu loads onto the tray. Stock bought at the street's stalls arrives straight here (decision
    /// 2026-11-10), and so does a new campaign's starter stock, once.
    /// </summary>
    public sealed class StorageStation : Interactable, IFaceButtonTarget, IStationPrompt, ISlotMenuStation, ISavedContents
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
        public sealed class StarterStock
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

        [Tooltip("What's here on a new campaign's first morning, while there's room. After that, stock is bought at the street's stalls.")]
        [FormerlySerializedAs("_deliveries")]
        [SerializeField] private StarterStock[] _starterStock = Array.Empty<StarterStock>();

        private StorageUnit _unit;
        private StoreBack _container;
        private KitchenServices _services;

        public StorageUnit Unit => _unit;

        /// <summary>Raised when stock bought at a stall arrives here, with how many portions: for the feedback.</summary>
        public event Action<int> Stocked;

        public void NotifyStocked(int portions) => Stocked?.Invoke(portions);

        public bool IsRefrigerated => _refrigerated;

        public override bool IsClear => _unit != null && _unit.Slots.All(slot => slot.Count == 0);

        /// <summary>This one's being sold before the first morning: its starter stock goes there instead.</summary>
        public void PassStarterStockTo(StorageStation other)
        {
            other._starterStock = other._starterStock.Concat(_starterStock).ToArray();
            _starterStock = Array.Empty<StarterStock>();
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
            _container = new StoreBack(this);
        }

        public IEnumerable<(int Slot, Item Item)> SavedItems
        {
            get
            {
                for (var i = 0; i < _unit.Slots.Count; i++)
                {
                    foreach (var ingredient in _unit.Slots[i].Stock)
                        yield return (i, ingredient);
                }
            }
        }

        public void ClearForLoad()
        {
        }

        // Stock finds its own slot by type, oldest first, as it was saved.
        public bool RestoreItem(int slot, Item item)
        {
            if (!_unit.CanAccept(item))
                return false;
            _unit.Put(item);
            return true;
        }

        /// <summary>A new campaign's starter stock, up to each slot's capacity. Free, and only once.</summary>
        /// <returns>What was put in, as (type id, portions).</returns>
        public List<(string TypeId, int Count)> StockStarter()
        {
            var stocked = new List<(string, int)>();
            foreach (var stock in _starterStock)
            {
                var count = _unit.Stock(stock.TypeId, () => (Ingredient)_services.CreateItem(stock.TypeId, _unit.Temperature), stock.Count);
                if (count > 0)
                    stocked.Add((stock.TypeId, count));
            }
            return stocked;
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
                lines.Add(new PromptLine(PromptControl.Triggers, Tr.Get("open"), held == null || (held is Tray openTray && openTray.HasRoom)));
                if (held is Tray tray)
                    lines.Add(new PromptLine(PromptControl.South, Tr.Get("put back"), tray.Items.Any(_unit.CanAccept)));
                else if (held != null)
                    lines.Add(new PromptLine(PromptControl.South, Tr.Get("put back"), _unit.CanAccept(held)));
                return;
            }

            for (var i = 0; i < _unit.Slots.Count; i++)
            {
                var slot = _unit.Slots[i];
                var control = (PromptControl)i;
                lines.Add(slot.Count > 0
                    ? new PromptLine(control, (slot.HoldsCooked ? Tr.Format("cooked {what}", ("what", Tr.Get(slot.TypeId))) : Tr.Get(slot.TypeId)) + $" ×{slot.Count}", player.Hands.IsEmpty || (player.Hands.Current is Tray room && room.HasRoom),
                        iconId: slot.TypeId, iconLabel: $"×{slot.Count}")
                    : new PromptLine(control, "—", available: false));
            }
        }

        // Putting food back: the unit decides which slot. Raw stock is shown by the bin's sample
        // shape, so its own view goes away; cooked food has no sample, so its real view stays in
        // the bin, showing exactly what was put there.
        private sealed class StoreBack : IItemContainer
        {
            private readonly StorageStation _station;

            public StoreBack(StorageStation station)
            {
                _station = station;
            }

            public bool CanTake => false;

            public bool CanAccept(Item item) => _station._unit.CanAccept(item);

            public Item Take() => _station._unit.Take();

            public void Put(Item item)
            {
                var unit = _station._unit;
                var services = _station._services;
                unit.Put(item);
                var ingredient = (Ingredient)item;
                if (ingredient.HasBeenCooked)
                    services.ItemViews.Place(item, _station.BinAnchor(unit.SlotOf(ingredient)), services.Tuning.ItemMove, _station.ItemAnchor.position);
                else
                    services.ItemViews.Release(item);
            }
        }
    }
}
