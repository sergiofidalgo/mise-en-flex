namespace Game.Gameplay.Saving
{
    /// <summary>
    /// A station that keeps a number of identical things, such as clean plates or empty trays, which
    /// a save only needs to count.
    /// </summary>
    public interface ISavedCount
    {
        int SavedCount { get; }

        void RestoreCount(int count);
    }
}
