using System;
using System.Collections.Generic;

namespace Game.Simulation.Cooking
{
    /// <summary>Tuning for <see cref="FireSafety"/>. Times are real seconds.</summary>
    public sealed class FireSettings
    {
        /// <summary>
        /// How long a pan has to stay on a lit flame with its food burnt before it catches fire: the
        /// smoke alarm, which starts well before burnt, is the warning (decision 2026-11-07).
        /// </summary>
        public float IgnitionSeconds { get; set; } = 4f;

        /// <summary>Sweeps of the extinguisher that put a fire out.</summary>
        public float SweepsToPutOut { get; set; } = 3f;
    }

    /// <summary>
    /// Pan fires (decisions 2026-11-07 and 2026-11-10). Only a gas hob catches fire: a pan left on a
    /// lit flame after the food in it has burnt catches a few seconds later. While it burns the hob
    /// and pan can't be used and the food is lost. It burns until it's put out; it doesn't spread,
    /// and it doesn't ruin the hob (appliances have no lifespan for now). Induction and the grill
    /// never catch fire.
    ///
    /// Also says when food first burns in a pan on the heat, on any hob, since that leaves soot.
    /// </summary>
    public sealed class FireSafety
    {
        private sealed class Watch
        {
            public float Smouldering;
            public bool Burning;
            public float PutOut;
        }

        private readonly Dictionary<Hob, Watch> _watches = new Dictionary<Hob, Watch>();
        private readonly HashSet<Ingredient> _burnt = new HashSet<Ingredient>();
        private readonly List<Ingredient> _lost = new List<Ingredient>();

        public FireSafety(FireSettings settings = null)
        {
            Settings = settings ?? new FireSettings();
        }

        /// <summary>Read live, so tuning edits apply in play.</summary>
        public FireSettings Settings { get; set; }

        /// <summary>
        /// A pan caught fire, with the food that was in it: it's taken out of the pan here, and the
        /// caller takes it out of the kitchen.
        /// </summary>
        public event Action<Hob, IReadOnlyList<Ingredient>> Ignited;

        /// <summary>A fire was put out.</summary>
        public event Action<Hob> PutOut;

        /// <summary>Food in a pan on the heat burnt on the side it's lying on, for the first time.</summary>
        public event Action<Hob, Ingredient> FoodBurnt;

        /// <summary>Only gas has an open flame that grease can catch.</summary>
        public static bool CanCatchFire(Hob hob) => hob != null && hob.Kind == HeatKind.Gas;

        public bool IsBurning(Hob hob) => hob != null && _watches.TryGetValue(hob, out var watch) && watch.Burning;

        /// <summary>0..1: how far a burning hob is towards being put out.</summary>
        public float PutOutProgress(Hob hob) =>
            hob != null && _watches.TryGetValue(hob, out var watch) && watch.Burning && Settings.SweepsToPutOut > 0f
                ? Math.Clamp(watch.PutOut / Settings.SweepsToPutOut, 0f, 1f)
                : 0f;

        /// <summary>0..1: how close a smouldering pan is to catching, for the view (0 when not smouldering).</summary>
        public float Smouldering(Hob hob) =>
            hob != null && _watches.TryGetValue(hob, out var watch) && !watch.Burning && Settings.IgnitionSeconds > 0f
                ? Math.Clamp(watch.Smouldering / Settings.IgnitionSeconds, 0f, 1f)
                : 0f;

        /// <summary>Any hob on fire, for the alarm.</summary>
        public bool AnyBurning
        {
            get
            {
                foreach (var watch in _watches.Values)
                {
                    if (watch.Burning)
                        return true;
                }
                return false;
            }
        }

        /// <summary>Watches the hobs for a step of real time.</summary>
        public void Tick(IReadOnlyList<Hob> hobs, float realSeconds)
        {
            if (realSeconds <= 0f)
                return;
            foreach (var hob in hobs)
            {
                NoticeBurning(hob);
                if (!CanCatchFire(hob))
                    continue;
                var watch = WatchFor(hob);
                if (watch.Burning)
                    continue;

                if (!Smoulders(hob))
                {
                    watch.Smouldering = 0f;
                    continue;
                }
                watch.Smouldering += realSeconds;
                if (watch.Smouldering >= Settings.IgnitionSeconds)
                    Ignite(hob, watch);
            }
        }

        /// <summary>Extinguisher work on a burning hob; enough sweeps put it out.</summary>
        /// <returns>True if that put it out.</returns>
        public bool Spray(Hob hob, float sweeps)
        {
            if (!IsBurning(hob) || sweeps <= 0f)
                return false;
            var watch = _watches[hob];
            watch.PutOut += sweeps;
            if (watch.PutOut < Settings.SweepsToPutOut)
                return false;
            Extinguish(hob, watch);
            return true;
        }

        /// <summary>The kitchen closes: whatever's still burning goes out (it doesn't burn all night).</summary>
        /// <returns>The hobs that were burning.</returns>
        public List<Hob> PutOutAll()
        {
            var were = new List<Hob>();
            foreach (var pair in _watches)
            {
                if (pair.Value.Burning)
                    were.Add(pair.Key);
            }
            foreach (var hob in were)
                Extinguish(hob, _watches[hob]);
            return were;
        }

        /// <summary>A hob was sold: stop watching it.</summary>
        public void Forget(Hob hob) => _watches.Remove(hob);

        // A pan on a lit flame with food burnt on the side it's lying on.
        private static bool Smoulders(Hob hob)
        {
            var pan = hob.Pan;
            if (pan == null || hob.Dial <= 0f)
                return false;
            foreach (var food in pan.Contents)
            {
                if (BurntUnderneath(food))
                    return true;
            }
            return false;
        }

        private static bool BurntUnderneath(Ingredient food) =>
            food.Properties.BurntBrowning > 0f && food.Browning(food.DownSide) >= food.Properties.BurntBrowning;

        // Each piece of food counts once, the first time it burns on the heat.
        private void NoticeBurning(Hob hob)
        {
            var pan = hob?.Pan;
            if (pan == null || hob.Dial <= 0f)
                return;
            foreach (var food in pan.Contents)
            {
                if (BurntUnderneath(food) && _burnt.Add(food))
                    FoodBurnt?.Invoke(hob, food);
            }
        }

        private void Ignite(Hob hob, Watch watch)
        {
            watch.Burning = true;
            watch.Smouldering = 0f;
            watch.PutOut = 0f;

            // The food is lost to the flames.
            _lost.Clear();
            var pan = hob.Pan;
            while (pan != null && pan.CanTake)
            {
                var food = (Ingredient)pan.Take();
                _burnt.Remove(food);
                _lost.Add(food);
            }
            Ignited?.Invoke(hob, _lost.ToArray());
        }

        private void Extinguish(Hob hob, Watch watch)
        {
            watch.Burning = false;
            watch.Smouldering = 0f;
            watch.PutOut = 0f;
            // Put out means the gas off too, or it would catch again on the next burnt steak.
            hob.Dial = 0f;
            PutOut?.Invoke(hob);
        }

        private Watch WatchFor(Hob hob)
        {
            if (!_watches.TryGetValue(hob, out var watch))
            {
                watch = new Watch();
                _watches[hob] = watch;
            }
            return watch;
        }
    }
}
