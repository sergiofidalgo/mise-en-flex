using System;
using System.Collections.Generic;

namespace Game.Simulation.Saving
{
    /// <summary>
    /// One restaurant of the campaign, as it was last left (Phase 6, decision 2026-11-10): its room,
    /// what's in it, its staff, its guests' reviews, its own day count and fee clock. A restaurant
    /// only runs while players are in it, so an idle one keeps this record untouched until they
    /// come back. Money is not here: it belongs to the campaign (<see cref="CampaignSave.Balance"/>).
    /// </summary>
    [Serializable]
    public sealed class RestaurantSave
    {
        /// <summary>
        /// The id of the <c>RestaurantDefinition</c> it was bought as (its size, street, vendors).
        /// A restaurant migrated from a version-1 file is the grill (<see cref="Version1Restaurant.GrillId"/>).
        /// </summary>
        public string DefinitionId = "";

        /// <summary>The name players gave it, for the sign, reviews and the slot card.</summary>
        public string Name = "";

        public string StyleName = "";
        public string Area = "";

        /// <summary>
        /// The day that was just played here. Continue starts the morning after (decision 2026-11-08).
        /// Days are counted per restaurant: one played elsewhere doesn't move this one on.
        /// </summary>
        public int Day = 1;

        /// <summary>Finished days here since its last fee (every 5, paused while idle: decision 2026-11-10).</summary>
        public int DaysTowardsFee;

        /// <summary>Money in and out here since its last fee, one amount per ledger line: this restaurant's cut.</summary>
        public float[] Period = new float[0];

        /// <summary>How each day here went, for the restaurant's progress in Books.</summary>
        public List<Days.DayRecord> History = new List<Days.DayRecord>();

        /// <summary>Plates broken at the sink and not yet replaced.</summary>
        public int BrokenPlates;

        public List<StationRecord> Stations = new List<StationRecord>();

        /// <summary>Everything lying around the kitchen, flattened: a pan's food points back at the pan.</summary>
        public List<ItemRecord> Items = new List<ItemRecord>();

        public List<StaffRecord> Staff = new List<StaffRecord>();

        public List<ReviewRecord> Reviews = new List<ReviewRecord>();

        /// <summary>Every order ever taken here, per dish: not every guest reviews, so reviews can't count them.</summary>
        public List<DishCount> Dishes = new List<DishCount>();

        /// <summary>
        /// Prices set here; any dish not listed sells at its suggested price. Per restaurant, since
        /// the same steak can fairly cost more in a dining room than at a counter (service styles, 6b).
        /// </summary>
        public List<Cookbook.PriceRecord> Prices = new List<Cookbook.PriceRecord>();

        /// <summary>Dirt left from service, cleaned the next morning (decision 2026-11-06).</summary>
        public List<DirtRecord> Dirt = new List<DirtRecord>();
    }

    /// <summary>
    /// The restaurant's fields as a version-1 file had them, at the top of the file. Read from the same
    /// text as the <see cref="CampaignSave"/> and handed to <see cref="SaveMigration.Upgrade"/>.
    /// Frozen: never rename or remove a field, or old files lose it.
    /// </summary>
    [Serializable]
    public sealed class Version1Restaurant
    {
        /// <summary>
        /// The definition id of the grill counter, the only restaurant before Phase 6. Its
        /// <c>RestaurantDefinition</c> asset uses the same id, so a migrated campaign owns that lot.
        /// </summary>
        public const string GrillId = "grill";

        public int Day = 1;
        public int BrokenPlates;
        public List<StationRecord> Stations = new List<StationRecord>();
        public List<ItemRecord> Items = new List<ItemRecord>();
        public List<StaffRecord> Staff = new List<StaffRecord>();
        public List<ReviewRecord> Reviews = new List<ReviewRecord>();
        public int DaysTowardsFee;
        public float[] Period = new float[0];
        public List<Days.DayRecord> History = new List<Days.DayRecord>();
        public string RestaurantName = "";
        public string StyleName = "";
        public string Area = "";
        public List<DishCount> Dishes = new List<DishCount>();
        public List<Cookbook.PriceRecord> Prices = new List<Cookbook.PriceRecord>();
        public List<DirtRecord> Dirt = new List<DirtRecord>();

        /// <summary>Restaurant 1 of a migrated campaign: the grill counter every version-1 file was.</summary>
        public RestaurantSave ToRestaurant() => new RestaurantSave
        {
            DefinitionId = GrillId,
            Name = RestaurantName ?? "",
            StyleName = StyleName ?? "",
            Area = Area ?? "",
            Day = Day,
            DaysTowardsFee = DaysTowardsFee,
            Period = Period ?? new float[0],
            History = History ?? new List<Days.DayRecord>(),
            BrokenPlates = BrokenPlates,
            Stations = Stations ?? new List<StationRecord>(),
            Items = Items ?? new List<ItemRecord>(),
            Staff = Staff ?? new List<StaffRecord>(),
            Reviews = Reviews ?? new List<ReviewRecord>(),
            Dishes = Dishes ?? new List<DishCount>(),
            Prices = Prices ?? new List<Cookbook.PriceRecord>(),
            Dirt = Dirt ?? new List<DirtRecord>(),
        };
    }
}
