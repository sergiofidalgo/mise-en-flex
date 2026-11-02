using Game.Data;
using Game.Gameplay.Items;
using Game.Simulation.Cooking;
using Game.Simulation.Items;

namespace Game.Gameplay
{
    /// <summary>
    /// The kitchen scene's shared services, created once by <see cref="KitchenCompositionRoot"/>
    /// and handed to stations and players.
    /// </summary>
    public sealed class KitchenServices
    {
        public KitchenServices(
            CookingSimulation cooking,
            ItemViewRegistry itemViews,
            ItemCatalog catalog,
            EquipmentTier equipment,
            TuningConfig tuning)
        {
            Cooking = cooking;
            ItemViews = itemViews;
            Catalog = catalog;
            Equipment = equipment;
            Tuning = tuning;
        }

        public CookingSimulation Cooking { get; }
        public ItemViewRegistry ItemViews { get; }
        public ItemCatalog Catalog { get; }
        public EquipmentTier Equipment { get; }
        public TuningConfig Tuning { get; }

        /// <summary>
        /// Creates a new item of <paramref name="typeId"/>. Known ingredients are registered with
        /// the cooking simulation so they heat, cool and spoil wherever they are.
        /// </summary>
        public Item CreateItem(string typeId)
        {
            var definition = Catalog.Find(typeId);
            return definition != null
                ? Cooking.AddIngredient(definition.ToProperties())
                : new Item(typeId);
        }
    }
}
