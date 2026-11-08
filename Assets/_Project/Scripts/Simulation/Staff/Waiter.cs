using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Service;

namespace Game.Simulation.Staff
{
    public enum WaiterTask
    {
        Idle,
        ToPlate,
        ToDiner,
        ToCounter,
        ToTable,
        /// <summary>Taking a cleared dirty plate to a counter, to pile it up there.</summary>
        ToDirtyCounter,
    }

    /// <summary>
    /// Takes food out and clears up (decisions 2026-11-04 and 06). When the bell's been rung for a plate
    /// they carry it to the diner whose order it matches, one plate at a time; a plate nobody ordered
    /// stays where it is. With nothing to serve, they clear dirty plates and pile them on a kitchen
    /// counter, up to its limit (decision 2026-11-08). Follows the
    /// same rules as a player, so they can be beaten to a plate. Gameplay walks them and calls
    /// <see cref="Arrived"/>.
    /// </summary>
    public sealed class Waiter : IStaffJob
    {
        private readonly ReadyBell _bell;
        private readonly IReadOnlyList<IServingPlace> _places;
        private readonly IReadOnlyList<ItemSlot> _counters;
        private readonly Sink _sink;
        private readonly Func<ServiceSimulation> _service;
        private readonly CookingSimulation _cooking;
        private readonly StaffSettings _settings;
        private readonly Random _random;

        private ItemSlot _counter;
        private IServingPlace _place;

        public Waiter(
            StaffMember member,
            ReadyBell bell,
            IReadOnlyList<IServingPlace> places,
            IReadOnlyList<ItemSlot> counters,
            Sink sink,
            Func<ServiceSimulation> service,
            CookingSimulation cooking,
            StaffSettings settings,
            int seed)
        {
            Member = member;
            _bell = bell;
            _places = places;
            _counters = counters;
            _sink = sink;
            _service = service;
            _cooking = cooking;
            _settings = settings;
            _random = new Random(seed);
        }

        public StaffMember Member { get; }

        public WaiterTask Task { get; private set; }

        public Plate Carried { get; private set; }

        Item IStaffJob.Carried => Carried;

        public object Target
        {
            get
            {
                switch (Task)
                {
                    case WaiterTask.ToPlate:
                    case WaiterTask.ToCounter:
                    case WaiterTask.ToDirtyCounter:
                        return _counter;
                    case WaiterTask.ToDiner:
                    case WaiterTask.ToTable:
                        return _place.Slot;
                    default:
                        return null;
                }
            }
        }

        public event Action<Item> ItemGone;

        public event Action FoodChanged { add { } remove { } }

        public void Tick(float seconds)
        {
            if (Task == WaiterTask.Idle)
                Decide();
        }

        public void Arrived()
        {
            switch (Task)
            {
                case WaiterTask.ToPlate:
                    PickUpPlate();
                    break;
                case WaiterTask.ToDiner:
                    Deliver();
                    break;
                case WaiterTask.ToCounter:
                    if (_counter.CanAccept(Carried))
                    {
                        _counter.Put(Carried);
                        _bell.Ring(_counter);
                        Carried = null;
                    }
                    Task = WaiterTask.Idle;
                    break;
                case WaiterTask.ToTable:
                    // Someone may have got there first.
                    if (_place.Slot.HasDirtyPlate)
                    {
                        Carried = _place.Slot.TakeDirtyPlate();
                        Task = WaiterTask.Idle; // Decide picks a counter for it
                    }
                    else
                    {
                        Task = WaiterTask.Idle;
                    }
                    break;
                case WaiterTask.ToDirtyCounter:
                    // Someone may have filled it on the way: Decide picks another.
                    if (_counter.CanAccept(Carried))
                    {
                        _counter.Put(Carried);
                        Carried = null;
                        Member.RecordJob(_settings.FatiguePerJob, false);
                    }
                    Task = WaiterTask.Idle;
                    break;
            }
        }

