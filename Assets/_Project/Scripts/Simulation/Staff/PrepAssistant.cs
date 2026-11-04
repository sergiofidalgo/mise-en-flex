using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Prep;
using Game.Simulation.Storage;

namespace Game.Simulation.Staff
{
    public enum PrepTask
    {
        Idle,
        ToRack,
        ToStorage,
        ToBoard,
        WaitingForBoard,
        Cutting,
        ToContainer,
    }

    /// <summary>
    /// What a prep assistant does (decision 2026-11-04): keeps the containers the players set out
    /// stocked. A container with some food in it says what it's for; when it runs low they bring
    /// more from storage by tray, cutting at the board if what's stored is still whole. Empty
    /// containers are left alone, because only players decide what a container is for. They follow
    /// the player's rules: one tray, one board, and they can be beaten to either.
    /// </summary>
    public sealed class PrepAssistant : IStaffJob
    {
        private readonly IReadOnlyList<ItemSlot> _counters;
        private readonly IReadOnlyList<StorageUnit> _storage;
        private readonly PrepCounter _board;
        private readonly ItemRack _trays;
        private readonly CookingSimulation _cooking;
        private readonly StaffSettings _settings;
        private readonly Random _random;

        private string _wanted;
        private bool _returning;
        private bool _fetchingWhole;
        private Ingredient _cutting;
        private float _cutLeft;
        private float _waited;

        public PrepAssistant(
            StaffMember member,
            IReadOnlyList<ItemSlot> counters,
            IReadOnlyList<StorageUnit> storage,
            PrepCounter board,
            ItemRack trays,
            CookingSimulation cooking,
            StaffSettings settings,
            int seed)
        {
            Member = member;
            _counters = counters;
            _storage = storage;
            _board = board;
            _trays = trays;
            _cooking = cooking;
            _settings = settings;
            _random = new Random(seed);
        }

        public StaffMember Member { get; }

        public PrepTask Task { get; private set; }

        /// <summary>Their tray, kept for the shift once they've taken one from the rack.</summary>
        public Tray Tray { get; private set; }

        public ItemSlot TargetCounter { get; private set; }

        public StorageUnit TargetUnit { get; private set; }

        public Item Carried => Tray;

        public object Target
        {
            get
            {
                switch (Task)
                {
                    case PrepTask.ToRack:
                        return _trays;
                    case PrepTask.ToStorage:
                        return TargetUnit;
                    case PrepTask.ToBoard:
                    case PrepTask.WaitingForBoard:
                    case PrepTask.Cutting:
                        return _board;
                    case PrepTask.ToContainer:
                        return TargetCounter;
                    default:
                        return null;
                }
            }
        }

        public event Action<Item> ItemGone;

        public event Action FoodChanged;

        public void Tick(float seconds)
        {
            switch (Task)
            {
                case PrepTask.Idle:
                    Decide();
                    break;
                case PrepTask.WaitingForBoard:
                    Wait(seconds);
                    break;
                case PrepTask.Cutting:
                    Cut(seconds);
                    break;
            }
        }

        public void Arrived()
        {
            switch (Task)
            {
                case PrepTask.ToRack:
                    Tray = _trays.CanTake ? (Tray)_trays.Take() : null;
                    Task = PrepTask.Idle;
                    break;
                case PrepTask.ToStorage:
                    ArrivedAtStorage();
                    break;
                case PrepTask.ToBoard:
                    _waited = 0f;
                    Task = PrepTask.WaitingForBoard;
                    TryStartCut();
                    break;
                case PrepTask.ToContainer:
                    ArrivedAtContainer();
                    break;
            }
        }

        public void Release()
        {
            if (Tray == null)
                return;

            // What's on the tray goes back to storage; anything that doesn't fit is lost.
            for (var i = Tray.Items.Count - 1; i >= 0; i--)
            {
                var item = Tray.TakeAt(i);
                var unit = FindRoomFor(item);
                if (unit != null)
                {
                    unit.Put(item);
                }
                else if (item is Ingredient ingredient)
                {
                    _cooking.Remove(ingredient);
                }
                ItemGone?.Invoke(item);
            }

            _trays.Put(Tray);
            Tray = null;
            Task = PrepTask.Idle;
        }

        private void Decide()
        {
            if (Tray == null)
            {
                if (_trays.CanTake && LowContainersByFill().Count > 0)
                    Task = PrepTask.ToRack;
                return;
            }

            if (!Tray.IsEmpty)
            {
                PutAwayLeftovers();
                return;
            }

            // The emptiest container first, skipping any they can't do anything for.
            foreach (var counter in LowContainersByFill())
            {
                var container = (FoodContainer)counter.Current;
                if (TryStartFetch(container.HoldsTypeId, counter))
                    return;
            }
        }

        private bool TryStartFetch(string typeId, ItemSlot counter)
        {
            var unit = FindStock(typeId);
            _fetchingWhole = false;
            if (unit == null)
            {
                // No point fetching something to cut while the board is in use.
                if (_board.Current != null)
                    return false;
                unit = FindWholeFor(typeId);
                _fetchingWhole = true;
            }
            if (unit == null)
                return false;

            _wanted = typeId;
            _returning = false;
            TargetCounter = counter;
            TargetUnit = unit;
            Task = PrepTask.ToStorage;
            return true;
        }

