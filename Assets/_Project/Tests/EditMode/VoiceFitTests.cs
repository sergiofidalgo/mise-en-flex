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
        public void RepliesAndAnswersFit()
        {
            var voices = Voices().ToList();
            var replies = voices.SelectMany(v => v.Replies.Values.SelectMany(r => r)).Select(t => ReviewVoices.Fill(t, Long))
                .Where(t => t.Length > ReviewVoices.MaxReplyLength).ToList();
            var answers = voices.SelectMany(v => v.WarmAnswers.Concat(v.DryAnswers)).Select(a => ReviewVoices.Fill(a.Text, Long))
                .Where(t => t.Length > ReviewVoices.MaxAnswerLength).ToList();
            Assert.That(replies, Is.Empty, string.Join("\n", replies));
            Assert.That(answers, Is.Empty, string.Join("\n", answers));
        }

        [Test]
        public void EveryPlaceholderIsKnown()
        {
            var voices = Voices().ToList();
            var all = voices.SelectMany(v => v.Lines.Values.SelectMany(l => l).Select(l => l.Text)
                .Concat(v.Replies.Values.SelectMany(r => r))
                .Concat(v.WarmAnswers.Concat(v.DryAnswers).Select(a => a.Text)));
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
            foreach (Persona persona in System.Enum.GetValues(typeof(Persona)))
            {
                Assert.That(grill.WarmAnswers.Any(a => a.Only == persona), Is.True, $"a warm answer from a {persona}");
                Assert.That(grill.DryAnswers.Any(a => a.Only == persona), Is.True, $"a dry answer from a {persona}");
            }
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
    }
}
