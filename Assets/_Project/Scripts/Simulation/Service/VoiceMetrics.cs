using System;
using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Saving;

namespace Game.Simulation.Service
{
    /// <summary>How a month of a restaurant's reviews and replies reads: does anything repeat too soon or too often?</summary>
    public sealed class VoiceReport
    {
        public List<(int Day, string Text)> Reviews { get; } = new List<(int, string)>();
        public List<(int Day, string Text)> Replies { get; } = new List<(int, string)>();
        public int Days { get; set; }

        /// <summary>How often the most-used text appears.</summary>
        public static int MostUses(IEnumerable<(int Day, string Text)> texts) =>
            texts.GroupBy(t => t.Text).Select(g => g.Count()).DefaultIfEmpty(0).Max();

        /// <summary>The fewest days between two uses of the same text; int.MaxValue if nothing repeats.</summary>
        public static int ShortestGap(IEnumerable<(int Day, string Text)> texts)
        {
            var gap = int.MaxValue;
            foreach (var group in texts.GroupBy(t => t.Text))
            {
                var days = group.Select(t => t.Day).OrderBy(d => d).ToList();
                for (var i = 1; i < days.Count; i++)
                    gap = Math.Min(gap, days[i] - days[i - 1]);
            }
            return gap;
        }

        /// <summary>Share of texts that are used only once.</summary>
        public static float Unique(IEnumerable<(int Day, string Text)> texts)
        {
            var list = texts.ToList();
            return list.Count == 0 ? 1f : list.GroupBy(t => t.Text).Count(g => g.Count() == 1) / (float)list.Count;
        }

        public static IEnumerable<(string Text, int Uses)> Repeated(IEnumerable<(int Day, string Text)> texts, int top) =>
            texts.GroupBy(t => t.Text).Where(g => g.Count() > 1).OrderByDescending(g => g.Count()).Take(top).Select(g => (g.Key, g.Count()));

        public string Summary() =>
            $"{Days} days: {Reviews.Count} reviews ({Unique(Reviews):P0} unique, most-used ×{MostUses(Reviews)}, shortest repeat {Gap(ShortestGap(Reviews))}), " +
            $"{Replies.Count} replies ({Unique(Replies):P0} unique, most-used ×{MostUses(Replies)}, shortest repeat {Gap(ShortestGap(Replies))})";

        private static string Gap(int days) => days == int.MaxValue ? "never" : days == 1 ? "1 day" : $"{days} days";
    }

    /// <summary>
    /// Plays a month of reviews and replies for a restaurant's voice, so repetition is measured, not
    /// guessed (user's call, 2026-11-09: every restaurant's interactions should feel unique). The
    /// month is a busy one on purpose: a realistic mix of complaints and praise, every kind of
    /// guest, returning guests, an owner who replies to most reviews (usually fittingly) and tires
    /// as the day goes on, and the campaign's no-repeat memory.
    /// </summary>
    public static class VoiceMetrics
    {
        // How often each kind of review comes up, roughly as the game produces them.
        private static readonly (ReviewTopic Topic, float Weight)[] Mix =
        {
            (ReviewTopic.Praise, 15f), (ReviewTopic.Decent, 8f), (ReviewTopic.WalkedOut, 15f), (ReviewTopic.SlowService, 12f),
            (ReviewTopic.Overcooked, 10f), (ReviewTopic.Undercooked, 8f), (ReviewTopic.Burnt, 5f), (ReviewTopic.Cold, 5f),
            (ReviewTopic.PaleCrust, 5f), (ReviewTopic.NotRested, 4f), (ReviewTopic.NoSide, 3f), (ReviewTopic.SideOff, 3f),
            (ReviewTopic.BadCut, 3f), (ReviewTopic.NotFresh, 2f), (ReviewTopic.NoMain, 2f),
            (ReviewTopic.Dirty, 3f),
        };

