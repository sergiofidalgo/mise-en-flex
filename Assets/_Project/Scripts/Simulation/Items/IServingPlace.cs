namespace Game.Simulation.Items
{
    /// <summary>One customer's place at a table: their seat, and where their plate goes.</summary>
    public interface IServingPlace
    {
        int SeatIndex { get; }

        PassCounter Slot { get; }
    }
}
