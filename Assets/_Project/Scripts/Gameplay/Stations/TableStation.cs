namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A table in the dining room: one customer, served by carrying the plate out and cleared by
    /// carrying it back. Bought like any furniture, and adds a seat to the service.
    /// </summary>
    public sealed class TableStation : ServingStation
    {
        public override bool BelongsInDiningRoom => true;
    }
}
