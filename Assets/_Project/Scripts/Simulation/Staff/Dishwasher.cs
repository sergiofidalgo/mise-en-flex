using System;
using System.Collections.Generic;
using Game.Simulation.Items;

namespace Game.Simulation.Staff
{
    public enum DishwasherTask
    {
        Idle,
        /// <summary>Walking to the pass counter at <see cref="Dishwasher.TargetPass"/>.</summary>
        ToPass,
        ToPile,
        ToSink,
        Washing,
        ToStack,
    }

    /// <summary>
    /// What a dishwasher does: carries dirty plates from the pass to the dirty pile, from the pile to
    /// the sink, washes them, and carries clean ones to the stack, one at a time (decision
    /// 2026-11-04). Follows the same rules as a player, so they can be beaten to a plate or help
    /// mid-pile. Gameplay walks them and calls <see cref="Arrived"/>. Order of work: finished plates
    /// first (service may be waiting), then clearing stools, then filling the sink, then washing.
    /// </summary>
    public sealed class Dishwasher : IStaffJob
    {
        private readonly Sink _sink;
        private readonly DirtyPile _pile;
        private readonly PlateStack _stack;
        private readonly IReadOnlyList<IDirtyPlateSource> _passes;
        private readonly StaffSettings _settings;
        private readonly Random _random;

        // Which side of the pile's journey a walk to the pile is: to drop a plate, or to fetch one.
        private bool _collectingFromPile;

        public Dishwasher(StaffMember member, Sink sink, DirtyPile pile, PlateStack stack, IReadOnlyList<IDirtyPlateSource> passes, StaffSettings settings, int seed)
        {
            Member = member;
            _sink = sink;
            _pile = pile;
            _stack = stack;
            _passes = passes;
            _settings = settings;
            _random = new Random(seed);
        }

        public StaffMember Member { get; }

        public DishwasherTask Task { get; private set; }

        public int TargetPass { get; private set; } = -1;

        /// <summary>The plate in their hands, or null.</summary>
        public Plate Carried { get; private set; }

        Item IStaffJob.Carried => Carried;

        public object Target
        {
            get
            {
                switch (Task)
                {
                    case DishwasherTask.ToPass:
                        return _passes[TargetPass];
                    case DishwasherTask.ToPile:
                        return _pile;
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

        // Plates going on the stack, the pile or in the sink have their views handled there.
        public event Action<Item> ItemGone { add { } remove { } }

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
                case DishwasherTask.ToPass:
                    var pass = _passes[TargetPass];
                    // Someone may have got there first.
                    if (pass.HasDirtyPlate)
                    {
                        Carried = pass.TakeDirtyPlate();
                        Task = DishwasherTask.ToPile;
                    }
                    else
                    {
                        Task = DishwasherTask.Idle;
                    }
                    break;

                case DishwasherTask.ToPile:
                    ArrivedAtPile();
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
            }
        }

        /// <summary>They've been let go: what they're carrying goes where it belongs.</summary>
        public void PutDownCarried()
        {
            if (Carried == null)
                return;
            if (!Carried.IsDirty)
                _stack.Put(Carried);
            else if (_pile.CanAccept(Carried))
                _pile.Put(Carried);
            else if (_sink.CanAccept(Carried))
                _sink.Put(Carried);
            else
                _pile.PutOverLimit(Carried);
            Carried = null;
            Task = DishwasherTask.Idle;
        }

        private void ArrivedAtPile()
        {
            if (Carried != null)
            {
                if (_pile.CanAccept(Carried))
                {
                    _pile.Put(Carried);
                    Carried = null;
                    Task = DishwasherTask.Idle;
                }
                else
                {
                    // The pile filled up on the way: straight to the sink.
                    Task = DishwasherTask.ToSink;
                }
                return;
            }

            if (_collectingFromPile && _pile.CanTake)
            {
                Carried = (Plate)_pile.Take();
                _collectingFromPile = false;
                Task = DishwasherTask.ToSink;
                return;
            }

            _collectingFromPile = false;
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
                    Task = DishwasherTask.Idle;
                }
                else
                {
                    // Nowhere to put it: they get on with what's in the sink to make room.
                    Task = DishwasherTask.Washing;
                }
                return;
            }

            if (_sink.CanTake)
            {
                Carried = (Plate)_sink.Take();
                Task = DishwasherTask.ToStack;
            }
            else
            {
                Task = _sink.DirtyCount > 0 ? DishwasherTask.Washing : DishwasherTask.Idle;
            }
        }

        private void Decide()
        {
            if (Carried != null)
            {
                Task = Carried.IsDirty ? DishwasherTask.ToPile : DishwasherTask.ToStack;
                return;
            }

            if (_sink.CanTake)
            {
                Task = DishwasherTask.ToSink;
                return;
            }

            if (_pile.Count < _pile.Capacity)
            {
                for (var i = 0; i < _passes.Count; i++)
                {
                    if (_passes[i].HasDirtyPlate)
                    {
                        TargetPass = i;
                        Task = DishwasherTask.ToPass;
                        return;
                    }
                }
            }

            if (_pile.CanTake && _sink.HasRoom)
            {
                _collectingFromPile = true;
                Task = DishwasherTask.ToPile;
                return;
            }

            if (_sink.DirtyCount > 0)
                Task = DishwasherTask.ToSink;
        }

        private void Wash(float seconds)
        {
            if (_sink.DirtyCount == 0)
            {
                Task = DishwasherTask.Idle;
                return;
            }

            var pace = Member.WorkingSpeed(_settings) / Math.Max(1f, _settings.WashSecondsPerPlate);
            if (_sink.Wash(seconds * pace) == null)
                return;

            var mistake = _random.NextDouble() < Member.MistakeChance(_settings);
            if (mistake)
                _sink.BreakClean();
            Member.RecordJob(_settings.FatiguePerPlate, mistake);
            Task = DishwasherTask.Idle;
        }
    }
}
