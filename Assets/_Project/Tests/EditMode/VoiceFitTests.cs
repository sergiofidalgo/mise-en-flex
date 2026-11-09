using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Every line the voices can write fits its place on the dashboard, with long facts filled in,
    /// and names only facts the writer knows. A line that's too long is cut off with an ellipsis.
    /// </summary>
    public class VoiceFitTests
    {
        private static readonly ReviewFacts Long = new ReviewFacts
        {
            Main = "steak", Side = "onions", Ordered = "medium rare", Got = "medium rare",
            Name = "Isabella", Restaurant = "The Copper Pan", LeftAt = "19:35", WaitMinutes = 45,
        };

        private static IEnumerable<Voice> Voices()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<ServiceConfig>();
            yield return config.GenericVoice();
            foreach (var style in new[] { "Grill", "Steakhouse" })
            {
                var voice = ReviewVoiceDefaults.All().First(v => v.Speaks(style)).ToVoice();
                yield return voice;
            }
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void ReviewLinesFitOnOneLine()
        {
            var tooLong = Voices().SelectMany(v => v.Lines.Values.SelectMany(l => l))
                .Select(l => ReviewVoices.Fill(l.Text, Long))
                .Where(t => t.Length > ReviewVoices.MaxReviewLength).ToList();
            Assert.That(tooLong, Is.Empty, string.Join("\n", tooLong));
        }

        [Test]
        public void RepliesFit()
        {
            var replies = Voices().SelectMany(v => v.Replies.Values.SelectMany(r => r)).Select(t => ReviewVoices.Fill(t, Long))
                .Where(t => t.Length > ReviewVoices.MaxReplyLength).ToList();
            Assert.That(replies, Is.Empty, string.Join("\n", replies));
        }

        [Test]
        public void ReturningGuestsLinesFitOnOneLine()
        {
            var tooLong = Voices().SelectMany(v => v.ReturnLines.Values.SelectMany(l => l))
                .Select(l => ReviewVoices.Fill(l.Text, Long))
                .Where(t => t.Length > ReviewVoices.MaxReviewLength).ToList();
            Assert.That(tooLong, Is.Empty, string.Join("\n", tooLong));
        }

        [Test]
        public void EveryMemoryHasALineForABetterAndAWorseNight()
        {
            var generic = Voices().First();
            foreach (GuestMemory memory in System.Enum.GetValues(typeof(GuestMemory)))
            foreach (var better in new[] { true, false })
                Assert.That(generic.ReturnLines.TryGetValue((memory, better), out var lines) && lines.Any(l => l.Only == null), Is.True, $"a generic line for {memory}, {(better ? "better" : "worse")}");
        }

        [Test]
        public void EveryPlaceholderIsKnown()
        {
            var voices = Voices().ToList();
            var all = voices.SelectMany(v => v.Lines.Values.SelectMany(l => l).Select(l => l.Text)
                .Concat(v.Replies.Values.SelectMany(r => r))
                .Concat(v.ReturnLines.Values.SelectMany(l => l).Select(l => l.Text))
                .Concat(v.ReplyParts.Values.SelectMany(l => l).Select(l => l.Text)));
            var unknown = all.Select(t => ReviewVoices.Fill(t, Long)).Where(t => t.Contains("{") || t.Contains("}")).ToList();
            Assert.That(unknown, Is.Empty, string.Join("\n", unknown));
        }

        [Test]
        public void TheGrillCoversEveryTopicAndEveryReply()
        {
            var grill = ReviewVoiceDefaults.All().First(v => v.Speaks("Grill")).ToVoice();
            foreach (ReviewTopic topic in System.Enum.GetValues(typeof(ReviewTopic)))
                Assert.That(grill.Lines.TryGetValue(topic, out var lines) && lines.Count >= 2, Is.True, $"at least two grill lines for {topic}");
            foreach (ReviewReply reply in System.Enum.GetValues(typeof(ReviewReply)))
            foreach (TopicGroup group in System.Enum.GetValues(typeof(TopicGroup)))
                Assert.That(grill.Replies.ContainsKey((reply, group)), Is.True, $"a grill reply for {reply} to {group}");
        }

        [Test]
        public void AnInfluencersHashtagNeverPushesALineOver()
        {
            var voice = new Voice
            {
                Lines = new Dictionary<ReviewTopic, List<VoiceLine>> { [ReviewTopic.Praise] = new List<VoiceLine> { new VoiceLine(new string('x', 40)) } },
                Hashtags = new List<string> { "#averylonghashtag", "#ok" },
            };
            for (var seed = 0; seed < 10; seed++)
                Assert.That(ReviewVoices.Line(ReviewTopic.Praise, Persona.Influencer, Long, voice, null, null, new System.Random(seed)).Length, Is.LessThanOrEqualTo(ReviewVoices.MaxReviewLength));
        }

        [Test]
        public void ReplyPartsFitTwoLinesTogether()
        {
            var parts = Voices().SelectMany(v => v.ReplyParts).ToList();
            var openers = parts.Where(p => p.Key.Item3 == ReplyPart.Opener).SelectMany(p => p.Value).Select(l => ReviewVoices.Fill(l.Text, Long)).Where(t => t.Length > 52).ToList();
            var closers = parts.Where(p => p.Key.Item3 == ReplyPart.Closer).SelectMany(p => p.Value).Select(l => ReviewVoices.Fill(l.Text, Long)).Where(t => t.Length > 56).ToList();
            var bodies = parts.Where(p => p.Key.Item3 == ReplyPart.Body).SelectMany(p => p.Value).Select(l => ReviewVoices.Fill(l.Text, Long)).Where(t => t.Length > 90).ToList();
            Assert.That(openers.Concat(closers).Concat(bodies), Is.Empty, string.Join("\n", openers.Concat(closers).Concat(bodies)));
        }
    }
}
