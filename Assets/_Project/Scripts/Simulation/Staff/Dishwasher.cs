using System;
using System.Collections.Generic;
using Game.Simulation.Items;

namespace Game.Simulation.Staff
{
    public enum DishwasherTask
    {
        Idle,
        /// <summary>Walking to fetch a dirty plate: from a stool's counter or a kitchen counter.</summary>
        ToFetch,
        ToSink,
        Washing,
        ToStack,
        /// <summary>Walking to leave a dirty plate on a counter while the sink's busy.</summary>
        ToCounter,
    }

    /// <summary>
    /// What the cleaner does with plates (decision 2026-11-08): fetches dirty plates from the stools
    /// and the counters they've been piled on, washes them one at a time, and carries each clean one
    /// to the stack. Follows the same rules as a player, so they can be beaten to a plate or wash
    /// alongside. Gameplay walks them and calls <see cref="Arrived"/>. Order of work: a plate already
    /// in the sink, then clearing stools (service may be waiting for the seat), then counters.
    /// </summary>
    public sealed class Dishwasher : IStaffJob
    {
        private readonly Sink _sink;
        private readonly PlateStack _stack;
        private readonly IReadOnlyList<IDirtyPlateSource> _passes;
        private readonly IReadOnlyList<ItemSlot> _counters;
        private readonly StaffSettings _settings;
        private readonly Random _random;

        // Where they're walking. The lists can change on the way, as furniture moves.
        private IDirtyPlateSource _targetSource;
        private ItemSlot _targetCounter;

        public Dishwasher(StaffMember member, Sink sink, PlateStack stack, IReadOnlyList<IDirtyPlateSource> passes,
            IReadOnlyList<ItemSlot> counters, StaffSettings settings, int seed)
        {
            Member = member;
            _sink = sink;
            _stack = stack;
            _passes = passes;
            _counters = counters;
            _settings = settings;
            _random = new Random(seed);
        }

        public StaffMember Member { get; }

        public DishwasherTask Task { get; private set; }

        /// <summary>The plate in their hands, or null.</summary>
        public Plate Carried { get; private set; }

        Item IStaffJob.Carried => Carried;

        public object Target
        {
            get
            {
                switch (Task)
                {
                    case DishwasherTask.ToFetch:
                        return _targetSource ?? (object)_targetCounter;
                    case DishwasherTask.ToCounter:
                        return _targetCounter;
                    case DishwasherTask.ToSink:
                    case DishwasherTask.Washing:
                        return _sink;
                    case DishwasherTask.ToStack:
                        return _stack;
                    default:
                        return null;
                }
            }
        }

        /// <summary>A plate broken in their hands, so its view can go.</summary>
        public event Action<Item> ItemGone;

        public event Action FoodChanged { add { } remove { } }

        public void Release() => PutDownCarried();

        public void Tick(float seconds)
        {
            if (Task == DishwasherTask.Idle)
                Decide();
            if (Task == DishwasherTask.Washing)
                Wash(seconds);
        }

        /// <summary>Gameplay: they've reached where <see cref="Task"/> was taking them.</summary>
        public void Arrived()
        {
            switch (Task)
            {
                case DishwasherTask.ToFetch:
                    // Someone may have got there first.
                    if (_targetSource != null && _targetSource.HasDirtyPlate)
                        Carried = _targetSource.TakeDirtyPlate();
                    else if (_targetSource == null && _targetCounter != null && IsDirtyPlate(_targetCounter.Current))
                        Carried = (Plate)_targetCounter.Take();
                    Task = DishwasherTask.Idle;
                    break;

                case DishwasherTask.ToSink:
                    ArrivedAtSink();
                    break;

                case DishwasherTask.ToStack:
                    // A full stack: they hold the plate and wait for room.
                    if (_stack.CanAccept(Carried))
                    {
                        _stack.Put(Carried);
                        Carried = null;
                        Task = DishwasherTask.Idle;
                    }
                    break;

                case DishwasherTask.ToCounter:
                    if (_targetCounter.CanAccept(Carried))
                    {
                        _targetCounter.Put(Carried);
                        Carried = null;
                    }
                    Task = DishwasherTask.Idle;
                    break;
            }
        }

