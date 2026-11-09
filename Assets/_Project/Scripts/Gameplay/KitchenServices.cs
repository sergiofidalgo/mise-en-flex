using System;
using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Items;
using Game.Simulation.Cooking;
using Game.Simulation.Days;
using Game.Simulation.Items;
using Game.Simulation.Movement;
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
            StaffConfig staffConfig,
            StationRegistry stations,
            FloorPlanConfig floorPlan)
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
            Stations = stations;
            FloorPlan = floorPlan;
            Building = floorPlan.ToPlan();
        }

        public CookingSimulation Cooking { get; }

        /// <summary>Reads the hobs once a day for the energy bill.</summary>
        public EnergyMeter Energy { get; } = new EnergyMeter();
        public ItemViewRegistry ItemViews { get; }
        public ItemCatalog Catalog { get; }
        public EquipmentTier Equipment { get; }
        public TuningConfig Tuning { get; }
        public RealismSettings Realism { get; }

        /// <summary>Today's dinner service: customers, seats and arrivals. A new one each day.</summary>
        public ServiceSimulation Service { get; private set; }
        public ServiceConfig ServiceConfig { get; }

        public DayCycle Days { get; }

        /// <summary>What this morning's delivery brought, as (type id, portions).</summary>
        public List<(string TypeId, int Count)> LastDelivery { get; set; } = new List<(string, int)>();

        public StaffRoster Staff { get; }

        /// <summary>Reviews from earlier days, as saved. Today's are on <see cref="Service"/>.</summary>
        public List<Game.Simulation.Saving.ReviewRecord> ReviewHistory { get; } = new List<Game.Simulation.Saving.ReviewRecord>();

        /// <summary>Review and reply lines used lately, saved with the campaign so they don't repeat.</summary>
        public Game.Simulation.Service.LineMemory ReviewLines { get; set; } = new Game.Simulation.Service.LineMemory();

        /// <summary>Orders taken per dish since the campaign began (not every guest reviews).</summary>
        public Dictionary<string, int> DishTally { get; } = new Dictionary<string, int>();

        /// <summary>What says a plate on a counter is ready to go out.</summary>
        public ReadyBell Bell { get; } = new ReadyBell();
        public StaffConfig StaffConfig { get; }

        /// <summary>The stations in the kitchen right now.</summary>
        public StationRegistry Stations { get; }

        /// <summary>The building: its rooms, walls and doors.</summary>
        public FloorPlanConfig FloorPlan { get; }

        /// <summary>The same, for the rules: where people can stand and reach.</summary>
        public FloorPlan Building { get; }

        /// <summary>Where people can walk with the furniture as it stands now.</summary>
        /// <param name="alsoAvoid">Anything else to keep clear of, besides the furniture.</param>
        public StationLayout NewLayout(IEnumerable<Footprint> alsoAvoid = null)
        {
            var footprints = new List<Footprint>();
            if (alsoAvoid != null)
                footprints.AddRange(alsoAvoid);
            foreach (var station in Stations.All)
                footprints.Add(station.Footprint);
            return new StationLayout(footprints, Building);
        }

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
