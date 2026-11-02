using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;

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
        public float CrustPale { get; set; } = 0.2f;

        /// <summary>Average browning range for a full-marks crust.</summary>
        public float CrustGoodMin { get; set; } = 0.7f;
        public float CrustGoodMax { get; set; } = 1.4f;

        public float DonenessWeight { get; set; } = 0.4f;
        public float CrustWeight { get; set; } = 0.25f;
        public float TemperatureWeight { get; set; } = 0.25f;
        public float FreshnessWeight { get; set; } = 0.1f;
    }

    /// <summary>One food item's score. Parts that don't apply to it are null.</summary>
    public sealed class ItemScore
    {
        public Ingredient Ingredient { get; internal set; }
        public string DonenessName { get; internal set; }
        public float? Doneness { get; internal set; }
        public float? Crust { get; internal set; }
        public float? Temperature { get; internal set; }
        public float Freshness { get; internal set; }

        /// <summary>Weighted average of the parts that apply, 0..1.</summary>
        public float Overall { get; internal set; }

        /// <summary>Average temperature when served, °C.</summary>
        public float ServedAt { get; internal set; }
    }

    public sealed class DishScore
    {
        public DishScore(IReadOnlyList<ItemScore> items, float overall)
        {
            Items = items;
            Overall = overall;
        }

        public IReadOnlyList<ItemScore> Items { get; }

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
        public static DishScore Score(Plate plate, ScoringSettings settings)
        {
            var items = new List<ItemScore>();
            foreach (var item in plate.Items)
            {
                if (item is Ingredient ingredient)
                    items.Add(ScoreItem(ingredient, settings));
            }

            var overall = 0f;
            foreach (var item in items)
                overall += item.Overall;
            return new DishScore(items, items.Count > 0 ? overall / items.Count : 0f);
        }

        public static ItemScore ScoreItem(Ingredient ingredient, ScoringSettings settings)
        {
            var p = ingredient.Properties;
            var score = new ItemScore
            {
                Ingredient = ingredient,
                DonenessName = ingredient.Doneness?.Name ?? (p.DonenessLevels.Count > 0 ? "Raw" : null),
                Freshness = ingredient.Freshness,
                ServedAt = ingredient.AverageTemperature,
            };

            if (p.TargetCoreTemperature.HasValue)
                score.Doneness = DonenessScore(ingredient.PeakCoreTemperature, p.TargetCoreTemperature.Value, settings);

            if (p.ScoresBrowning)
                score.Crust = ingredient.IsBurnt ? 0f : CrustScore(
                    (ingredient.Browning(IngredientSide.A) + ingredient.Browning(IngredientSide.B)) / 2f, p.BurntBrowning, settings);

            if (p.ServedHot)
                score.Temperature = Ramp(ingredient.AverageTemperature, p.ServingColdTemperature, p.ServingIdealTemperature);

            // Weighted average of the parts that apply to this item.
            var total = settings.FreshnessWeight * score.Freshness;
            var weights = settings.FreshnessWeight;
            Add(score.Doneness, settings.DonenessWeight);
            Add(score.Crust, settings.CrustWeight);
            Add(score.Temperature, settings.TemperatureWeight);
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

        // Full marks within the tolerance of the target, then falling linearly to 0 over the falloff.
        private static float DonenessScore(float peakCore, float target, ScoringSettings settings)
        {
            var off = Math.Abs(peakCore - target) - settings.DonenessTolerance;
            return off <= 0f ? 1f : Math.Max(0f, 1f - off / settings.DonenessFalloff);
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
