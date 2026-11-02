namespace Game.Gameplay.Stations
{
    /// <summary>A station whose heat the facing player turns with the triggers (keyboard Q / E).</summary>
    public interface IHeatControl
    {
        /// <param name="heatDown">Left trigger, 0..1.</param>
        /// <param name="heatUp">Right trigger, 0..1.</param>
        void AdjustHeat(float heatDown, float heatUp, float realDeltaTime);
    }
}
