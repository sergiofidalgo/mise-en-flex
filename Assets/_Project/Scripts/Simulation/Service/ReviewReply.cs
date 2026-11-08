using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Saving;

namespace Game.Simulation.Service
{
    /// <summary>The canned replies an owner can post under a review (decision 2026-11-08).</summary>
    public enum ReviewReply
    {
        Apologise,
        Thank,
        StandYourGround,
    }

    public sealed class ReplySettings
    {
        /// <summary>Stars a fitting reply adds to how the review counts in the rating.</summary>
        public float FittingNudge { get; set; } = 0.25f;

        /// <summary>Stars a poor reply takes away.</summary>
        public float PoorNudge { get; set; } = 0.25f;
    }

    /// <summary>
    /// What a reply does: apologising fits a complaint and thanking fits praise, while standing your
    /// ground never fits. Players aren't told; they see it in the rating (learn by playing).
    /// </summary>
    public static class ReviewReplies
    {
        public static bool IsComplaint(ReviewTopic topic) => topic != ReviewTopic.Praise && topic != ReviewTopic.Decent;

        public static bool Fits(ReviewReply reply, ReviewTopic topic) => reply switch
        {
            ReviewReply.Apologise => IsComplaint(topic),
            ReviewReply.Thank => !IsComplaint(topic),
            _ => false,
        };

        /// <summary>Posts a reply, once. Returns false if this review already has one.</summary>
        public static bool Reply(ReviewRecord review, ReviewReply reply, ReplySettings settings)
        {
            if (review.Reply >= 0)
                return false;
            review.Reply = (int)reply;
            review.Nudge = Fits(reply, (ReviewTopic)review.Topic) ? settings.FittingNudge : -settings.PoorNudge;
            return true;
        }

        /// <summary>The restaurant's rating: every review so far, after replies. Null with none.</summary>
        public static float? Rating(IEnumerable<ReviewRecord> reviews)
        {
            var list = reviews.ToList();
            return list.Count > 0 ? list.Average(r => r.RatedStars) : (float?)null;
        }
    }
}
