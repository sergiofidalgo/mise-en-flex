namespace Game.Data
{
    /// <summary>
    /// How an item visibly travels between hands and stations.
    /// </summary>
    public readonly struct ItemMoveSettings
    {
        public ItemMoveSettings(float duration, float arcHeight, float pop)
        {
            Duration = duration;
            ArcHeight = arcHeight;
            Pop = pop;
        }

        /// <summary>Seconds; zero or less snaps instantly.</summary>
        public float Duration { get; }

        /// <summary>Metres of upward arc at mid-flight.</summary>
        public float ArcHeight { get; }

        /// <summary>Extra scale at mid-flight, as a fraction.</summary>
        public float Pop { get; }
    }
}
