using Game.Simulation.Cooking;
using Game.Simulation.Items;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// The bin: discards single items, and scrapes food off held plates and out of held pans.
    /// Discarded food stops being simulated and its view is removed.
    /// </summary>
    public sealed class BinStation : Interactable
    {
        private readonly ItemSink _sink = new ItemSink();
        private KitchenServices _services;

        public override IItemContainer Container => _sink;

        public ItemSink Sink => _sink;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            _sink.Discarded += HandleDiscarded;
        }

        private void OnDestroy()
        {
            _sink.Discarded -= HandleDiscarded;
        }

        private void HandleDiscarded(Item item)
        {
            if (item is Ingredient ingredient)
                _services.Cooking.Remove(ingredient);
            _services.ItemViews.Release(item);
        }
    }
}