        private void PutAwayLeftovers()
        {
            var item = Tray.Items[Tray.Items.Count - 1];

            // Back where it was going if there's room, otherwise back to storage.
            foreach (var counter in _counters)
            {
                if (counter.Current is FoodContainer container && !container.IsEmpty && container.CanAccept(item))
                {
                    TargetCounter = counter;
                    Task = PrepTask.ToContainer;
                    return;
                }
            }

            TargetUnit = FindRoomFor(item);
            if (TargetUnit == null)
            {
                // Nowhere for it at all: it's lost rather than carried forever.
                Tray.TakeAt(Tray.Items.Count - 1);
                if (item is Ingredient ingredient)
                    _cooking.Remove(ingredient);
                ItemGone?.Invoke(item);
                return;
            }
            _returning = true;
            Task = PrepTask.ToStorage;
        }

        private void ArrivedAtStorage()
        {
            if (_returning)
            {
                for (var i = Tray.Items.Count - 1; i >= 0; i--)
                {
                    if (!TargetUnit.CanAccept(Tray.Items[i]))
                        continue;
                    var item = Tray.TakeAt(i);
                    TargetUnit.Put(item);
                    ItemGone?.Invoke(item);
                }
                _returning = false;
                Task = PrepTask.Idle;
                return;
            }

            if (_fetchingWhole)
            {
                for (var s = 0; s < TargetUnit.Slots.Count; s++)
                {
                    var slot = TargetUnit.Slots[s];
                    if (slot.Count > 0 && slot.Stock[0].Properties.CutIntoTypeId == _wanted && TargetUnit.TryTake(s, out var whole))
                    {
                        Tray.Put(whole);
                        _cutting = whole;
                        Task = PrepTask.ToBoard;
                        return;
                    }
                }
                Task = PrepTask.Idle;
                return;
            }

            var room = TargetCounter.Current is FoodContainer container ? container.Capacity - container.Items.Count : 0;
            for (var s = 0; s < TargetUnit.Slots.Count; s++)
            {
                while (room > 0 && Tray.HasRoom && TargetUnit.Slots[s].TypeId == _wanted && TargetUnit.TryTake(s, out var portion))
                {
                    Tray.Put(portion);
                    room--;
                }
            }
            Task = Tray.IsEmpty ? PrepTask.Idle : PrepTask.ToContainer;
        }

        private void Wait(float seconds)
        {
            if (TryStartCut())
                return;

            // The board stayed busy: put the whole ingredient away and think again.
            _waited += seconds;
            if (_waited >= _settings.BoardPatienceSeconds)
            {
                _cutting = null;
                Task = PrepTask.Idle;
            }
        }

        private bool TryStartCut()
        {
            if (_cutting == null || IndexOnTray(_cutting) < 0)
            {
                Task = PrepTask.Idle;
                return true;
            }
            if (!_board.CanAccept(_cutting))
                return false;

            Tray.TakeAt(IndexOnTray(_cutting));
            _board.Put(_cutting);
            _cutLeft = _settings.CutSeconds;
            Task = PrepTask.Cutting;
            FoodChanged?.Invoke();
            return true;
        }

        private void Cut(float seconds)
        {
            // A chef took the food off the board or finished the cut.
            if (_board.Current != _cutting)
            {
                _cutting = null;
                Task = PrepTask.Idle;
                return;
            }

            _cutLeft -= seconds * Member.WorkingSpeed(_settings);
            if (_cutLeft > 0f)
                return;

            // Skill and tiredness decide how even it comes out.
            var slip = (float)_random.NextDouble() * Member.MistakeChance(_settings);
            var product = _board.CutWithoutDial(Math.Clamp(1f - slip, 0f, 1f));
            _board.Take();
            Tray.Put(product);
            _cutting = null;
            Member.RecordJob(_settings.FatiguePerJob, mistake: false);
            Task = PrepTask.ToContainer;
            FoodChanged?.Invoke();
        }

        private void ArrivedAtContainer()
        {
            if (TargetCounter.Current is FoodContainer container)
            {
                for (var i = Tray.Items.Count - 1; i >= 0; i--)
                {
                    if (!container.CanAccept(Tray.Items[i]))
                        continue;

                    var item = Tray.TakeAt(i);
                    var dropped = _random.NextDouble() < Member.MistakeChance(_settings);
                    if (dropped)
                    {
                        _cooking.Remove((Ingredient)item);
                        ItemGone?.Invoke(item);
                    }
                    else
                    {
                        container.Put(item);
                    }
                    Member.RecordJob(_settings.FatiguePerJob, dropped);
                }
            }
            Task = PrepTask.Idle;
        }

        private List<ItemSlot> LowContainersByFill()
        {
            var low = new List<ItemSlot>();
            foreach (var counter in _counters)
            {
                if (counter.Current is FoodContainer container && !container.IsEmpty
                    && container.Items.Count < container.Capacity * _settings.RefillBelow)
                {
                    low.Add(counter);
                }
            }
            low.Sort((a, b) => ((FoodContainer)a.Current).Items.Count.CompareTo(((FoodContainer)b.Current).Items.Count));
            return low;
        }

        private StorageUnit FindStock(string typeId)
        {
            foreach (var unit in _storage)
            {
                foreach (var slot in unit.Slots)
                {
                    if (slot.TypeId == typeId)
                        return unit;
                }
            }
            return null;
        }

        private StorageUnit FindWholeFor(string cutTypeId)
        {
            foreach (var unit in _storage)
            {
                foreach (var slot in unit.Slots)
                {
                    if (slot.Count > 0 && slot.Stock[0].Properties.CutIntoTypeId == cutTypeId)
                        return unit;
                }
            }
            return null;
        }

        private StorageUnit FindRoomFor(Item item)
        {
            foreach (var unit in _storage)
            {
                if (unit.CanAccept(item))
                    return unit;
            }
            return null;
        }

        private int IndexOnTray(Item item)
        {
            for (var i = 0; i < Tray.Items.Count; i++)
            {
                if (Tray.Items[i] == item)
                    return i;
            }
            return -1;
        }
    }
}
