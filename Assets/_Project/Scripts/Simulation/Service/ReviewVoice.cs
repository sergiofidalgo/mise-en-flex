using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Simulation.Service
{
    /// <summary>Who's writing: each kind of guest has its own lines and habits (ideas.md, 2026-11-09).</summary>
    public enum Persona
    {
        Regular,
        Foodie,
        Influencer,
        Critic,
        Tourist,
        Local,
    }

    /// <summary>What a review is broadly about, for choosing a reply that answers it.</summary>
    public enum TopicGroup
    {
        Praise,
        Food,
        Wait,
        /// <summary>Cooked more or less than ordered: replies here may mention what was ordered.</summary>
        Doneness,
    }

    /// <summary>A line someone might write, optionally only for one kind of guest.</summary>
    public sealed class VoiceLine
    {
        public VoiceLine(string text, Persona? only = null)
        {
            Text = text;
            Only = only;
        }

        public string Text { get; }
        public Persona? Only { get; }

        public bool Suits(Persona persona) => Only == null || Only == persona;
    }

    /// <summary>
    /// How one kind of restaurant is talked about: its guests' lines per topic, the owner's replies,
    /// how guests answer a reply that fits or doesn't, which guests it draws, and its hashtags. A
    /// style's voice is used together with the generic one, so a style only needs what's its own.
    /// </summary>
    public sealed class Voice
    {
        public string Style { get; set; } = "";
        public Dictionary<ReviewTopic, List<VoiceLine>> Lines { get; set; } = new Dictionary<ReviewTopic, List<VoiceLine>>();
        public Dictionary<(ReviewReply, TopicGroup), List<string>> Replies { get; set; } = new Dictionary<(ReviewReply, TopicGroup), List<string>>();
        public List<string> Hashtags { get; set; } = new List<string>();

        /// <summary>What a returning guest writes, by what they remember and whether tonight went better.</summary>
        public Dictionary<(GuestMemory, bool), List<VoiceLine>> ReturnLines { get; set; } = new Dictionary<(GuestMemory, bool), List<VoiceLine>>();

        /// <summary>How often each persona visits, by <see cref="Persona"/> order; empty means regulars only.</summary>
        public float[] PersonaWeights { get; set; } = new float[0];
    }

    /// <summary>The facts of one visit that lines can mention. Empty ones rule out the lines that need them.</summary>
    public sealed class ReviewFacts
    {
        public string Main = "";
        public string Side = "";
        public string Ordered = "";
        public string Got = "";
        public string Name = "";
        public string Restaurant = "";
        public string LeftAt = "";

        /// <summary>Minutes waited, or 0 when not worth mentioning.</summary>
        public int WaitMinutes;
    }

    /// <summary>
    /// The lines used lately, so the same words don't come back for a while. Kept with the campaign:
    /// a deck that's shuffled across days, not reset each one.
    /// </summary>
    public sealed class LineMemory
    {
        private readonly LinkedList<string> _recent = new LinkedList<string>();
        private readonly HashSet<string> _lookup = new HashSet<string>();

        public LineMemory(int capacity = 80)
        {
            Capacity = Math.Max(1, capacity);
        }

        public int Capacity { get; }

        public bool WasUsed(string line) => _lookup.Contains(line);

        /// <summary>How long ago a line was used: 0 for the latest, larger for older, int.MaxValue if never.</summary>
        public int Age(string line)
        {
            var age = 0;
            for (var node = _recent.Last; node != null; node = node.Previous, age++)
            {
                if (node.Value == line)
                    return age;
            }
            return int.MaxValue;
        }

        public void Use(string line)
        {
            if (_lookup.Contains(line))
                _recent.Remove(line);
            _recent.AddLast(line);
            _lookup.Add(line);
            while (_recent.Count > Capacity)
            {
                _lookup.Remove(_recent.First.Value);
                _recent.RemoveFirst();
            }
        }

        public List<string> ToList() => _recent.ToList();

        public void Load(IEnumerable<string> lines)
        {
            _recent.Clear();
            _lookup.Clear();
            if (lines == null)
                return;
            foreach (var line in lines)
                Use(line);
        }
    }

    /// <summary>
    /// Writes what guests and owners say from a style's voice and the generic one: lines that fit the
    /// facts and never the same words twice in a while, owners' replies written for what the review
    /// was about, and returning guests' lines that remember how they were treated.
    /// </summary>
    public static class ReviewVoices
    {
        /// <summary>
        /// Characters a review line can have and still fit on one line of the dashboard's review card
        /// (Lora Italic at 30 px in 640 px, about 0.46 em a character). Replies and answers have more room.
        /// </summary>
        public const int MaxReviewLength = 44;
        public const int MaxReplyLength = 72;

        private static readonly string[] Placeholders = { "{main}", "{side}", "{ordered}", "{got}", "{name}", "{restaurant}", "{left}", "{wait}" };

        public static TopicGroup GroupOf(ReviewTopic topic) => topic switch
        {
            ReviewTopic.Praise => TopicGroup.Praise,
            ReviewTopic.Decent => TopicGroup.Praise,
            ReviewTopic.SlowService => TopicGroup.Wait,
            ReviewTopic.WalkedOut => TopicGroup.Wait,
            ReviewTopic.Undercooked => TopicGroup.Doneness,
            ReviewTopic.Overcooked => TopicGroup.Doneness,
            _ => TopicGroup.Food,
        };

        /// <summary>A guest drawn from the style's mix (the generic mix if the style has none).</summary>
        public static Persona PickPersona(Voice style, Voice generic, Random random)
        {
            var weights = style?.PersonaWeights is { Length: > 0 } own ? own : generic?.PersonaWeights ?? new float[0];
            var total = weights.Sum(w => Math.Max(0f, w));
            if (total <= 0f)
                return Persona.Regular;
            var roll = (float)random.NextDouble() * total;
            for (var i = 0; i < weights.Length && i <= (int)Persona.Local; i++)
            {
                roll -= Math.Max(0f, weights[i]);
                if (roll <= 0f)
                    return (Persona)i;
            }
            return Persona.Regular;
        }

        /// <summary>A review line, or empty when neither voice has one that fits.</summary>
        public static string Line(ReviewTopic topic, Persona persona, ReviewFacts facts, Voice style, Voice generic, LineMemory memory, Random random)
        {
            var candidates = LinesFor(style, topic, persona).Concat(LinesFor(generic, topic, persona));
            var line = Pick(candidates, facts, memory, random);
            if (line.Length > 0 && persona == Persona.Influencer)
            {
                // Only a hashtag that still fits on the card's one line.
                var tags = ((style?.Hashtags.Count > 0 ? style.Hashtags : generic?.Hashtags) ?? new List<string>())
                    .Where(t => line.Length + 1 + t.Length <= MaxReviewLength).ToList();
                if (tags.Count > 0)
                    line += " " + tags[random.Next(tags.Count)];
            }
            return line;
        }

        /// <summary>
        /// A returning guest's line, which remembers the last visit: how they were treated, and
        /// whether tonight was better (praise or decent) or not. Empty when neither voice has one.
        /// </summary>
        public static string ReturnLine(GuestMemory memory, ReviewTopic topic, Persona persona, ReviewFacts facts, Voice style, Voice generic, LineMemory used, Random random)
        {
            var key = (memory, !ReviewReplies.IsComplaint(topic));
            IEnumerable<VoiceLine> Of(Voice voice) =>
                voice != null && voice.ReturnLines.TryGetValue(key, out var lines) ? lines.Where(l => l.Suits(persona)) : Enumerable.Empty<VoiceLine>();
            return Pick(Of(style).Concat(Of(generic)), facts, used, random);
        }

        /// <summary>
        /// The owner's reply, written in the style's voice for what the review was about. One reply per
        /// review (user's call, 2026-11-09): its words show what kind of reply it is.
        /// </summary>
        public static string Reply(ReviewReply reply, ReviewTopic topic, ReviewFacts facts, Voice style, Voice generic, LineMemory memory, Random random)
        {
            var group = GroupOf(topic);
            var owners = RepliesFor(style, (reply, group)).Concat(RepliesFor(generic, (reply, group)));
            // A doneness complaint is also about the food, so the food replies answer it too.
            if (group == TopicGroup.Doneness)
                owners = owners.Concat(RepliesFor(style, (reply, TopicGroup.Food))).Concat(RepliesFor(generic, (reply, TopicGroup.Food)));
            return Pick(owners.Select(t => new VoiceLine(t)), facts, memory, random);
        }

        /// <summary>Fills in the facts: {main}, {side}, {ordered}, {got}, {name}, {restaurant}, {left} and {wait}.</summary>
        public static string Fill(string text, ReviewFacts facts) => Capitalised(text
            .Replace("{main}", facts.Main)
            .Replace("{side}", facts.Side)
            .Replace("{ordered}", facts.Ordered)
            .Replace("{got}", facts.Got)
            .Replace("{name}", facts.Name)
            .Replace("{restaurant}", facts.Restaurant)
            .Replace("{left}", facts.LeftAt)
            .Replace("{wait}", facts.WaitMinutes.ToString()));

        // A line that opens with a fact ("{ordered}, exactly.") starts with a capital all the same.
        private static string Capitalised(string text) =>
            text.Length > 0 && char.IsLower(text[0]) ? char.ToUpperInvariant(text[0]) + text.Substring(1) : text;

        /// <summary>True if every fact the line mentions is known.</summary>
        public static bool CanFill(string text, ReviewFacts facts)
        {
            foreach (var placeholder in Placeholders)
            {
                if (!text.Contains(placeholder))
                    continue;
                var known = placeholder switch
                {
                    "{main}" => facts.Main,
                    "{side}" => facts.Side,
                    "{ordered}" => facts.Ordered,
                    "{got}" => facts.Got,
                    "{name}" => facts.Name,
                    "{restaurant}" => facts.Restaurant,
                    "{left}" => facts.LeftAt,
                    _ => facts.WaitMinutes > 0 ? "x" : "",
                };
                if (string.IsNullOrEmpty(known))
                    return false;
            }
            return true;
        }

        // Lines not used lately first, at random among them; if every one was used, the one used longest ago.
        // Lines that mention a fact are favoured twice over, because they're the ones that feel on-point.
        private static string Pick(IEnumerable<VoiceLine> candidates, ReviewFacts facts, LineMemory memory, Random random)
        {
            var usable = candidates.Where(c => !string.IsNullOrEmpty(c.Text) && CanFill(c.Text, facts)).Select(c => c.Text).Distinct().ToList();
            if (usable.Count == 0)
                return string.Empty;
            var fresh = usable.Where(t => memory == null || !memory.WasUsed(t)).ToList();
            string chosen;
            if (fresh.Count > 0)
            {
                var weighted = fresh.SelectMany(t => Specific(t) ? new[] { t, t } : new[] { t }).ToList();
                chosen = weighted[random.Next(weighted.Count)];
            }
            else
            {
                chosen = usable.OrderByDescending(t => memory.Age(t)).First();
            }
            memory?.Use(chosen);
            return Fill(chosen, facts);
        }

        private static bool Specific(string text) => Placeholders.Any(text.Contains);

        private static IEnumerable<VoiceLine> LinesFor(Voice voice, ReviewTopic topic, Persona persona) =>
            voice != null && voice.Lines.TryGetValue(topic, out var lines) ? lines.Where(l => l.Suits(persona)) : Enumerable.Empty<VoiceLine>();

        private static IEnumerable<string> RepliesFor(Voice voice, (ReviewReply, TopicGroup) key) =>
            voice != null && voice.Replies.TryGetValue(key, out var lines) ? lines : Enumerable.Empty<string>();
    }
}
