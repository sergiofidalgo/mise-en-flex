using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Saving;

namespace Game.Simulation.Campaign
{
    /// <summary>
    /// Which kitchen scene a campaign opens (Phase 6, milestone 3): each playable restaurant has its
    /// own, generated from its definition. A restaurant without one yet (a lot that's only data until
    /// 6b or Phase 7) opens the first kitchen, the grill's, as every restaurant did before.
    /// </summary>
    public static class KitchenScenes
    {
        /// <summary>The scene the campaign's current restaurant plays in, or <paramref name="fallback"/>.</summary>
        public static string For(CampaignSave campaign, IEnumerable<RestaurantSettings> lots, string fallback) =>
            For(campaign?.Current?.DefinitionId, lots, fallback);

        /// <summary>The scene the restaurant with this definition id plays in, or <paramref name="fallback"/>.</summary>
        public static string For(string definitionId, IEnumerable<RestaurantSettings> lots, string fallback)
        {
            var lot = Find(definitionId, lots);
            return lot != null && !string.IsNullOrEmpty(lot.KitchenScene) ? lot.KitchenScene : fallback ?? "";
        }

        /// <summary>The lot with this id, or null.</summary>
        public static RestaurantSettings Find(string definitionId, IEnumerable<RestaurantSettings> lots)
        {
            if (string.IsNullOrEmpty(definitionId) || lots == null)
                return null;
            return lots.FirstOrDefault(l => l != null && l.Id == definitionId);
        }

        /// <summary>Every distinct kitchen scene among the lots, in the lots' order.</summary>
        public static List<string> All(IEnumerable<RestaurantSettings> lots) =>
            (lots ?? Enumerable.Empty<RestaurantSettings>())
                .Where(l => l != null && !string.IsNullOrEmpty(l.KitchenScene))
                .Select(l => l.KitchenScene)
                .Distinct()
                .ToList();
    }
}
