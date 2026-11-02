namespace Game.Gameplay.Stations
{
    /// <summary>A station that does something on the Use button (X; keyboard A), e.g. a hob flips its food.</summary>
    public interface IUsable
    {
        /// <returns>False if nothing could happen, so the station shows its "can't" feedback.</returns>
        bool TryUse(PlayerInteractor player);
    }
}
