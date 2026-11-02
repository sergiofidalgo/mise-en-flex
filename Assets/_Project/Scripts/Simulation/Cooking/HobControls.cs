namespace Game.Simulation.Cooking
{
    /// <summary>
    /// What a player does at a hob in the kitchen view (decision 2026-11-02): the triggers turn
    /// the heat smoothly and Use flips the food. A pan holds one piece of food, so there's never
    /// a question of which one.
    /// </summary>
    public static class HobControls
    {
        /// <param name="heatDown">Left trigger (keyboard Q), 0..1.</param>
        /// <param name="heatUp">Right trigger (keyboard E), 0..1.</param>
        /// <param name="dialSpeed">Dial change per second at full trigger (1 = off to full in a second).</param>
        /// <param name="realDeltaTime">Real seconds, not game time: controls feel the same at any time scale.</param>
        public static void AdjustHeat(Hob hob, float heatDown, float heatUp, float dialSpeed, float realDeltaTime)
        {
            hob.Dial += (heatUp - heatDown) * dialSpeed * realDeltaTime;
        }

        /// <returns>False if there's nothing in the pan to flip (or no pan).</returns>
        public static bool TryFlip(Hob hob)
        {
            var pan = hob.Pan;
            if (pan == null || pan.Contents.Count == 0)
                return false;

            pan.Contents[0].Flip();
            return true;
        }
    }
}
