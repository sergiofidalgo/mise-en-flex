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

        /// <summary>Replies plus walk-outs in a day after which the owner sounds tired.</summary>
        public int TiredAfter { get; set; } = 3;

        /// <summary>…and after which they're fed up, and it shows.</summary>
        public int FedUpAfter { get; set; } = 6;
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

        /// <summary>
        /// Writes the reply as posted, in the style's voice and about what the review said: one reply
        /// per review, whose words show what kind of reply it is (decision 2026-11-09). The same review
        /// and reply always give the same words.
        /// </summary>
        /// <param name="name">The guest's first name as the player sees it (their language's); their saved name if null.</param>
        public static void WriteReply(ReviewRecord review, ReviewReply reply, Voice style, Voice generic, LineMemory memory, string restaurant, OwnerMood mood = OwnerMood.Fresh, string name = null)
        {
            var order = (review.Order ?? "").Split(',');
            var facts = new ReviewFacts
            {
                Main = order[0].Trim(),
                Ordered = order.Length > 1 ? order[1].Trim() : "",
                Name = name ?? (review.Guest ?? "").Split(' ')[0],
                Restaurant = restaurant ?? "",
            };
            var seed = unchecked(review.Day * 7919 + StableHash(review.Guest) * 31 + (int)reply);
            review.ReplyText = ReviewVoices.Reply(reply, (ReviewTopic)review.Topic, facts, mood, style, generic, memory, new System.Random(seed));
            review.ReplyMood = (int)mood;
        }

        // string.GetHashCode differs between runs; this doesn't.
        private static int StableHash(string text)
        {
            var hash = 17;
            foreach (var c in text ?? "")
                hash = unchecked(hash * 31 + c);
            return hash & 0x7fffffff;
        }

        /// <summary>The restaurant's rating: every review so far, after replies. Null with none.</summary>
        public static float? Rating(IEnumerable<ReviewRecord> reviews)
        {
            var list = reviews.ToList();
            return list.Count > 0 ? list.Average(r => r.RatedStars) : (float?)null;
        }
    }
}
