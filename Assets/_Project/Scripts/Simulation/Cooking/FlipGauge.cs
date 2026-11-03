using System;

namespace Game.Simulation.Cooking
{
    /// <summary>Where the flip gauge's green band sits, in browning of the side facing down.</summary>
    public sealed class FlipGaugeSettings
    {
        public float GoodFrom { get; set; } = 0.7f;
        public float GoodTo { get; set; } = 1.1f;
    }

    /// <summary>
    /// The gauge for the first flip (decision 2026-11-03): from the moment food that needs flipping
    /// touches the pan until it's flipped, it reads the browning of the side facing down. It reads
    /// the food, not a timer, so it creeps in a cool pan and races in a hot one.
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

        public static bool IsInGoodBand(Ingredient ingredient, FlipGaugeSettings settings)
        {
            var browning = ingredient.Browning(ingredient.DownSide);
            return browning >= settings.GoodFrom && browning <= settings.GoodTo;
        }
    }
}
