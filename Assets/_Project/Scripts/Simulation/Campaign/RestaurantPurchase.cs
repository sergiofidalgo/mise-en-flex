using System;
using Game.Simulation.Saving;

namespace Game.Simulation.Campaign
{
    /// <summary>
    /// Buying restaurants on the city miniature and choosing which one to play (Phase 6, milestone 2,
    /// game-design §15.2). Plain rules over the campaign save; the office saves straight after, as
    /// anything done there does.
    /// </summary>
    public static class RestaurantPurchase
    {
        /// <summary>
        /// A campaign as it starts in the office: owning nothing, with <paramref name="loan"/> borrowed
        /// from the boss, enough to buy the grill and stock its first morning (decision 2026-11-10).
        /// The loan is both in the pocket and owed: milestone 4 takes it back in instalments.
        /// </summary>
        public static CampaignSave NewCampaign(float loan) => new CampaignSave
        {
            Balance = loan,
            Loan = loan,
        };

        /// <summary>True if the campaign already owns the restaurant made from that lot.</summary>
        public static bool Owns(CampaignSave campaign, RestaurantSettings lot) =>
            campaign != null && lot != null && campaign.Find(lot.Id) != null;

        /// <summary>
        /// True if <paramref name="name"/> is the lot's own name: keeping it keeps its old reviews
        /// (game-design §15.2). Spacing and letter case don't make a new name.
        /// </summary>
        public static bool KeepsOldName(RestaurantSettings lot, string name) =>
            lot != null && string.Equals(RestaurantNaming.Clean(name), RestaurantNaming.Clean(lot.Name), StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Buys a lot: adds its record to the campaign and spends its price from the campaign's
        /// money. Money may go below zero, which is owed to the boss (decision 2026-11-08), so a lot
        /// is never refused for its price: players find out by playing. The first restaurant bought
        /// becomes the current one; later ones wait until chosen.
        /// </summary>
        /// <param name="name">The name to trade under; empty (or the lot's own) keeps the old name and its reviews.</param>
        /// <param name="style">The style chosen; ignored for a lot with a fixed style, and empty keeps the previous one.</param>
        public static RestaurantSave Buy(CampaignSave campaign, RestaurantSettings lot, string name, string style = null)
        {
            if (campaign == null)
                throw new ArgumentNullException(nameof(campaign));
            if (lot == null)
                throw new ArgumentNullException(nameof(lot));
            if (Owns(campaign, lot))
                throw new InvalidOperationException($"The campaign already owns '{lot.Id}'.");

            var clean = RestaurantNaming.Clean(name);
            var keep = clean.Length == 0 || KeepsOldName(lot, clean);
            var record = lot.NewRecord(keep);
            record.Name = keep ? lot.Name ?? "" : clean;
            record.StyleName = lot.FixedStyle || string.IsNullOrWhiteSpace(style) ? lot.PreviousStyle ?? "" : style.Trim();
            campaign.Balance -= lot.Price;
            return campaign.Add(record);
        }

        /// <summary>
        /// Makes an owned restaurant the one played next. False (and nothing changes) if the campaign
        /// doesn't own it.
        /// </summary>
        public static bool Choose(CampaignSave campaign, string definitionId)
        {
            if (campaign?.Restaurants == null)
                return false;
            var index = campaign.Restaurants.FindIndex(r => r.DefinitionId == definitionId);
            if (index < 0)
                return false;
            campaign.Switch(index);
            return true;
        }

        /// <summary>
        /// True for a restaurant bought but never played: it has no room saved yet, so its kitchen
        /// opens as built, on day 1, rather than continuing from a record. Every played day saves
        /// the room's stations, so a played restaurant always has some.
        /// </summary>
        public static bool NeverOpened(RestaurantSave restaurant) =>
            restaurant != null && (restaurant.Stations == null || restaurant.Stations.Count == 0)
            && (restaurant.History == null || restaurant.History.Count == 0);
    }
}
