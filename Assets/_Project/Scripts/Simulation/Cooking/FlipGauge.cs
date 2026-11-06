using System;

namespace Game.Simulation.Cooking
{
    /// <summary>Where the flip gauge's green band sits, in browning of the side facing down.</summary>
    public sealed class FlipGaugeSettings
    {
        public float GoodFrom { get; set; } = 0.7f;
        public float GoodTo { get; set; } = 1.1f;

        /// <summary>Browning of the side in the pan at which the bar warns it's about to burn.</summary>
        public float WarnFrom { get; set; } = 1.5f;
    }

    /// <summary>
    /// The bar under a pan (decisions 2026-11-03 and 06). From the moment food that needs flipping
    /// touches the pan until it's flipped, it reads the browning of the side facing down; after that,
    /// the stages of doneness. It reads the food, not a timer, so it creeps in a cool pan and races
    /// in a hot one, which is how players learn what heat does.
    /// </summary>
    public static class FlipGauge
    {
        public static bool Shows(Ingredient ingredient) =>
            ingredient.Properties.NeedsFlip && !ingredient.HasBeenFlipped;

        /// <summary>0 (no colour) to 1 (burnt).</summary>
        public static float Position(Ingredient ingredient) =>
            ToPosition(ingredient.Browning(ingredient.DownSide), ingredient.Properties);

        /// <summary>A browning value on the gauge's 0..1 scale for this ingredient.</summary>
        public static float ToPosition(float browning, IngredientProperties properties) =>
            properties.BurntBrowning > 0f ? Math.Clamp(browning / properties.BurntBrowning, 0f, 1f) : 1f;

        /// <summary>
        /// The side in the pan is close to burning, or something already has. It reads the side
        /// down, so flipping away from danger clears it.
        /// </summary>
        public static bool IsAboutToBurn(Ingredient ingredient, FlipGaugeSettings settings) =>
            ingredient.IsBurnt || ingredient.Browning(ingredient.DownSide) >= settings.WarnFrom;

        /// <summary>After the first flip the bar shows the stages instead, for food that has any.</summary>
        public static bool ShowsStages(Ingredient ingredient) =>
            ingredient.Properties.NeedsFlip && ingredient.HasBeenFlipped && ingredient.Properties.DonenessLevels.Count > 0;

        /// <summary>How many stages the bar has: raw, then each doneness level.</summary>
        public static int StageCount(IngredientProperties properties) => properties.DonenessLevels.Count + 1;

        /// <summary>
        /// Where the food's doneness is on the bar, 0..1, with the stages equally wide: it follows the
        /// hottest its middle has been, as doneness does, so how fast it moves shows what the heat is
        /// doing. Stops at the end of the last stage.
        /// </summary>
        public static float StagePosition(Ingredient ingredient)
        {
            var levels = ingredient.Properties.DonenessLevels;
            var core = ingredient.PeakCoreTemperature;
            var from = Math.Min(ingredient.Properties.StartTemperature, levels[0].MinPeakCoreTemperature - 1f);
            for (var stage = 0; stage <= levels.Count; stage++)
            {
                // The last stage is as wide in degrees as the one before it.
                var to = stage < levels.Count
                    ? levels[stage].MinPeakCoreTemperature
                    : from + (levels.Count > 1 ? from - levels[levels.Count - 2].MinPeakCoreTemperature : 10f);
                if (core < to || stage == levels.Count)
                    return Math.Clamp((stage + Math.Clamp((core - from) / (to - from), 0f, 1f)) / (levels.Count + 1), 0f, 1f);
                from = to;
            }
            return 1f;
        }

        public static bool IsInGoodBand(Ingredient ingredient, FlipGaugeSettings settings)
        {
            var browning = ingredient.Browning(ingredient.DownSide);
            return browning >= settings.GoodFrom && browning <= settings.GoodTo;
        }
    }
}
