using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Gameplay.Stations;
using Game.Simulation.Items;
using Game.Simulation.Saving;
using UnityEngine;

namespace Game.Gameplay.Saving
{
    /// <summary>
    /// Writes the restaurant being played into a <see cref="RestaurantSave"/>, and puts one back into
    /// the scene. Putting back happens in two steps around binding: the furniture first, so the
    /// stations bind where they stand, then what's on them. What belongs to the campaign rather than
    /// the restaurant (money, recipes, recent lines) is read and written by <see cref="CaptureCampaign"/>.
    /// </summary>
    public static class CampaignSnapshot
    {
        /// <summary>The restaurant as it is now, at the start of Review.</summary>
        /// <param name="players">The chefs: what's in their hands and belts is kept too.</param>
        public static RestaurantSave Capture(KitchenServices services, IEnumerable<PlayerInteractor> players)
        {
            var save = new RestaurantSave
            {
                Day = services.Days.Day,
            };

            var stations = services.Stations.All;
            for (var i = 0; i < stations.Count; i++)
            {
                var station = stations[i];
                var position = station.transform.position;
                save.Stations.Add(new StationRecord
                {
                    FurnitureId = station.FurnitureId ?? "",
                    SceneName = station.name,
                    X = position.x,
                    Z = position.z,
                    Degrees = station.transform.eulerAngles.y,
                    Chairs = station is TableStation table ? table.ChairCount : 0,
                    Count = station is ISavedCount counted ? counted.SavedCount : 0,
                });
                if (station is ISavedContents contents)
                {
                    foreach (var (slot, item) in contents.SavedItems)
                        ItemRecords.Write(item, i, slot, save.Items);
                }
                if (station is SinkStation sink)
                    save.BrokenPlates = sink.Sink.Broken;
            }

            // TODO(Phase 5): anything a member of staff is carrying when the service ends isn't kept.
            foreach (var player in players)
            {
                if (player.Hands.Current != null)
                    ItemRecords.Write(player.Hands.Current, -1, 0, save.Items);
                if (player.Tool != null)
                    ItemRecords.Write(player.Tool, -1, 0, save.Items);
            }

            // Dirt on furniture points at its station record; on the floor, it's where it lies.
            DirtRecords.Write(services.Dirt, host =>
            {
                for (var s = 0; s < stations.Count; s++)
                {
                    if (ReferenceEquals(stations[s], host))
                        return s;
                }
                return -1;
            }, save.Dirt);

            save.Staff.AddRange(services.Staff.Hired.Select(SaveConversions.ToRecord));
            save.Reviews.AddRange(services.ReviewHistory);
            save.Dishes.AddRange(services.DishTally.Select(d => new DishCount { Dish = d.Key, Count = d.Value }));
            if (services.Cookbook != null)
                save.Prices.AddRange(services.Cookbook.PriceRecords());
            save.DaysTowardsFee = services.Days.DaysTowardsFee;
            save.Period = services.Days.Period.ToArray();
            save.History.AddRange(services.Days.History);
            return save;
        }

        /// <summary>
        /// The campaign as it is now: <paramref name="loaded"/> (or a new one) with the current
        /// restaurant replaced by <paramref name="restaurant"/>, and the campaign's own fields taken
        /// from the day. Every other restaurant is kept exactly as loaded: idle places don't change.
        /// </summary>
        public static CampaignSave CaptureCampaign(CampaignSave loaded, RestaurantSave restaurant, KitchenServices services)
        {
            var campaign = loaded ?? new CampaignSave();
            campaign.StoreCurrent(restaurant);
            campaign.Balance = services.Days.Balance;
            campaign.StoreLoan(services.Days.Loan);
            campaign.StoreEscalation(services.Boss);
            campaign.RecentLines = services.ReviewLines.ToList();
            if (services.Cookbook != null)
                campaign.Recipes = services.Cookbook.UnlockedIds().ToList();
            return campaign;
        }