        /// <summary>They've been let go: what they're carrying goes where it belongs.</summary>
        public void PutDownCarried()
        {
            if (Carried == null)
                return;

            if (!Carried.IsDirty)
            {
                _stack.Put(Carried);
            }
            else if (_sink.CanAccept(Carried))
            {
                _sink.Put(Carried);
            }
            else
            {
                var counter = CounterToLeaveDirtyPlate();
                if (counter != null)
                {
                    counter.Put(Carried);
                }
                else
                {
                    // Nowhere for it: it's dropped and breaks.
                    _sink.CountBroken();
                    ItemGone?.Invoke(Carried);
                }
            }
            Carried = null;
            Task = DishwasherTask.Idle;
        }

        private void ArrivedAtSink()
        {
            if (Carried != null)
            {
                if (_sink.CanAccept(Carried))
                {
                    _sink.Put(Carried);
                    Carried = null;
                    Task = DishwasherTask.Washing;
                }
                else
                {
                    // Someone else's plate is in: Decide finds a counter to leave this one on.
                    Task = DishwasherTask.Idle;
                }
                return;
            }

            Task = _sink.DirtyCount > 0 ? DishwasherTask.Washing : DishwasherTask.Idle;
        }

        private void Decide()
        {
            _targetSource = null;
            _targetCounter = null;

            if (Carried != null)
            {
                if (!Carried.IsDirty)
                {
                    Task = DishwasherTask.ToStack;
                }
                else if (_sink.HasRoom)
                {
                    Task = DishwasherTask.ToSink;
                }
                else
                {
                    _targetCounter = CounterToLeaveDirtyPlate();
                    if (_targetCounter != null)
                        Task = DishwasherTask.ToCounter;
                }
                return;
            }

            if (_sink.DirtyCount > 0)
            {
                Task = DishwasherTask.ToSink;
                return;
            }

            for (var i = 0; i < _passes.Count; i++)
            {
                if (_passes[i].HasDirtyPlate)
                {
                    _targetSource = _passes[i];
                    Task = DishwasherTask.ToFetch;
                    return;
                }
            }

            for (var i = 0; i < _counters.Count; i++)
            {
                if (IsDirtyPlate(_counters[i].Current))
                {
                    _targetCounter = _counters[i];
                    Task = DishwasherTask.ToFetch;
                    return;
                }
            }
        }

        private void Wash(float seconds)
        {
            if (_sink.DirtyCount == 0)
            {
                Task = DishwasherTask.Idle;
                return;
            }

            var pace = Member.WorkingSpeed(_settings) / Math.Max(1f, _settings.WashSecondsPerPlate);
            var plate = _sink.Wash(seconds * pace);
            if (plate == null)
                return;

            var mistake = _random.NextDouble() < Member.MistakeChance(_settings);
            Member.RecordJob(_settings.FatiguePerPlate, mistake);
            if (mistake)
            {
                // Dropped as it came out of the sink.
                _sink.CountBroken();
                ItemGone?.Invoke(plate);
                Task = DishwasherTask.Idle;
                return;
            }

            Carried = plate;
            Task = DishwasherTask.ToStack;
        }

        // A counter where a dirty plate can wait: on top of other dirty plates first, so they
        // gather in a few piles rather than spreading over every counter.
        private ItemSlot CounterToLeaveDirtyPlate()
        {
            ItemSlot empty = null;
            for (var i = 0; i < _counters.Count; i++)
            {
                var counter = _counters[i];
                if (counter.CanPileOn(Carried))
                    return counter;
                if (empty == null && counter.IsEmpty)
                    empty = counter;
            }
            return empty;
        }

        private static bool IsDirtyPlate(Item item) => item is Plate plate && plate.IsDirty;
    }
}