        /// <summary>They've been let go: what they're carrying goes where it belongs.</summary>
        public void Release()
        {
            if (Carried == null)
                return;

            if (Carried.IsDirty)
            {
                var counter = CounterForDirtyPlate();
                if (counter != null)
                {
                    counter.Put(Carried);
                }
                else
                {
                    // Nowhere for it: it's dropped and breaks.
                    LoseCarried();
                    _sink.CountBroken();
                }
            }
            else
            {
                var counter = FreeCounter();
                if (counter != null)
                    counter.Put(Carried);
                else
                    LoseCarried();
            }
            Carried = null;
            Task = WaiterTask.Idle;
        }

        private void Decide()
        {
            if (Carried != null)
            {
                if (Carried.IsDirty)
                {
                    // With every counter full, they hold it until one frees up.
                    _counter = CounterForDirtyPlate();
                    if (_counter != null)
                        Task = WaiterTask.ToDirtyCounter;
                    return;
                }
                _place = DinerFor(Carried);
                if (_place != null)
                {
                    Task = WaiterTask.ToDiner;
                    return;
                }

                // Whoever it was for has gone: it goes back on a counter, if there's one free.
                _counter = FreeCounter();
                if (_counter != null)
                    Task = WaiterTask.ToCounter;
                return;
            }

            foreach (var counter in _bell.Waiting)
            {
                _place = DinerFor((Plate)counter.Current);
                if (_place == null)
                    continue;
                _counter = counter;
                Task = WaiterTask.ToPlate;
                return;
            }

            // Clearing only if there's somewhere to pile the plate.
            if (CounterForDirtyPlate(anyDirtyPlate: true) == null)
                return;
            foreach (var place in _places)
            {
                if (!place.Slot.HasDirtyPlate)
                    continue;
                _place = place;
                Task = WaiterTask.ToTable;
                return;
            }
        }

        private void PickUpPlate()
        {
            Task = WaiterTask.Idle;

            // Someone may have got there first, or the diner may have gone.
            if (!_bell.IsRung(_counter) || DinerFor((Plate)_counter.Current) == null)
                return;

            Carried = (Plate)_counter.Take();
            if (_random.NextDouble() < Member.MistakeChance(_settings))
            {
                LoseCarried();
                _sink.CountBroken();
                Member.RecordJob(_settings.FatiguePerJob, true);
            }
        }

        private void Deliver()
        {
            Task = WaiterTask.Idle;
            if (!IsFor(_place, Carried))
                return; // Decide finds them someone else, or a counter.

            _place.Slot.Put(Carried);
            Carried = null;
            Member.RecordJob(_settings.FatiguePerJob, false);
        }

        // Only the diner whose order it matches, sitting where a plate can go.
        private IServingPlace DinerFor(Plate plate)
        {
            foreach (var place in _places)
            {
                if (IsFor(place, plate))
                    return place;
            }
            return null;
        }

        private bool IsFor(IServingPlace place, Plate plate)
        {
            if (!place.Slot.CanAccept(plate))
                return false;
            var customer = _service().AtSeat(place.SeatIndex);
            return customer != null && customer.State == CustomerState.Waiting && customer.Order != null
                && OrderCheck.Check(customer.Order, plate, out _) == OrderMatch.Matches;
        }

        // A counter where a dirty plate can go: on top of other dirty plates first, so they gather
        // in a few piles rather than spreading over every counter.
        private ItemSlot CounterForDirtyPlate(bool anyDirtyPlate = false)
        {
            ItemSlot empty = null;
            foreach (var counter in _counters)
            {
                var top = counter.Current as Plate;
                var hasRoom = anyDirtyPlate
                    ? top != null && top.IsDirty && counter.Count < counter.MaxDirtyPlates
                    : counter.CanPileOn(Carried);
                if (hasRoom)
                    return counter;
                if (empty == null && counter.IsEmpty)
                    empty = counter;
            }
            return empty;
        }

        private ItemSlot FreeCounter()
        {
            foreach (var counter in _counters)
            {
                if (counter.IsEmpty)
                    return counter;
            }
            return null;
        }

        // Dropped: the plate breaks and the food's gone.
        private void LoseCarried()
        {
            foreach (var item in Carried.Items)
            {
                if (item is Ingredient ingredient)
                    _cooking.Remove(ingredient);
                ItemGone?.Invoke(item);
            }
            ItemGone?.Invoke(Carried);
            Carried = null;
        }
    }
}
