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
        ToSink,
        Washing,
        ToStack,
    }

    /// <summary>
    /// What a dishwasher does: carries dirty plates from the pass to the sink one at a time, washes
    /// them, and carries clean ones back to the stack. Follows the same rules as a player, so they
    /// can be beaten to a plate or help mid-pile. Gameplay walks them and calls <see cref="Arrived"/>.
    /// Order of work: finished plates first (service may be waiting), then clearing stools, then washing.
    /// </summary>
    public sealed class Dishwasher : IStaffJob
    {
        private readonly Sink _sink;
        private readonly PlateStack _stack;
        private readonly IReadOnlyList<IDirtyPlateSource> _passes;
        private readonly StaffSettings _settings;
        private readonly Random _random;

        public Dishwasher(StaffMember member, Sink sink, PlateStack stack, IReadOnlyList<IDirtyPlateSource> passes, StaffSettings settings, int seed)
        {
            Member = member;
            _sink = sink;
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

        // Plates going on the stack or in the sink have their views handled there.
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
                        Task = DishwasherTask.ToSink;
                    }
                    else
                    {
                        Task = DishwasherTask.Idle;
                    }
                    break;

                case DishwasherTask.ToSink:
                    if (Carried != null)
                    {
                        _sink.Put(Carried);
                        Carried = null;
                        Task = DishwasherTask.Idle;
                    }
                    else if (_sink.CanTake)
                    {
                        Carried = (Plate)_sink.Take();
                        Task = DishwasherTask.ToStack;
                    }
                    else
                    {
                        Task = _sink.DirtyCount > 0 ? DishwasherTask.Washing : DishwasherTask.Idle;
                    }
                    break;

                case DishwasherTask.ToStack:
                    _stack.Put(Carried);
                    Carried = null;
                    Task = DishwasherTask.Idle;
                    break;
            }
        }

        /// <summary>They've been let go: what they're carrying goes where it belongs.</summary>
        public void PutDownCarried()
        {
            if (Carried == null)
                return;
            if (Carried.IsDirty)
                _sink.Put(Carried);
            else
                _stack.Put(Carried);
            Carried = null;
            Task = DishwasherTask.Idle;
        }

        private void Decide()
        {
            if (_sink.CanTake)
            {
                Task = DishwasherTask.ToSink;
                return;
            }

            for (var i = 0; i < _passes.Count; i++)
            {
                if (_passes[i].HasDirtyPlate)
                {
                    TargetPass = i;
                    Task = DishwasherTask.ToPass;
                    return;
                }
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