        private static readonly string[] Names = { "Mike", "Jessica", "Tyler", "Ashley", "Brandon", "Megan", "Chris", "Lauren", "Dave", "Brittany", "Jamal", "Keisha", "Carlos", "Maria", "Tony", "Gina", "Bobby", "Linda", "Hank", "Dolores" };
        private static readonly string[] Doneness = { "rare", "medium rare", "medium", "well done" };

        public static VoiceReport Simulate(Voice style, Voice generic, int days = 30, int reviewsPerDay = 4, float replyShare = 0.8f, int seed = 1, int tiredAfter = 3, int fedUpAfter = 6, string restaurant = "The Copper Pan")
        {
            var random = new Random(seed);
            var memory = new LineMemory();
            var book = new GuestBook();
            var posted = new List<ReviewRecord>();
            var report = new VoiceReport { Days = days };
            var total = Mix.Sum(m => m.Weight);

            for (var day = 1; day <= days; day++)
            {
                book.Rebuild(posted, day);
                var repliesToday = 0;
                var walkOutsToday = 0;
                for (var i = 0; i < reviewsPerDay; i++)
                {
                    var roll = (float)random.NextDouble() * total;
                    var topic = Mix.First(m => (roll -= m.Weight) <= 0f || m.Topic == Mix[Mix.Length - 1].Topic).Topic;
                    if (topic == ReviewTopic.WalkedOut)
                        walkOutsToday++;
                    var returning = book.Draw(random);
                    var persona = returning?.Persona ?? ReviewVoices.PickPersona(style, generic, random);
                    // Overcooked means served past what was ordered, undercooked short of it.
                    var ordered = topic == ReviewTopic.Overcooked ? Doneness[random.Next(Doneness.Length - 1)]
                        : topic == ReviewTopic.Undercooked ? Doneness[1 + random.Next(Doneness.Length - 1)]
                        : Doneness[random.Next(Doneness.Length)];
                    var name = returning?.Name ?? $"{Names[random.Next(Names.Length)]} {(char)('A' + random.Next(26))}.";
                    var facts = new ReviewFacts
                    {
                        Main = "steak", Side = "onions", Ordered = ordered,
                        Got = topic == ReviewTopic.Undercooked ? "rare" : topic == ReviewTopic.Overcooked ? "well done" : ordered,
                        Name = name.Split(' ')[0], Restaurant = restaurant,
                        LeftAt = $"{19 + random.Next(2)}:{random.Next(10, 60):00}", WaitMinutes = random.Next(15, 45),
                    };
                    var line = returning != null
                        ? ReviewVoices.ReturnLine(returning.Memory, topic, persona, facts, style, generic, memory, random)
                        : ReviewVoices.Line(topic, persona, facts, style, generic, memory, random);
                    report.Reviews.Add((day, line));

                    var record = new ReviewRecord { Day = day, Guest = name, Topic = (int)topic, Persona = (int)persona, Order = $"steak, {ordered}", WalkedOut = topic == ReviewTopic.WalkedOut };
                    if (random.NextDouble() < replyShare)
                    {
                        var reply = ChooseReply(topic, random);
                        var mood = ReviewVoices.MoodAfter(repliesToday, walkOutsToday, tiredAfter, fedUpAfter);
                        record.Reply = (int)reply;
                        record.ReplyText = ReviewVoices.Reply(reply, topic, facts, mood, style, generic, memory, random);
                        record.ReplyMood = (int)mood;
                        report.Replies.Add((day, record.ReplyText));
                        repliesToday++;
                    }
                    posted.Add(record);
                }
            }
            return report;
        }

        // Most players learn what fits: seven times in ten the fitting reply, otherwise either of the others.
        private static ReviewReply ChooseReply(ReviewTopic topic, Random random)
        {
            var fitting = ReviewReplies.IsComplaint(topic) ? ReviewReply.Apologise : ReviewReply.Thank;
            if (random.NextDouble() < 0.7)
                return fitting;
            var others = ((ReviewReply[])Enum.GetValues(typeof(ReviewReply))).Where(r => r != fitting).ToArray();
            return others[random.Next(others.Length)];
        }
    }
}
