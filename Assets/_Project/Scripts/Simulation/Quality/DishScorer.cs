using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Service;

namespace Game.Simulation.Quality
{
    /// <summary>Tuning for <see cref="DishScorer"/>. Weights are relative; missing parts are left out and the rest rescaled.</summary>
    public sealed class ScoringSettings
    {
        /// <summary>°C either side of the target core temperature that still scores full marks.</summary>
        public float DonenessTolerance { get; set; } = 3f;

        /// <summary>°C beyond the tolerance over which the doneness score falls from 1 to 0.</summary>
        public float DonenessFalloff { get; set; } = 8f;

        /// <summary>Average browning below which the crust scores 0 (pale, steamed meat).</summary>
        public float CrustPale { get; set; } = 0.1f;

        /// <summary>Average browning range for a full-marks crust.</summary>
        public float CrustGoodMin { get; set; } = 0.6f;
        public float CrustGoodMax { get; set; } = 1.4f;

        public float DonenessWeight { get; set; } = 0.4f;
        public float CrustWeight { get; set; } = 0.25f;
        public float TemperatureWeight { get; set; } = 0.25f;
        public float FreshnessWeight { get; set; } = 0.1f;
        public float CutWeight { get; set; } = 0.15f;
        public float RestWeight { get; set; } = 0.15f;

        /// <summary>
        /// With an order, how much a side counts in the dish next to the main (1). A missing
        /// ordered item counts as a zero at its weight, so forgetting the onions costs points.
        /// </summary>
        public float SideWeight { get; set; } = 0.5f;
    }

    /// <summary>One food item's score. Parts that don't apply to it are null.</summary>
    public sealed class ItemScore
    {
        public Ingredient Ingredient { get; internal set; }
        public string DonenessName { get; internal set; }

        /// <summary>The doneness that was asked for (the order's, else the house standard's level), or null.</summary>
        public string TargetDonenessName { get; internal set; }
        public float? Doneness { get; internal set; }
        public float? Crust { get; internal set; }
        public float? Temperature { get; internal set; }
        public float Freshness { get; internal set; }

        /// <summary>Evenness of the cut, for food cut at the prep counter.</summary>
        public float? Cut { get; internal set; }

        /// <summary>How well it rested off the pan before serving, for food that should rest (steak).</summary>
        public float? Rest { get; internal set; }

        /// <summary>Weighted average of the parts that apply, 0..1.</summary>
        public float Overall { get; internal set; }

        /// <summary>Average temperature when served, °C.</summary>
        public float ServedAt { get; internal set; }
    }

    public sealed class DishScore
    {
        public DishScore(IReadOnlyList<ItemScore> items, float overall, Order order = null, IReadOnlyList<string> missing = null, OrderMatch? match = null)
        {
            Items = items;
            Overall = overall;
            Order = order;
            Missing = missing ?? Array.Empty<string>();
            Match = match;
        }

        public IReadOnlyList<ItemScore> Items { get; }

        /// <summary>The order it was scored against, or null (house standard).</summary>
        public Order Order { get; }

        /// <summary>Type ids the order asked for that weren't on the plate.</summary>
        public IReadOnlyList<string> Missing { get; }

        /// <summary>How the plate compared with the order when served, or null without an order.</summary>
        public OrderMatch? Match { get; }

        /// <summary>0..1: the average of the items' scores.</summary>
        public float Overall { get; }

        /// <summary>0 to 5 in half stars.</summary>
        public float Stars => (float)Math.Round(Overall * 10f, MidpointRounding.AwayFromZero) / 2f;
    }

    /// <summary>
    /// Scores a plated dish when it reaches the pass: doneness against the target, crust,
    /// serving temperature (food cools from pan to pass, so speed matters) and freshness.
    /// Drives payment, tips and reviews from Phase 2.
    /// </summary>
    public static class DishScorer
    {
        /// <summary>
        /// Scores a plate. With an <paramref name="order"/>, the main is scored against the ordered
        /// doneness, the main weighs 1 and everything else <see cref="ScoringSettings.SideWeight"/>,
        /// and anything ordered but missing counts as a zero. Without one, every item weighs the
        /// same against its house standard.
        /// </summary>
        public static DishScore Score(Plate plate, ScoringSettings settings, Order order = null)
        {
            var items = new List<ItemScore>();
            var total = 0f;
            var weights = 0f;
            var mainScored = false;
            var sideServed = false;
            foreach (var item in plate.Items)
            {
                if (!(item is Ingredient ingredient))
                    continue;
                var isMain = order != null && !mainScored && ingredient.TypeId == order.MainTypeId;
                var score = ScoreItem(ingredient, settings, isMain ? order.Doneness : (DonenessLevel?)null);
                items.Add(score);

                mainScored |= isMain;
                sideServed |= order?.SideTypeId != null && ingredient.TypeId == order.SideTypeId;
                var weight = order == null || isMain ? 1f : settings.SideWeight;
                total += weight * score.Overall;
                weights += weight;
            }

            var missing = new List<string>();
            if (order != null)
            {
                if (!mainScored)
                {
                    missing.Add(order.MainTypeId);
                    weights += 1f;
                }
                if (order.SideTypeId != null && !sideServed)
                {
                    missing.Add(order.SideTypeId);
                    weights += settings.SideWeight;
                }
            }

            var match = order != null ? OrderCheck.Check(order, plate, out _) : (OrderMatch?)null;
            return new DishScore(items, weights > 0f ? total / weights : 0f, order, missing, match);
        }

