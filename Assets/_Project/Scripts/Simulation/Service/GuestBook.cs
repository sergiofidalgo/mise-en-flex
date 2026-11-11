using System;
using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Saving;

namespace Game.Simulation.Service
{
    /// <summary>What a guest remembers of their last visit: what they wrote and how the owner answered.</summary>
    public enum GuestMemory
    {
        /// <summary>They complained (or walked out) and the owner apologised.</summary>
        Apologised,
        /// <summary>They complained and nobody answered.</summary>
        Ignored,
        /// <summary>They complained and got thanked or argued with.</summary>
        Dismissed,
        /// <summary>They praised it and were thanked.</summary>
        Thanked,
        /// <summary>They praised it and got no reply, or an odd one.</summary>
        Unthanked,
    }

    /// <summary>A guest who has posted before and might come back.</summary>
    public sealed class KnownGuest
    {
        public string Name = "";

        /// <summary>Their name slot and initial, so they're shown in the player's language.</summary>
        public int Slot = -1;
        public string Initial = "";
        public Persona Persona;
        public int LastDay;
        public int Visits;

        /// <summary>The look they came in last time, or -1 if their reviews are older than looks.</summary>
        public int Look = -1;
        public GuestMemory Memory;

        /// <summary>The owner's mood in the reply they got, if they got one.</summary>
        public OwnerMood ReplyMood;
    }

    /// <summary>
    /// The guests who have posted before (ideas.md and decisions, 2026-11-09). Only reviewers are
    /// known, so the book is rebuilt from the review history and needs no save of its own. How
    /// likely each is to return follows how they were treated: an apology that fits brings people
    /// back, being ignored less so, being brushed off least, and a reply written tired or fed up
    /// puts people off. Players aren't told; they see who comes back and what they say.
    /// </summary>
    public sealed class GuestBook
    {
        private readonly List<KnownGuest> _guests = new List<KnownGuest>();

        /// <summary>Chance a reviewing guest is someone who's been before, when anyone might return.</summary>
        public float ReturnShare { get; set; } = 0.3f;

        /// <summary>How willing each memory makes a guest to come back, by <see cref="GuestMemory"/> order.</summary>
        public float[] Willingness { get; set; } = { 3f, 1f, 0.3f, 2f, 1.2f };

        /// <summary>
        /// How a reply's mood changes a guest's willingness to return, by <see cref="OwnerMood"/> order:
        /// a weary reply puts people off a little, a rude one a lot (user's call, 2026-11-09).
        /// </summary>
        public float[] MoodWillingness { get; set; } = { 1f, 0.8f, 0.4f };

        /// <summary>How much likelier a returning guest is to post than a first-timer.</summary>
        public float PostBoost { get; set; } = 1.5f;

        public IReadOnlyList<KnownGuest> Guests => _guests;

        /// <summary>Everyone who posted before today, as of their latest review.</summary>
        public void Rebuild(IEnumerable<ReviewRecord> history, int today)
        {
            _guests.Clear();
            foreach (var visits in history.Where(r => r.Day < today && !string.IsNullOrEmpty(r.Guest)).GroupBy(r => r.Guest))
            {
                var last = visits.OrderBy(r => r.Day).ThenBy(r => r.ArrivedAt).Last();
                _guests.Add(new KnownGuest
                {
                    Name = last.Guest,
                    Slot = last.GuestSlot,
                    Initial = last.GuestInitial,
                    Persona = (Persona)last.Persona,
                    LastDay = last.Day,
                    Look = last.Look,
                    Visits = visits.Count(),
                    Memory = MemoryOf(last),
                    ReplyMood = last.Reply >= 0 ? (OwnerMood)last.ReplyMood : OwnerMood.Fresh,
                });
            }
        }

        /// <summary>
        /// Someone who's been before, drawn by how willing they are to return, or null. Each comes
        /// back at most once a day: a drawn guest leaves the book until it's rebuilt. With
        /// <paramref name="look"/>, only guests who wore that look (or whose look isn't known) can be
        /// drawn, so a returning guest's card shows the same face as before (2026-11-11).
        /// </summary>
        public KnownGuest Draw(Random random, int look = -1)
        {
            if (_guests.Count == 0 || random.NextDouble() >= ReturnShare)
                return null;
            var candidates = look < 0 ? _guests : _guests.Where(g => g.Look < 0 || g.Look == look).ToList();
            var total = candidates.Sum(Weight);
            if (total <= 0f)
                return null;
            var roll = (float)random.NextDouble() * total;
            foreach (var guest in candidates)
            {
                roll -= Weight(guest);
                if (roll <= 0f)
                {
                    _guests.Remove(guest);
                    return guest;
                }
            }
            return null;
        }

        // How willing they are: what they remember, times how the reply sounded.
        public float Weight(KnownGuest guest)
        {
            var index = (int)guest.Memory;
            var willing = Willingness != null && index < Willingness.Length ? Math.Max(0f, Willingness[index]) : 1f;
            var mood = (int)guest.ReplyMood;
            return willing * (MoodWillingness != null && mood < MoodWillingness.Length ? Math.Max(0f, MoodWillingness[mood]) : 1f);
        }

        public static GuestMemory MemoryOf(ReviewRecord review)
        {
            var topic = (ReviewTopic)review.Topic;
            if (ReviewReplies.IsComplaint(topic))
            {
                if (review.Reply < 0)
                    return GuestMemory.Ignored;
                return (ReviewReply)review.Reply == ReviewReply.Apologise ? GuestMemory.Apologised : GuestMemory.Dismissed;
            }
            return review.Reply >= 0 && (ReviewReply)review.Reply == ReviewReply.Thank ? GuestMemory.Thanked : GuestMemory.Unthanked;
        }
    }
}
