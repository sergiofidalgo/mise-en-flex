namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A station that does something on the Use button (X; keyboard A). From milestone 3
    /// Use opens the station's close-up instead.
    /// </summary>
    public interface IUsable
    {
        /// <returns>False if nothing could happen, so the station shows its "can't" feedback.</returns>
        bool TryUse(PlayerInteractor player);
    }
}
