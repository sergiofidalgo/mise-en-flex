using Game.Simulation.Items;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// The way in from the street. Moved like furniture, along the dining room's outer walls, but it
    /// isn't furniture to anyone else: nobody works at it or walks around it, and it can't be sold.
    /// </summary>
    public sealed class StreetDoor : Interactable
    {
        public override IItemContainer Container => null;
    }
}
