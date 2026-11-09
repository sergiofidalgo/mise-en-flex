using System.Linq;
using Game.Data;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// A month in each restaurant's voice reads as a month, not a loop (user's call, 2026-11-09).
    /// VoiceMetrics plays 30 busy days: four reviews a day, most of them answered, guests coming back.
    /// When these fail, the voice needs more lines, not looser targets.
    /// </summary>
    public class VoiceMetricsTests
    {
        private const int Days = 30;
        private const int MaxReplyUses = 2;
        private const int MinReplyGapDays = 7;
        private const int MaxReviewUses = 3;
        private const int MinReviewGapDays = 5;

        private static readonly int[] Seeds = { 1, 2, 3 };

        private static Voice Generic()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<ServiceConfig>();
            var voice = config.GenericVoice();
            UnityEngine.Object.DestroyImmediate(config);
            return voice;
        }

        private static Voice Grill() => ReviewVoiceDefaults.All().First(v => v.Speaks("Grill")).ToVoice();

        [Test]
        public void TheGrillsRepliesDontRepeatInAMonth([ValueSource(nameof(Seeds))] int seed)
        {
            var report = VoiceMetrics.Simulate(Grill(), Generic(), Days, seed: seed);
            var repeated = string.Join("\n", VoiceReport.Repeated(report.Replies, 5).Select(r => $"×{r.Uses} {r.Text}"));
            Assert.That(VoiceReport.MostUses(report.Replies), Is.LessThanOrEqualTo(MaxReplyUses), report.Summary() + "\n" + repeated);
            Assert.That(VoiceReport.ShortestGap(report.Replies), Is.GreaterThanOrEqualTo(MinReplyGapDays), report.Summary() + "\n" + repeated);
        }

        [Test]
        public void TheGrillsReviewsDontRepeatInAMonth([ValueSource(nameof(Seeds))] int seed)
        {
            var report = VoiceMetrics.Simulate(Grill(), Generic(), Days, seed: seed);
            var repeated = string.Join("\n", VoiceReport.Repeated(report.Reviews, 5).Select(r => $"×{r.Uses} {r.Text}"));
            Assert.That(VoiceReport.MostUses(report.Reviews), Is.LessThanOrEqualTo(MaxReviewUses), report.Summary() + "\n" + repeated);
            Assert.That(VoiceReport.ShortestGap(report.Reviews), Is.GreaterThanOrEqualTo(MinReviewGapDays), report.Summary() + "\n" + repeated);
        }

        [Test]
        public void RepliesComeInMoreThanOneLength()
        {
            var report = VoiceMetrics.Simulate(Grill(), Generic(), Days, seed: 1);
            var lengths = report.Replies.Select(r => r.Text.Length).ToList();
            Assert.That(lengths.Count(l => l <= ReviewVoices.MaxReplyLength), Is.GreaterThan(lengths.Count / 5), "some short");
            Assert.That(lengths.Count(l => l > ReviewVoices.MaxReplyLength), Is.GreaterThan(lengths.Count / 5), "some long");
            Assert.That(lengths.Max(), Is.LessThanOrEqualTo(ReviewVoices.MaxComposedReplyLength), "never past two lines");
        }

        [Test]
        public void ALongDayShowsInTheReplies()
        {
            Assert.That(ReviewVoices.MoodAfter(0, 0, 3, 6), Is.EqualTo(OwnerMood.Fresh));
            Assert.That(ReviewVoices.MoodAfter(2, 1, 3, 6), Is.EqualTo(OwnerMood.Tired), "walk-outs wear on the owner too");
            Assert.That(ReviewVoices.MoodAfter(6, 0, 3, 6), Is.EqualTo(OwnerMood.FedUp));
            var fedUp = Enumerable.Range(0, 40)
                .Select(seed => ReviewVoices.Reply(ReviewReply.StandYourGround, ReviewTopic.Overcooked, new ReviewFacts { Main = "steak", Ordered = "medium", Name = "Tony" }, OwnerMood.FedUp, Grill(), Generic(), null, new System.Random(seed)))
                .ToList();
            Assert.That(fedUp.Any(r => r.Contains("thermometer") || r.Contains("laminated") || r.Contains("cafeteria") || r.Contains("somewhere else") || r.Contains("diner")), Is.True, "a fed-up owner gets rude");
        }
    }
}
