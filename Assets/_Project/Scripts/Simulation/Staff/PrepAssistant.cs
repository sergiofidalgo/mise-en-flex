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
        ToBin,
        ReturningTray,
    }

    /// <summary>
    /// What a prep assistant does (decisions 2026-11-04): keeps the containers the players set out
    /// stocked, going by the ingredient in each. They don't restock storage. When a container runs
    /// low, they bring portions from storage, on a tray if the rack has one and by hand otherwise,
    /// and the tray goes back afterwards. If what's stored is still whole, they carry one by hand to
    /// the board and cut it. Empty containers are left alone: only players decide what a container
    /// is for. If food ends up with nowhere to go, whole food goes back to storage and cut food in
    /// the bin. They follow the player's rules, and can be beaten to a board or a container.
    /// </summary>
    public sealed class PrepAssistant : IStaffJob
    {
        private readonly IReadOnlyList<ItemSlot> _counters;
        private readonly IReadOnlyList<StorageUnit> _storage;
        private readonly PrepCounter _board;
        private readonly ItemRack _trays;
        private readonly ItemSink _bin;
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
            ItemSink bin,
            CookingSimulation cooking,
            StaffSettings settings,
            int seed)
        {
            Member = member;
            _counters = counters;
            _storage = storage;
            _board = board;
            _trays = trays;
            _bin = bin;
            _cooking = cooking;
            _settings = settings;
            _random = new Random(seed);
        }

        public StaffMember Member { get; }

        public PrepTask Task { get; private set; }

        /// <summary>The tray they're using for this trip, or null.</summary>
        public Tray Tray { get; private set; }

        /// <summary>Food carried by hand, or null.</summary>
        public Ingredient InHand { get; private set; }

        public ItemSlot TargetCounter { get; private set; }

        public StorageUnit TargetUnit { get; private set; }

        public Item Carried => Tray != null ? Tray : InHand;

        public object Target
        {
            get
            {
                switch (Task)
                {
                    case PrepTask.ToRack:
                    case PrepTask.ReturningTray:
                        return _trays;
                    case PrepTask.ToStorage:
                        return TargetUnit;
                    case PrepTask.ToBoard:
                    case PrepTask.WaitingForBoard:
                    case PrepTask.Cutting:
                        return _board;
                    case PrepTask.ToContainer:
                        return TargetCounter;
                    case PrepTask.ToBin:
                        return _bin;
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
                    Task = PrepTask.ToStorage;
                    break;
                case PrepTask.ReturningTray:
                    _trays.Put(Tray);
                    Tray = null;
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
                case PrepTask.ToBin:
                    ArrivedAtBin();
                    break;
            }
        }

        public void Release()
        {
            while (TryTakeCarriedFood(out var food))
            {
                var unit = IsCut(food) ? null : FindRoomFor(food);
                if (unit != null)
                    unit.Put(food);
                else
                    _cooking.Remove(food);
                ItemGone?.Invoke(food);
            }

            if (Tray != null)
            {
                _trays.Put(Tray);
                Tray = null;
            }
            Task = PrepTask.Idle;
        }

        private void Decide()
        {
            if (HasCarriedFood)
            {
                PutAwayLeftovers();
                return;
            }

            if (Tray != null)
            {
                Task = PrepTask.ReturningTray;
                return;
            }

            // The emptiest container first, skipping any they can't do anything for.
            foreach (var counter in LowContainersByFill())
            {
                if (TryStartFetch(((FoodContainer)counter.Current).HoldsTypeId, counter))
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

            // A tray for several portions, if there's one free. Food to cut goes by hand.
            Task = !_fetchingWhole && _trays.CanTake ? PrepTask.ToRack : PrepTask.ToStorage;
            return true;
        }

        // Food they're holding that isn't going where it was meant to.
        private void PutAwayLeftovers()
        {
            var food = PeekCarriedFood();
            foreach (var counter in _counters)
            {
                if (counter.Current is FoodContainer container && !container.IsEmpty && container.CanAccept(food))
                {
                    TargetCounter = counter;
                    Task = PrepTask.ToContainer;
                    return;
                }
            }

            // Whole food goes back if there's room; food already cut, or with no room, gets binned.
            TargetUnit = IsCut(food) ? null : FindRoomFor(food);
            _returning = TargetUnit != null;
            Task = _returning ? PrepTask.ToStorage : PrepTask.ToBin;
        }

        private void ArrivedAtStorage()
        {
            if (_returning)
            {
                while (TryPeekCarriedFood(out var food) && !IsCut(food) && TargetUnit.CanAccept(food))
                {
                    TryTakeCarriedFood(out food);
                    TargetUnit.Put(food);
                    ItemGone?.Invoke(food);
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
                    if (slot.Count > 0 && !slot.HoldsCooked && slot.Stock[0].Properties.CutIntoTypeId == _wanted && TargetUnit.TryTake(s, out var whole))
                    {
                        InHand = whole;
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
                while (room > 0 && CanCarryMore && TargetUnit.Slots[s].TypeId == _wanted && !TargetUnit.Slots[s].HoldsCooked && TargetUnit.TryTake(s, out var portion))
                {
                    if (Tray != null)
                        Tray.Put(portion);
                    else
                        InHand = portion;
                    room--;
                }
            }
            Task = HasCarriedFood ? PrepTask.ToContainer : PrepTask.Idle;
        }

        private void Wait(float seconds)
        {
            if (TryStartCut())
                return;

            // The board stayed busy: they think again, and the food goes back.
            _waited += seconds;
            if (_waited >= _settings.BoardPatienceSeconds)
            {
                _cutting = null;
                Task = PrepTask.Idle;
            }
        }

        private bool TryStartCut()
        {
            if (_cutting == null || InHand != _cutting)
            {
                Task = PrepTask.Idle;
                return true;
            }
            if (!_board.CanAccept(_cutting))
                return false;

            InHand = null;
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
            _board.CutWithoutDial(Math.Clamp(1f - slip, 0f, 1f));
            InHand = (Ingredient)_board.Take();
            _cutting = null;
            Member.RecordJob(_settings.FatiguePerJob, mistake: false);
            Task = PrepTask.ToContainer;
            FoodChanged?.Invoke();
        }

        private void ArrivedAtContainer()
        {
            if (TargetCounter.Current is FoodContainer container)
            {
                while (TryPeekCarriedFood(out var food) && container.CanAccept(food))
                {
                    TryTakeCarriedFood(out food);
                    var dropped = _random.NextDouble() < Member.MistakeChance(_settings);
                    if (dropped)
                    {
                        _cooking.Remove(food);
                        ItemGone?.Invoke(food);
                    }
                    else
                    {
                        container.Put(food);
                    }
                    Member.RecordJob(_settings.FatiguePerJob, dropped);
                }
            }
            Task = PrepTask.Idle;
        }

        private void ArrivedAtBin()
        {
            // Only what has nowhere else to go: cut food, or food with no room in storage. If the
            // bin is full they keep hold of it and stay by the bin until there's room.
            while (TryPeekCarriedFood(out var food) && (IsCut(food) || FindRoomFor(food) == null) && _bin.CanAccept(food))
            {
                TryTakeCarriedFood(out food);
                _bin.Put(food);
            }
            Task = PrepTask.Idle;
        }

        private static bool IsCut(Ingredient food) => food.CutEvenness.HasValue;

        private bool HasCarriedFood => InHand != null || (Tray != null && !Tray.IsEmpty);

        // With a tray, up to its capacity; by hand, one.
        private bool CanCarryMore => Tray != null ? Tray.HasRoom : InHand == null;

        private Ingredient PeekCarriedFood()
        {
            TryPeekCarriedFood(out var food);
            return food;
        }

        private bool TryPeekCarriedFood(out Ingredient food)
        {
            food = InHand ?? (Tray != null && !Tray.IsEmpty ? (Ingredient)Tray.Items[Tray.Items.Count - 1] : null);
            return food != null;
        }

        private bool TryTakeCarriedFood(out Ingredient food)
        {
            if (InHand != null)
            {
                food = InHand;
                InHand = null;
                return true;
            }
            if (Tray != null && !Tray.IsEmpty)
            {
                food = (Ingredient)Tray.Take();
                return true;
            }
            food = null;
            return false;
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
                    if (slot.TypeId == typeId && !slot.HoldsCooked)
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
                    if (slot.Count > 0 && !slot.HoldsCooked && slot.Stock[0].Properties.CutIntoTypeId == cutTypeId)
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
    }
}
