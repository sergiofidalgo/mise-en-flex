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
        public void AReplyIsWrittenForWhatTheReviewWasAbout()
        {
            var voice = new Voice
            {
                Replies = new Dictionary<(ReviewReply, TopicGroup), List<string>>
                {
                    [(ReviewReply.Apologise, TopicGroup.Wait)] = new List<string> { "Sorry about the wait, {name}." },
                    [(ReviewReply.Apologise, TopicGroup.Food)] = new List<string> { "Sorry about the {main}, {name}." },
                    [(ReviewReply.Apologise, TopicGroup.Doneness)] = new List<string> { "You asked for {ordered}, {name}." },
                },
            };
            Assert.That(ReviewVoices.Reply(ReviewReply.Apologise, ReviewTopic.WalkedOut, Steak(), voice, null, null, new Random(3)), Is.EqualTo("Sorry about the wait, Inés."));
            Assert.That(ReviewVoices.Reply(ReviewReply.Apologise, ReviewTopic.Burnt, Steak(), voice, null, null, new Random(3)), Is.EqualTo("Sorry about the steak, Inés."), "only doneness replies name the order");
            var doneness = Enumerable.Range(0, 10).Select(seed => ReviewVoices.Reply(ReviewReply.Apologise, ReviewTopic.Overcooked, Steak(), voice, null, null, new Random(seed))).Distinct().ToList();
            Assert.That(doneness, Is.EquivalentTo(new[] { "You asked for medium, Inés.", "Sorry about the steak, Inés." }), "a doneness complaint can also get a food reply");
        }

        [Test]
        public void TheSameReplyToTheSameReviewReadsTheSame()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<ServiceConfig>();
            ReviewRecord Review() => new ReviewRecord { Day = 1, Guest = "Inés R.", Order = "steak, medium", Topic = (int)ReviewTopic.WalkedOut, WalkedOut = true };
            var first = Review();
            var second = Review();
            ReviewReplies.WriteReply(first, ReviewReply.Apologise, config.StyleVoice(), config.GenericVoice(), null, "The Copper Pan");
            ReviewReplies.WriteReply(second, ReviewReply.Apologise, config.StyleVoice(), config.GenericVoice(), null, "The Copper Pan");
            Assert.That(first.ReplyText, Is.Not.Empty);
            Assert.That(second.ReplyText, Is.EqualTo(first.ReplyText));
            Assert.That(first.ReplyText, Does.Not.Contain("{"), "every fact filled in");
            UnityEngine.Object.DestroyImmediate(config);
        }

        // ---- Returning guests ----

        private static ReviewRecord Posted(string guest, int day, ReviewTopic topic, int reply = -1, Persona persona = Persona.Regular) =>
            new ReviewRecord { Guest = guest, Day = day, Topic = (int)topic, Reply = reply, Persona = (int)persona };

        [Test]
        public void GuestsRememberHowTheyWereTreated()
        {
            Assert.That(GuestBook.MemoryOf(Posted("A", 1, ReviewTopic.Burnt, (int)ReviewReply.Apologise)), Is.EqualTo(GuestMemory.Apologised));
            Assert.That(GuestBook.MemoryOf(Posted("A", 1, ReviewTopic.WalkedOut)), Is.EqualTo(GuestMemory.Ignored));
            Assert.That(GuestBook.MemoryOf(Posted("A", 1, ReviewTopic.Cold, (int)ReviewReply.StandYourGround)), Is.EqualTo(GuestMemory.Dismissed));
            Assert.That(GuestBook.MemoryOf(Posted("A", 1, ReviewTopic.Praise, (int)ReviewReply.Thank)), Is.EqualTo(GuestMemory.Thanked));
            Assert.That(GuestBook.MemoryOf(Posted("A", 1, ReviewTopic.Praise, (int)ReviewReply.Apologise)), Is.EqualTo(GuestMemory.Unthanked));
        }

        [Test]
        public void TheBookKnowsEarlierDaysOnlyAndEachGuestOnce()
        {
            var book = new GuestBook();
            book.Rebuild(new[]
            {
                Posted("Inés R.", 1, ReviewTopic.WalkedOut, persona: Persona.Critic),
                Posted("Inés R.", 2, ReviewTopic.Praise, (int)ReviewReply.Thank, Persona.Critic),
                Posted("Marco L.", 3, ReviewTopic.Burnt),
            }, today: 3);
            Assert.That(book.Guests.Count, Is.EqualTo(1), "today's guests aren't back yet");
            var ines = book.Guests[0];
            Assert.That(ines.Visits, Is.EqualTo(2));
            Assert.That(ines.Memory, Is.EqualTo(GuestMemory.Thanked), "as of their latest review");
            Assert.That(ines.Persona, Is.EqualTo(Persona.Critic));

            book.ReturnShare = 1f;
            Assert.That(book.Draw(new Random(1))?.Name, Is.EqualTo("Inés R."));
            Assert.That(book.Draw(new Random(1)), Is.Null, "once a day");
        }

        [Test]
        public void AnApologyBringsGuestsBackMoreThanDismissingThem()
        {
            var history = new List<ReviewRecord>();
            for (var i = 0; i < 50; i++)
            {
                history.Add(Posted($"Sorry {i}", 1, ReviewTopic.Burnt, (int)ReviewReply.Apologise));
                history.Add(Posted($"Smug {i}", 1, ReviewTopic.Burnt, (int)ReviewReply.StandYourGround));
            }
            int apologised = 0, dismissed = 0;
            var random = new Random(5);
            for (var day = 0; day < 200; day++)
            {
                var book = new GuestBook { ReturnShare = 1f };
                book.Rebuild(history, 2);
                var guest = book.Draw(random);
                if (guest.Memory == GuestMemory.Apologised)
                    apologised++;
                else
                    dismissed++;
            }
            Assert.That(apologised, Is.GreaterThan(dismissed * 4), "willingness 3 against 0.3");
        }

        [Test]
        public void AReturningGuestRemembersTheirLastVisit()
        {
            var voice = new Voice
            {
                ReturnLines = new Dictionary<(GuestMemory, bool), List<VoiceLine>>
                {
                    [(GuestMemory.Apologised, true)] = new List<VoiceLine> { new VoiceLine("Came back after the apology. Glad I did.") },
                    [(GuestMemory.Apologised, false)] = new List<VoiceLine> { new VoiceLine("Came back after the apology. Same again.") },
                },
            };
            Assert.That(ReviewVoices.ReturnLine(GuestMemory.Apologised, ReviewTopic.Praise, Persona.Regular, Steak(), voice, null, null, new Random(1)), Does.Contain("Glad"));
            Assert.That(ReviewVoices.ReturnLine(GuestMemory.Apologised, ReviewTopic.Burnt, Persona.Regular, Steak(), voice, null, null, new Random(1)), Does.Contain("Same again"));
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

        [Test]
        public void NotEveryGuestPosts()
        {
            var settings = new ReviewSettings { PraiseShare = 0.45f, DecentShare = 0.15f, ComplaintShare = 0.5f, WalkOutShare = 0.65f, PersonaShare = new[] { 0.7f, 1.3f, 2f, 3f, 1.1f, 0.8f } };
            Assert.That(settings.ShareChance(ReviewTopic.Decent, Persona.Regular), Is.LessThan(settings.ShareChance(ReviewTopic.Praise, Persona.Regular)), "a forgettable night is written about least");
            Assert.That(settings.ShareChance(ReviewTopic.WalkedOut, Persona.Regular), Is.GreaterThan(settings.ShareChance(ReviewTopic.Burnt, Persona.Regular)));
            Assert.That(settings.ShareChance(ReviewTopic.Praise, Persona.Critic), Is.EqualTo(1f), "critics nearly always post, capped at certain");
            Assert.That(new ReviewSettings().ShareChance(ReviewTopic.Decent, Persona.Local), Is.EqualTo(1f), "by default everyone posts");
        }

        [Test]
        public void TheConfigKeepsSomeGuestsQuiet()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<ServiceConfig>();
            var reviews = config.ToSettings().Reviews;
            foreach (ReviewTopic topic in Enum.GetValues(typeof(ReviewTopic)))
                Assert.That(reviews.ShareChance(topic, Persona.Regular), Is.LessThan(1f), $"a regular doesn't always post about {topic}");
            UnityEngine.Object.DestroyImmediate(config);
        }
    }
}
