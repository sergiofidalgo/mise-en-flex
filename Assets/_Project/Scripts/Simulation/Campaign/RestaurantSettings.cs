using System;
using System.Collections.Generic;
using Game.Simulation.Saving;

namespace Game.Simulation.Campaign
{
    /// <summary>How big a restaurant is (game-design §8.2): size is a tradeoff, chosen by which one you buy.</summary>
    public enum RestaurantSize
    {
        /// <summary>A counter with a few seats, like the first grill.</summary>
        Counter,
        Small,
        /// <summary>A mid-size dining room, the second size playable in 6a.</summary>
        DiningRoom,
        Large,
    }

    /// <summary>A review the restaurant had under its previous owner, kept if players keep its name.</summary>
    public sealed class OldReview
    {
        public float Stars { get; set; }
        public string Line { get; set; } = "";
        public string Guest { get; set; } = "";
    }

    /// <summary>
    /// One restaurant on the city miniature, from its <c>RestaurantDefinition</c> (Phase 6, decision
    /// 2026-11-10): what it is before anyone buys it. The record of what players have done with it is
    /// a <see cref="RestaurantSave"/>.
    /// </summary>
    public sealed class RestaurantSettings
    {
        /// <summary>Stable id the save keeps (<see cref="RestaurantSave.DefinitionId"/>); never change it once shipped.</summary>
        public string Id { get; set; } = "";

        /// <summary>The name it trades under when bought; players may keep it with its reviews, or rename it.</summary>
        public string Name { get; set; } = "";

        public RestaurantSize Size { get; set; }

        /// <summary>Guests it can seat at once with the furniture it comes with.</summary>
        public int Seats { get; set; }

        /// <summary>What buying it costs, on the miniature (milestone 2).</summary>
        public float Price { get; set; }

        /// <summary>Times the base fee: a bigger place on a better street owes the boss more.</summary>
        public float FeeScale { get; set; } = 1f;

        /// <summary>Furniture catalogue ids of the appliances left by the previous owner.</summary>
        public IReadOnlyList<string> InheritedAppliances { get; set; } = Array.Empty<string>();

        /// <summary>What it was before: its style if players keep it (the first grill's is fixed).</summary>
        public string PreviousStyle { get; set; } = "";

        public IReadOnlyList<OldReview> OldReviews { get; set; } = Array.Empty<OldReview>();

        /// <summary>The street it's on, also the area the slot card and reviews name.</summary>
        public string Street { get; set; } = "";

        /// <summary>The stalls on its street (e.g. "Butcher"): where its stock is bought.</summary>
        public IReadOnlyList<string> Vendors { get; set; } = Array.Empty<string>();

        /// <summary>The fee this restaurant owes, from the campaign's base fee.</summary>
        public Days.FeeSettings ScaleFee(Days.FeeSettings baseFee) => new Days.FeeSettings
        {
            Amount = baseFee.Amount * FeeScale,
            EveryDays = baseFee.EveryDays,
        };

        /// <summary>
        /// The record of a restaurant just bought, before its first day: its name and style as it was
        /// sold, and its old reviews if <paramref name="keepOldReviews"/> (keeping the name keeps them).
        /// </summary>
        public RestaurantSave NewRecord(bool keepOldReviews = true)
        {
            var record = new RestaurantSave
            {
                DefinitionId = Id,
                Name = Name,
                StyleName = PreviousStyle,
                Area = Street,
                Day = 1,
            };
            if (keepOldReviews)
            {
                // Written before the campaign started: day 0.
                foreach (var old in OldReviews)
                    record.Reviews.Add(new ReviewRecord { Day = 0, Stars = old.Stars, Line = old.Line ?? "", Guest = old.Guest ?? "" });
            }
            return record;
        }
    }
}
