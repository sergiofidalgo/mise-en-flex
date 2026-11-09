using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Simulation.Saving;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>The review and reply writer (ideas.md and decisions, 2026-11-09).</summary>
    public class ReviewVoiceTests
    {
        private static Voice VoiceWith(ReviewTopic topic, params string[] lines) => new Voice
        {
            Lines = new Dictionary<ReviewTopic, List<VoiceLine>> { [topic] = lines.Select(l => new VoiceLine(l)).ToList() },
        };

        private static ReviewFacts Steak() => new ReviewFacts { Main = "steak", Ordered = "medium", Got = "well done", Name = "Inés", Restaurant = "The Copper Pan", LeftAt = "19:35", WaitMinutes = 23 };

        [Test]
        public void FactsAreFilledIn()
        {
            var voice = VoiceWith(ReviewTopic.WalkedOut, "Waited {wait} minutes for a {ordered} {main}. Left at {left}.");
            var line = ReviewVoices.Line(ReviewTopic.WalkedOut, Persona.Regular, Steak(), voice, null, new LineMemory(), new Random(1));
            Assert.That(line, Is.EqualTo("Waited 23 minutes for a medium steak. Left at 19:35."));
        }

        [Test]
        public void LinesNeedingUnknownFactsAreSkipped()
        {
            var voice = VoiceWith(ReviewTopic.NoSide, "Forgot my {side}.", "Something was missing.");
            for (var seed = 0; seed < 20; seed++)
            {
                var line = ReviewVoices.Line(ReviewTopic.NoSide, Persona.Regular, Steak(), voice, null, null, new Random(seed));
                Assert.That(line, Is.EqualTo("Something was missing."), "no side is known, so no line may name it");
            }
        }

        [Test]
        public void NoLineRepeatsUntilAllHaveBeenUsed()
        {
            var voice = VoiceWith(ReviewTopic.Decent, "A.", "B.", "C.", "D.");
            var memory = new LineMemory();
            var random = new Random(7);
            var first = Enumerable.Range(0, 4).Select(_ => ReviewVoices.Line(ReviewTopic.Decent, Persona.Regular, Steak(), voice, null, memory, random)).ToList();
            Assert.That(first.Distinct().Count(), Is.EqualTo(4), "every line once before any repeats");
            var fifth = ReviewVoices.Line(ReviewTopic.Decent, Persona.Regular, Steak(), voice, null, memory, random);
            Assert.That(fifth, Is.EqualTo(first[0]), "then the one used longest ago");
        }

        [Test]
        public void MemoryKeepsOnlyTheLatestLines()
        {
            var memory = new LineMemory(3);
            foreach (var line in new[] { "a", "b", "c", "d" })
                memory.Use(line);
            Assert.That(memory.WasUsed("a"), Is.False);
            Assert.That(memory.ToList(), Is.EqualTo(new[] { "b", "c", "d" }));
            var loaded = new LineMemory(3);
            loaded.Load(memory.ToList());
            Assert.That(loaded.WasUsed("d"), Is.True, "survives a save");
        }

        [Test]
        public void PersonaOnlyLinesStayWithTheirPersona()
        {
            var voice = new Voice
            {
                Lines = new Dictionary<ReviewTopic, List<VoiceLine>>
                {
                    [ReviewTopic.Praise] = new List<VoiceLine> { new VoiceLine("Critic only.", Persona.Critic) },
                },
            };
            Assert.That(ReviewVoices.Line(ReviewTopic.Praise, Persona.Regular, Steak(), voice, null, null, new Random(1)), Is.Empty);
            Assert.That(ReviewVoices.Line(ReviewTopic.Praise, Persona.Critic, Steak(), voice, null, null, new Random(1)), Is.EqualTo("Critic only."));
        }

        [Test]
        public void InfluencersAddAHashtag()
        {
            var voice = VoiceWith(ReviewTopic.Praise, "Great.");
            voice.Hashtags = new List<string> { "#steaknight" };
            var line = ReviewVoices.Line(ReviewTopic.Praise, Persona.Influencer, Steak(), voice, null, null, new Random(1));
            Assert.That(line, Is.EqualTo("Great. #steaknight"));
        }

        [Test]
        public void AReplyThatFitsGetsAWarmAnswerAndLikes()
        {
            var voice = new Voice
            {
                Replies = new Dictionary<(ReviewReply, TopicGroup), List<string>>
                {
                    [(ReviewReply.Apologise, TopicGroup.Wait)] = new List<string> { "Sorry, {name}." },
                },
                WarmAnswers = new List<VoiceLine> { new VoiceLine("Fair enough.") },
                DryAnswers = new List<VoiceLine> { new VoiceLine("Wow. Okay.") },
            };
            var warm = ReviewVoices.Reply(ReviewReply.Apologise, ReviewTopic.WalkedOut, true, Persona.Regular, Steak(), voice, null, null, new Random(3));
            Assert.That(warm.Owner, Is.EqualTo("Sorry, Inés."), "written for what the review was about");
            Assert.That(warm.Answer, Is.EqualTo("Fair enough."));
            Assert.That(warm.Likes, Is.GreaterThanOrEqualTo(3));

            var dry = ReviewVoices.Reply(ReviewReply.Apologise, ReviewTopic.WalkedOut, false, Persona.Regular, Steak(), voice, null, null, new Random(3));
            Assert.That(dry.Answer, Is.EqualTo("Wow. Okay."));
            Assert.That(dry.Likes, Is.LessThan(3));
        }

        [Test]
        public void TheSameReplyToTheSameReviewReadsTheSame()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<ServiceConfig>();
            ReviewRecord Review() => new ReviewRecord { Day = 1, Guest = "Inés R.", Order = "steak, medium", Topic = (int)ReviewTopic.WalkedOut, WalkedOut = true };
            var first = Review();
            var second = Review();
            ReviewReplies.WriteThread(first, ReviewReply.Apologise, config.StyleVoice(), config.GenericVoice(), null, "The Copper Pan");
            ReviewReplies.WriteThread(second, ReviewReply.Apologise, config.StyleVoice(), config.GenericVoice(), null, "The Copper Pan");
            Assert.That(first.ReplyText, Is.Not.Empty);
            Assert.That(first.Answer, Is.Not.Empty);
            Assert.That(second.ReplyText, Is.EqualTo(first.ReplyText));
            Assert.That(second.Answer, Is.EqualTo(first.Answer));
            Assert.That(first.ReplyText, Does.Not.Contain("{"), "every fact filled in");
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void TheGrillHasItsOwnVoiceAndTheGenericOneHasTheOldLines()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<ServiceConfig>();
            var settings = config.ToSettings();
            Assert.That(config.GenericVoice().Lines[ReviewTopic.Burnt].Select(l => l.Text), Does.Contain("Tasted of charcoal."));
            if (config.StyleName == "Grill" || config.StyleName == "Steakhouse")
                Assert.That(settings.Reviews.Style, Is.Not.Null);
            foreach (ReviewTopic topic in Enum.GetValues(typeof(ReviewTopic)))
                Assert.That(config.GenericVoice().Lines.ContainsKey(topic), Is.True, $"a generic line for {topic}");
            foreach (ReviewReply reply in Enum.GetValues(typeof(ReviewReply)))
            foreach (TopicGroup group in Enum.GetValues(typeof(TopicGroup)))
                Assert.That(config.GenericVoice().Replies.ContainsKey((reply, group)), Is.True, $"a generic reply for {reply} to {group}");
            UnityEngine.Object.DestroyImmediate(config);
        }
    }
}
