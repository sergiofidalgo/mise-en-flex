using Game.Data;
using Game.Gameplay.Items;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Service;

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
            TuningConfig tuning,
            RealismSettings realism,
            ServiceSimulation service,
            ServiceConfig serviceConfig)
        {
            Cooking = cooking;
            ItemViews = itemViews;
            Catalog = catalog;
            Equipment = equipment;
            Tuning = tuning;
            Realism = realism;
            Service = service;
            ServiceConfig = serviceConfig;
        }

        public CookingSimulation Cooking { get; }
        public ItemViewRegistry ItemViews { get; }
        public ItemCatalog Catalog { get; }
        public EquipmentTier Equipment { get; }
        public TuningConfig Tuning { get; }
        public RealismSettings Realism { get; }

        /// <summary>The dinner service: customers, seats and arrivals.</summary>
        public ServiceSimulation Service { get; }
        public ServiceConfig ServiceConfig { get; }

        /// <summary>
        /// Creates a new item of <paramref name="typeId"/>. Known ingredients are registered with
        /// the cooking simulation so they heat, cool and spoil wherever they are.
        /// </summary>
        /// <param name="startTemperature">°C to start at (e.g. the fridge's); defaults to the ingredient's own.</param>
        public Item CreateItem(string typeId, float? startTemperature = null)
        {
            var definition = Catalog.Find(typeId);
            if (definition == null)
                return new Item(typeId);

            var properties = definition.ToProperties();
            if (startTemperature.HasValue)
                properties.StartTemperature = startTemperature.Value;
            return Cooking.AddIngredient(properties);
        }
    }
}
