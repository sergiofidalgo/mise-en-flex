using System;
using Game.Data;
using Game.Gameplay.Items;
using Game.Simulation.Cooking;
using Game.Simulation.Days;
using Game.Simulation.Items;
using Game.Simulation.Staff;
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
            ServiceConfig serviceConfig,
            DayCycle days,
            StaffRoster staff,
            StaffConfig staffConfig)
        {
            Cooking = cooking;
            ItemViews = itemViews;
            Catalog = catalog;
            Equipment = equipment;
            Tuning = tuning;
            Realism = realism;
            Service = service;
            ServiceConfig = serviceConfig;
            Days = days;
            Staff = staff;
            StaffConfig = staffConfig;
        }

        public CookingSimulation Cooking { get; }
        public ItemViewRegistry ItemViews { get; }
        public ItemCatalog Catalog { get; }
        public EquipmentTier Equipment { get; }
        public TuningConfig Tuning { get; }
        public RealismSettings Realism { get; }

        /// <summary>Today's dinner service: customers, seats and arrivals. A new one each day.</summary>
        public ServiceSimulation Service { get; private set; }
        public ServiceConfig ServiceConfig { get; }

        public DayCycle Days { get; }

        public StaffRoster Staff { get; }
        public StaffConfig StaffConfig { get; }

        /// <summary>Raised when a new day's service replaces the last, so listeners can move over to it.</summary>
        public event Action<ServiceSimulation, ServiceSimulation> ServiceReplaced;

        public void ReplaceService(ServiceSimulation service)
        {
            var old = Service;
            Service = service;
            ServiceReplaced?.Invoke(old, service);
        }

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
