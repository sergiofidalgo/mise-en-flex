using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Simulation.Saving;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Reviews are written when shown, in the player's language, old ones included; and reviews saved
    /// as English sentences are traced back to their templates (decision 2026-11-09).
    /// </summary>
    public class ReviewTextTests
    {
        [TearDown]
        public void BackToEnglish()
        {
            Locale.Language = "en";
            Translations.Clear("es");
        }

        private static Voice Grill() => ReviewVoiceDefaults.All().First(v => v.Speaks("Grill")).ToVoice();

        private static Voice Generic()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<ServiceConfig>();
            var voice = config.GenericVoice();
            UnityEngine.Object.DestroyImmediate(config);
            return voice;
        }

        private static ReviewFacts Facts() => new ReviewFacts { Main = "steak", Side = "onions", Ordered = "medium", Got = "well done", Restaurant = "The Copper Pan", LeftAt = "19:35", WaitMinutes = 23 };

        private static ReviewRecord Saved(Said said, ReviewFacts facts) => new ReviewRecord
        {
            Guest = "Mike S.", GuestSlot = 0, GuestInitial = "S", Order = "steak, medium",
            Line = said.Text, LineParts = said.Templates.ToList(), Facts = facts,
        };

        [Test]
        public void ANewReviewReadsAsItWasWritten()
        {
            var facts = Facts();
            var said = ReviewVoices.SayLine(ReviewTopic.WalkedOut, Persona.Influencer, facts, Grill(), Generic(), null, new System.Random(4));
            Assert.That(said.Templates, Is.Not.Empty);
            Assert.That(ReviewText.Line(Saved(said, facts)), Is.EqualTo(said.Text));
        }

        [Test]
        public void SwitchingLanguageRewritesOldReviews()
        {
            var template = "{wait} minutes, no {main}. Left at {left}.";
            var review = new ReviewRecord
            {
                Guest = "Mike S.", GuestSlot = 0, GuestInitial = "S", Order = "steak, medium",
                Line = "23 minutes, no steak. Left at 19:35.", LineParts = new List<string> { template }, Facts = Facts(),
                ReplyParts = new List<string> { "Sorry, {name}. The grill was full and we should've said so." },
            };
            Translations.Set("es", template, "{wait} minutos y ni rastro del {main}. Me fui a las {left}.");
            Translations.Set("es", "Sorry, {name}. The grill was full and we should've said so.", "Lo sentimos, {name}. La parrilla estaba llena.");
            Translations.Set("es", "steak", "filete");
            Translations.Set("es", "medium", "al punto");

            Assert.That(ReviewText.Line(review), Is.EqualTo("23 minutes, no steak. Left at 19:35."));
            Locale.Language = "es";
            Assert.That(ReviewText.Line(review), Is.EqualTo("23 minutos y ni rastro del filete. Me fui a las 19:35."));
            Assert.That(ReviewText.Reply(review), Is.EqualTo("Lo sentimos, Miguel. La parrilla estaba llena."), "the guest's name in Spanish too");
            Assert.That(ReviewText.Order(review.Order), Is.EqualTo("filete, al punto"));
        }

        [Test]
        public void AnUntranslatedLineStaysInEnglish()
        {
            var facts = Facts();
            var said = ReviewVoices.SayLine(ReviewTopic.Burnt, Persona.Regular, facts, Grill(), Generic(), null, new System.Random(2));
            Locale.Language = "es";
            Assert.That(ReviewText.Line(Saved(said, facts)), Is.EqualTo(said.Text));
        }

        [Test]
        public void AnOldEnglishReviewIsTracedWithItsFacts()
        {
            var review = new ReviewRecord { Guest = "Tony L.", Order = "steak, medium", Line = "31 minutes, no steak. Left at 20:05. #medrare" };
            Assert.That(ReviewTextRecovery.Recover(new[] { review }, Grill(), Generic()), Is.EqualTo(1));
            Assert.That(review.LineParts, Is.EqualTo(new[] { "{wait} minutes, no {main}. Left at {left}.", "#medrare" }));
            Assert.That(review.Facts.WaitMinutes, Is.EqualTo(31));
            Assert.That(review.Facts.LeftAt, Is.EqualTo("20:05"));
            Assert.That(review.Facts.Main, Is.EqualTo("steak"));

            Translations.Set("es", "{wait} minutes, no {main}. Left at {left}.", "{wait} minutos y ni rastro del {main}. Me fui a las {left}.");
            Translations.Set("es", "steak", "filete");
            Locale.Language = "es";
            Assert.That(ReviewText.Line(review), Is.EqualTo("31 minutos y ni rastro del filete. Me fui a las 20:05. #medrare"));
        }

        [Test]
        public void AnOldReplyBuiltFromPartsIsTraced()
        {
            var facts = Facts();
            facts.Name = "Tony";
            for (var seed = 0; seed < 40; seed++)
            {
                var said = ReviewVoices.SayReply(ReviewReply.Apologise, ReviewTopic.Overcooked, facts, OwnerMood.Tired, Grill(), Generic(), null, new System.Random(seed));
                var review = new ReviewRecord { Guest = "Tony L.", Order = "steak, medium", ReplyText = said.Text, Facts = new ReviewFacts { Main = "steak", Ordered = "medium" } };
                ReviewTextRecovery.Recover(new[] { review }, Grill(), Generic());
                Assert.That(review.ReplyParts, Is.Not.Empty, said.Text);
                var traced = ReviewText.Reply(review).Replace(GuestNames.FirstOf(review), "Tony");
                Assert.That(traced, Is.EqualTo(said.Text), "the traced parts write the same reply");
            }
        }

        [Test]
        public void ALineFromBeforeTheVoicesKeepsItsText()
        {
            var review = new ReviewRecord { Guest = "Inés R.", Line = "Something nobody wrote a template for.", ReplyText = "We're sorry. We'll do better next time." };
            Assert.That(ReviewTextRecovery.Recover(new[] { review }, Grill(), Generic()), Is.EqualTo(0));
            Assert.That(ReviewText.Line(review), Is.EqualTo("Something nobody wrote a template for."));
            Locale.Language = "es";
            Assert.That(ReviewText.Line(review), Is.EqualTo("Something nobody wrote a template for."));
        }

        [Test]
        public void EveryTemplateCanBeTracedBack()
        {
            var facts = Facts();
            var voices = new[] { Grill(), Generic() };
            var templates = voices.SelectMany(v => v.Lines.Values.SelectMany(l => l).Select(l => l.Text)
                .Concat(v.ReturnLines.Values.SelectMany(l => l).Select(l => l.Text))).Distinct().ToList();
            var lost = new List<string>();
            foreach (var template in templates)
            {
                var text = ReviewVoices.Fill(template, facts);
                var review = new ReviewRecord { Line = text };
                ReviewTextRecovery.Recover(new[] { review }, voices[0], voices[1]);
                if (review.LineParts.Count == 0 || ReviewVoices.Fill(review.LineParts[0], Merge(review.Facts, facts)) != text)
                    lost.Add(text);
            }
            Assert.That(lost, Is.Empty, string.Join("\n", lost));
        }

        // What was traced, with what the test knew for anything a template didn't mention.
        private static ReviewFacts Merge(ReviewFacts traced, ReviewFacts known) => new ReviewFacts
        {
            Main = string.IsNullOrEmpty(traced.Main) ? known.Main : traced.Main,
            Side = string.IsNullOrEmpty(traced.Side) ? known.Side : traced.Side,
            Ordered = string.IsNullOrEmpty(traced.Ordered) ? known.Ordered : traced.Ordered,
            Got = string.IsNullOrEmpty(traced.Got) ? known.Got : traced.Got,
            Restaurant = string.IsNullOrEmpty(traced.Restaurant) ? known.Restaurant : traced.Restaurant,
            LeftAt = string.IsNullOrEmpty(traced.LeftAt) ? known.LeftAt : traced.LeftAt,
            WaitMinutes = traced.WaitMinutes == 0 ? known.WaitMinutes : traced.WaitMinutes,
        };
    }
}
