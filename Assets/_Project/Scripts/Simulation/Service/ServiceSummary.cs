using System.Collections.Generic;
using System.Linq;

namespace Game.Simulation.Service
{
    /// <summary>
    /// The end-of-service numbers: what went well and what cost you. A snapshot taken from a
    /// <see cref="ServiceSimulation"/>; Phase 3's Review phase will build on it.
    /// </summary>
    public sealed class ServiceSummary
    {
        public int Served { get; private set; }
        public int AsOrdered { get; private set; }
        public int WalkOuts { get; private set; }
        public int TurnedAway { get; private set; }

        public float Money { get; private set; }
        public float Payments { get; private set; }
        public float Tips { get; private set; }
        public float WalkOutLosses { get; private set; }

        /// <summary>Average stars over every review, walk-outs included; null with no reviews.</summary>
        public float? Rating { get; private set; }
        public int ReviewCount { get; private set; }

        /// <summary>The highest and lowest rated reviews; null with no reviews.</summary>
        public Review Best { get; private set; }
        public Review Worst { get; private set; }

        /// <summary>A review about the most common complaint, or null if nobody complained.</summary>
        public Review TopComplaint { get; private set; }
        public int TopComplaintCount { get; private set; }

        public static ServiceSummary From(ServiceSimulation service)
        {
            var served = service.Customers.Where(c => c.Satisfaction != null).ToList();
            var reviews = service.Reviews;
            var complaints = reviews
                .Where(r => !ReviewTopics.IsPraise(r.Topic))
                .GroupBy(r => r.Topic)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            return new ServiceSummary
            {
                Served = served.Count,
                AsOrdered = served.Count(c => c.Satisfaction.Match == OrderMatch.Matches),
                WalkOuts = service.WalkOuts,
                TurnedAway = service.LostCovers - service.WalkOuts,
                Money = service.Money,
                Tips = service.Tips,
                Payments = service.Money - service.Tips + service.WalkOutLosses,
                WalkOutLosses = service.WalkOutLosses,
                Rating = reviews.Count > 0 ? reviews.Average(r => r.Stars) : (float?)null,
                ReviewCount = reviews.Count,
                Best = Highest(reviews),
                Worst = Lowest(reviews),
                TopComplaint = complaints?.First(),
                TopComplaintCount = complaints?.Count() ?? 0,
            };
        }

        private static Review Highest(IReadOnlyList<Review> reviews) => reviews.OrderByDescending(r => r.Stars).FirstOrDefault();

        private static Review Lowest(IReadOnlyList<Review> reviews) => reviews.OrderBy(r => r.Stars).FirstOrDefault();
    }
}
