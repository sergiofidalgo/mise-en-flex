namespace Game.Simulation.Items
{
    /// <summary>
    /// A chef's knife. Tools are held in their own slot alongside whatever is in the hands
    /// (decision 2026-11-02), so a chef can carry an onion and the knife at once.
    /// </summary>
    public sealed class Knife : Item
    {
        public const string KnifeTypeId = "Knife";

        public Knife()
            : base(KnifeTypeId)
        {
        }
    }
}
