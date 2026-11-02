namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A station that does something on the Use button (X; keyboard A) without a close-up.
    /// Stations with a CloseUpStation open their close-up on Use instead.
    /// </summary>
    public interface IUsable
    {
        /// <returns>False if nothing could happen, so the station shows its "can't" feedback.</returns>
        bool TryUse(PlayerInteractor player);
    }
}