        /// <param name="ordered">The doneness a customer asked for; null scores against the house standard.</param>
        public static ItemScore ScoreItem(Ingredient ingredient, ScoringSettings settings, DonenessLevel? ordered = null)
        {
            var p = ingredient.Properties;
            var score = new ItemScore
            {
                Ingredient = ingredient,
                DonenessName = ingredient.Doneness?.Name ?? (p.DonenessLevels.Count > 0 ? "Raw" : null),
                Freshness = ingredient.Freshness,
                ServedAt = ingredient.AverageTemperature,
            };

            if (ordered.HasValue)
            {
                score.TargetDonenessName = ordered.Value.Name;
                score.Doneness = DonenessScore(ingredient.PeakCoreTemperature, ordered.Value.OrderTargetCoreTemperature, settings);
            }
            else if (p.TargetCoreTemperature.HasValue)
            {
                score.TargetDonenessName = LevelAt(p, p.TargetCoreTemperature.Value);
                score.Doneness = DonenessScore(ingredient.PeakCoreTemperature, p.TargetCoreTemperature.Value, settings);
            }

            if (p.ScoresBrowning)
                score.Crust = ingredient.IsBurnt ? 0f : CrustOfBothSides(
                    ingredient.Browning(IngredientSide.A), ingredient.Browning(IngredientSide.B), p.BurntBrowning, settings);

            if (p.ServedHot)
                score.Temperature = Ramp(ingredient.AverageTemperature, p.ServingColdTemperature, p.ServingIdealTemperature);

            score.Cut = ingredient.CutEvenness;

            // Resting only means something once it's been cooked (it reached a doneness level).
            if (p.RestTargetSeconds > 0f && ingredient.Doneness.HasValue)
                score.Rest = Ramp(ingredient.SecondsOutOfPan, 0f, p.RestTargetSeconds);

            // Weighted average of the parts that apply to this item.
            var total = settings.FreshnessWeight * score.Freshness;
            var weights = settings.FreshnessWeight;
            Add(score.Doneness, settings.DonenessWeight);
            Add(score.Crust, settings.CrustWeight);
            Add(score.Temperature, settings.TemperatureWeight);
            Add(score.Cut, settings.CutWeight);
            Add(score.Rest, settings.RestWeight);
            score.Overall = weights > 0f ? total / weights : 0f;
            return score;

            void Add(float? part, float weight)
            {
                if (!part.HasValue)
                    return;
                total += weight * part.Value;
                weights += weight;
            }
        }

        private static string LevelAt(IngredientProperties p, float core)
        {
            string name = null;
            foreach (var level in p.DonenessLevels)
            {
                if (core >= level.MinPeakCoreTemperature)
                    name = level.Name;
            }
            return name;
        }

        // Full marks within the tolerance of the target, then falling linearly to 0 over the falloff.
        private static float DonenessScore(float peakCore, float target, ScoringSettings settings)
        {
            var off = Math.Abs(peakCore - target) - settings.DonenessTolerance;
            return off <= 0f ? 1f : Math.Max(0f, 1f - off / settings.DonenessFalloff);
        }

        // Judged by the better side, the one that's presented, so searing one side and finishing gently
        // is as good as it gets. But any side close to burnt loses points, however good the other is.
        private static float CrustOfBothSides(float a, float b, float burnt, ScoringSettings settings)
        {
            var best = Math.Max(CrustScore(a, burnt, settings), CrustScore(b, burnt, settings));
            var nearlyBurnt = 1f - Ramp(Math.Max(a, b), settings.CrustGoodMax, burnt);
            return Math.Min(best, nearlyBurnt);
        }

        // Pale scores 0, rising to full marks over the good range, then dropping towards burnt.
        private static float CrustScore(float browning, float burnt, ScoringSettings settings)
        {
            if (browning <= settings.CrustGoodMax)
                return Ramp(browning, settings.CrustPale, settings.CrustGoodMin);
            return 1f - Ramp(browning, settings.CrustGoodMax, burnt);
        }

        // 0 at or below `from`, 1 at or above `to`, linear between.
        private static float Ramp(float value, float from, float to)
        {
            if (to <= from)
                return value >= to ? 1f : 0f;
            return Math.Clamp((value - from) / (to - from), 0f, 1f);
        }
    }
}