        /// <summary>
        /// Puts the furniture where the save had it, before anything binds. Pieces already in the scene
        /// are reused and moved where they can be (so the starting fridge keeps its starter stock), bought
        /// pieces are made from the catalogue, and sold pieces go.
        /// </summary>
        /// <returns>The station for each saved record, in the save's order; null where one couldn't be made.</returns>
        public static Interactable[] ApplyLayout(RestaurantSave save, IReadOnlyList<Interactable> inScene, FurnitureCatalog catalog)
        {
            var placed = new Interactable[save.Stations.Count];
            var unused = new List<Interactable>(inScene);

            // Same name first, then any piece of the same kind.
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 0; i < save.Stations.Count; i++)
                {
                    if (placed[i] != null)
                        continue;
                    var record = save.Stations[i];
                    var match = unused.FirstOrDefault(station =>
                        (station.FurnitureId ?? "") == record.FurnitureId && (pass == 1 || station.name == record.SceneName));
                    if (match == null || (pass == 1 && string.IsNullOrEmpty(record.FurnitureId)))
                        continue;
                    unused.Remove(match);
                    placed[i] = match;
                    Move(match.transform, record, match.transform.position.y);
                }
            }

            // Bought since the scene was built: made from the catalogue, as the shop does.
            for (var i = 0; i < save.Stations.Count; i++)
            {
                var record = save.Stations[i];
                if (placed[i] != null || string.IsNullOrEmpty(record.FurnitureId))
                    continue;
                var entry = catalog != null ? catalog.Find(record.FurnitureId) : null;
                if (entry == null || entry.Prefab == null)
                {
                    Debug.LogWarning($"Saved furniture '{record.FurnitureId}' isn't in the catalogue any more; it's left out.");
                    continue;
                }
                var instance = Object.Instantiate(entry.Prefab);
                instance.name = entry.DisplayName;
                Move(instance.transform, record, 0f);
                placed[i] = instance.GetComponent<Interactable>();
            }

            // Sold: what's for sale and not in the save is gone. Fixed pieces (the sink, the bin) stay.
            foreach (var station in unused)
            {
                if (string.IsNullOrEmpty(station.FurnitureId))
                    continue;
                station.gameObject.SetActive(false); // gone now, for the search that follows
                Object.Destroy(station.gameObject);
            }

            Physics.SyncTransforms();
            return placed;
        }

        /// <summary>Puts back what was on the stations, after they've bound, and the counted things.</summary>
        public static void RestoreContents(RestaurantSave save, Interactable[] placed, KitchenServices services)
        {
            for (var i = 0; i < placed.Length; i++)
            {
                var station = placed[i];
                if (station == null)
                    continue;
                var record = save.Stations[i];
                if (station is ISavedContents contents)
                    contents.ClearForLoad();
                if (station is ISavedCount counted)
                    counted.RestoreCount(record.Count);
                if (station is TableStation table && record.Chairs > 0)
                    table.RestoreChairs(record.Chairs);
                if (station is SinkStation sink)
                    sink.Sink.RestoreBroken(save.BrokenPlates);
            }

            var factory = new KitchenItemFactory(services);
            var loose = new List<Item>();
            foreach (var (record, item) in ItemRecords.Read(save.Items, factory))
            {
                var station = record.Station >= 0 && record.Station < placed.Length ? placed[record.Station] : null;
                if (!(station is ISavedContents contents) || !contents.RestoreItem(record.Slot, item))
                    loose.Add(item);
            }

            // What was in someone's hands, or no longer fits where it was, goes on a free counter.
            foreach (var item in loose)
            {
                var counter = services.Stations.All.OfType<CounterStation>().FirstOrDefault(c => c.IsClear && c.Slot.CanAccept(item));
                if (counter == null || !counter.RestoreItem(0, item))
                {
                    Debug.LogWarning($"No free counter for a saved {item.TypeId}; it's lost.");
                    factory.Discard(item);
                }
            }

            foreach (var member in save.Staff)
                services.Staff.Restore(SaveConversions.ToMember(member));
            services.ReviewHistory.AddRange(save.Reviews);
            foreach (var dish in save.Dishes ?? new System.Collections.Generic.List<DishCount>())
                services.DishTally[dish.Dish] = dish.Count;
            DirtRecords.Read(save.Dirt, i => i >= 0 && i < placed.Length ? placed[i] : null, services.Dirt);
        }

        private static void Move(Transform transform, StationRecord record, float height)
        {
            transform.SetPositionAndRotation(new Vector3(record.X, height, record.Z), Quaternion.Euler(0f, record.Degrees, 0f));
        }
    }
}
