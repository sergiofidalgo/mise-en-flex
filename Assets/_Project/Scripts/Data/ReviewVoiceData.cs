using System;
using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Service;
using UnityEngine;

namespace Game.Data
{
    /// <summary>Who a line is for, in the inspector: anyone, or one kind of guest.</summary>
    public enum Who
    {
        Anyone,
        Regular,
        Foodie,
        Influencer,
        Critic,
        Tourist,
        Local,
    }

    /// <summary>
    /// How a kind of restaurant is talked about (ideas.md, 2026-11-09): what its guests write, how its
    /// owner replies, how guests answer, who comes, and its hashtags. Used together with the generic
    /// voice, so a style only needs what's its own. {main}, {side}, {ordered}, {got}, {name},
    /// {restaurant}, {left} and {wait} are filled in; a line naming a fact that isn't known is skipped.
    /// </summary>
    [Serializable]
    public sealed class ReviewVoiceData
    {
        [Serializable]
        public sealed class Lines
        {
            [SerializeField] private ReviewTopic _topic;
            [SerializeField] private Who _who;
            [SerializeField, TextArea] private string[] _lines;

            public Lines() { }

            public Lines(ReviewTopic topic, Who who, params string[] lines)
            {
                _topic = topic;
                _who = who;
                _lines = lines;
            }

            public ReviewTopic Topic => _topic;
            public Who Who => _who;
            public string[] Text => _lines ?? new string[0];
        }

        [Serializable]
        public sealed class Replies
        {
            [SerializeField] private ReviewReply _reply;
            [Tooltip("What the review is broadly about: praise, the food, or the wait.")]
            [SerializeField] private TopicGroup _about;
            [SerializeField, TextArea] private string[] _lines;

            public Replies() { }

            public Replies(ReviewReply reply, TopicGroup about, params string[] lines)
            {
                _reply = reply;
                _about = about;
                _lines = lines;
            }

            public ReviewReply Reply => _reply;
            public TopicGroup About => _about;
            public string[] Text => _lines ?? new string[0];
        }

        [Serializable]
        public sealed class Answers
        {
            [SerializeField] private Who _who;
            [SerializeField, TextArea] private string[] _lines;

            public Answers() { }

            public Answers(Who who, params string[] lines)
            {
                _who = who;
                _lines = lines;
            }

            public Who Who => _who;
            public string[] Text => _lines ?? new string[0];
        }

        [Tooltip("Restaurant styles that speak this way, e.g. Grill and Steakhouse. Empty: the generic voice.")]
        [SerializeField] private string[] _styles;
        [Tooltip("How often each kind of guest comes: Regular, Foodie, Influencer, Critic, Tourist, Local.")]
        [SerializeField] private float[] _guests;
        [SerializeField] private Lines[] _reviews;
        [Tooltip("The owner's replies, by reply and what the review is about. Each should read as what it is: an apology, thanks, or digging in.")]
        [SerializeField] private Replies[] _replies;
        [Tooltip("A guest answering a reply that fits.")]
        [SerializeField] private Answers[] _warmAnswers;
        [Tooltip("A guest answering a reply that doesn't.")]
        [SerializeField] private Answers[] _dryAnswers;
        [Tooltip("Influencers end their reviews with one.")]
        [SerializeField] private string[] _hashtags;

        public ReviewVoiceData() { }

        public ReviewVoiceData(string[] styles, float[] guests, Lines[] reviews, Replies[] replies, Answers[] warm, Answers[] dry, string[] hashtags)
        {
            _styles = styles;
            _guests = guests;
            _reviews = reviews;
            _replies = replies;
            _warmAnswers = warm;
            _dryAnswers = dry;
            _hashtags = hashtags;
        }

        public bool IsGeneric => _styles == null || _styles.Length == 0;

        public bool Speaks(string style) =>
            !string.IsNullOrEmpty(style) && _styles != null && _styles.Any(s => string.Equals(s, style, StringComparison.OrdinalIgnoreCase));

        /// <summary>The voice as the simulation reads it.</summary>
        public Voice ToVoice()
        {
            var voice = new Voice
            {
                Style = IsGeneric ? "" : string.Join("/", _styles),
                PersonaWeights = _guests ?? new float[0],
                Hashtags = (_hashtags ?? new string[0]).ToList(),
            };
            foreach (var group in _reviews ?? new Lines[0])
            {
                if (!voice.Lines.TryGetValue(group.Topic, out var list))
                    voice.Lines[group.Topic] = list = new List<VoiceLine>();
                list.AddRange(group.Text.Select(t => new VoiceLine(t, Persona(group.Who))));
            }
            foreach (var group in _replies ?? new Replies[0])
            {
                var key = (group.Reply, group.About);
                if (!voice.Replies.TryGetValue(key, out var list))
                    voice.Replies[key] = list = new List<string>();
                list.AddRange(group.Text);
            }
            voice.WarmAnswers = (_warmAnswers ?? new Answers[0]).SelectMany(a => a.Text.Select(t => new VoiceLine(t, Persona(a.Who)))).ToList();
            voice.DryAnswers = (_dryAnswers ?? new Answers[0]).SelectMany(a => a.Text.Select(t => new VoiceLine(t, Persona(a.Who)))).ToList();
            return voice;
        }

        private static Persona? Persona(Who who) => who == Who.Anyone ? (Persona?)null : (Persona)((int)who - 1);
    }

    /// <summary>
    /// The starting voices: the generic one and the grill's. Starter writing to give the system
    /// something to deal from; more styles and many more lines come with each cuisine (Phase 7).
    /// </summary>
    public static class ReviewVoiceDefaults
    {
        public static ReviewVoiceData[] All() => new[] { Generic(), Grill() };

        private static ReviewVoiceData Generic() => new ReviewVoiceData(
            new string[0],
            // Regular, Foodie, Influencer, Critic, Tourist, Local
            new[] { 5f, 2f, 1f, 0.5f, 1.5f, 2f },
            new[]
            {
                new ReviewVoiceData.Lines(ReviewTopic.Praise, Who.Influencer, "Came for the {main}, stayed for the content.", "Five stars and a reshoot."),
                new ReviewVoiceData.Lines(ReviewTopic.Praise, Who.Critic, "A rare thing: a kitchen that listens."),
                new ReviewVoiceData.Lines(ReviewTopic.Decent, Who.Tourist, "Nice enough. The guidebook oversold it."),
                new ReviewVoiceData.Lines(ReviewTopic.SlowService, Who.Anyone, "Food: yes. Speed: no."),
                new ReviewVoiceData.Lines(ReviewTopic.WalkedOut, Who.Anyone, "Waited {wait} minutes. Left at {left}. Ate a sandwich at home."),
                new ReviewVoiceData.Lines(ReviewTopic.WalkedOut, Who.Critic, "I cannot review the food. I never met it."),
                new ReviewVoiceData.Lines(ReviewTopic.Cold, Who.Anyone, "Lukewarm {main}. Like a handshake from someone who doesn't mean it."),
            },
            new[]
            {
                new ReviewVoiceData.Replies(ReviewReply.Apologise, TopicGroup.Wait,
                    "Sorry, {name}. We got slammed and you paid for it.",
                    "That wait isn't who we are, {name}. Truly sorry."),
                new ReviewVoiceData.Replies(ReviewReply.Apologise, TopicGroup.Food,
                    "Sorry, {name}. That {main} should never have left the pass.",
                    "You're right, {name}. We'll get the next one right."),
                new ReviewVoiceData.Replies(ReviewReply.Apologise, TopicGroup.Praise,
                    "Sorry if anything was less than perfect, {name}!",
                    "We apologise for... everything? Thanks for coming!"),
                new ReviewVoiceData.Replies(ReviewReply.Thank, TopicGroup.Praise,
                    "Thank you, {name}! The whole kitchen is grinning.",
                    "Means a lot, {name}. See you soon."),
                new ReviewVoiceData.Replies(ReviewReply.Thank, TopicGroup.Food,
                    "Thanks for the feedback, {name}! Glad you stopped by!",
                    "Thank you for choosing {restaurant}!"),
                new ReviewVoiceData.Replies(ReviewReply.Thank, TopicGroup.Wait,
                    "Thanks for your patience, {name}!",
                    "Thank you for visiting {restaurant}!"),
                new ReviewVoiceData.Replies(ReviewReply.StandYourGround, TopicGroup.Food,
                    "We stand by our kitchen.",
                    "Our {main} is served exactly as the chef intends."),
                new ReviewVoiceData.Replies(ReviewReply.StandYourGround, TopicGroup.Wait,
                    "Good things take time.",
                    "We were busy. That's what good restaurants are."),
                new ReviewVoiceData.Replies(ReviewReply.StandYourGround, TopicGroup.Praise,
                    "We know.",
                    "As expected."),
            },
            new[]
            {
                new ReviewVoiceData.Answers(Who.Anyone, "Fair enough. I'll give you another go.", "Okay, that's actually nice of you.", "Respect for replying.", "Appreciated. See you soon."),
                new ReviewVoiceData.Answers(Who.Critic, "A gracious reply. Noted."),
                new ReviewVoiceData.Answers(Who.Influencer, "Love the energy. Reposting."),
                new ReviewVoiceData.Answers(Who.Local, "Alright. I'll be back Thursday, as usual."),
            },
            new[]
            {
                new ReviewVoiceData.Answers(Who.Anyone, "Thanks for the essay. Still hungry.", "Copy-paste reply, nice.", "Wow. Okay.", "That's not an answer."),
                new ReviewVoiceData.Answers(Who.Critic, "Defensiveness is not a seasoning."),
                new ReviewVoiceData.Answers(Who.Influencer, "Screenshotted. For the group chat."),
                new ReviewVoiceData.Answers(Who.Tourist, "Glad we're only here for the week."),
            },
            new[] { "#foodie", "#datenight", "#eatlocal" });

        private static ReviewVoiceData Grill() => new ReviewVoiceData(
            new[] { "Grill", "Steakhouse" },
            // Regular, Foodie, Influencer, Critic, Tourist, Local
            new[] { 4f, 2f, 1.2f, 0.6f, 1f, 3f },
            new[]
            {
                new ReviewVoiceData.Lines(ReviewTopic.Praise, Who.Anyone, "That crust. That pink. Marry me, {restaurant}.", "Ordered {ordered}, got exactly {ordered}. Rare skill."),
                new ReviewVoiceData.Lines(ReviewTopic.Praise, Who.Foodie, "Proper sear, proper rest. Someone back there cares."),
                new ReviewVoiceData.Lines(ReviewTopic.Praise, Who.Critic, "A {main} that knows what it is."),
                new ReviewVoiceData.Lines(ReviewTopic.Praise, Who.Influencer, "POV: the perfect {ordered} {main}."),
                new ReviewVoiceData.Lines(ReviewTopic.Decent, Who.Anyone, "Solid {main}. Not life-changing, not sad."),
                new ReviewVoiceData.Lines(ReviewTopic.Decent, Who.Local, "Does the job. Like the old place did."),
                new ReviewVoiceData.Lines(ReviewTopic.Undercooked, Who.Anyone, "Asked for {ordered}. It mooed.", "{got} is a bold reading of {ordered}."),
                new ReviewVoiceData.Lines(ReviewTopic.Overcooked, Who.Anyone, "Ordered {ordered}, received a {got} shoe."),
                new ReviewVoiceData.Lines(ReviewTopic.Overcooked, Who.Critic, "The {main} arrived {got}, which is to say, finished."),
                new ReviewVoiceData.Lines(ReviewTopic.Burnt, Who.Anyone, "I can still taste the smoke alarm.", "Charcoal is a fuel, not a doneness."),
                new ReviewVoiceData.Lines(ReviewTopic.PaleCrust, Who.Anyone, "Grey all over. The grill was clearly off duty."),
                new ReviewVoiceData.Lines(ReviewTopic.NotRested, Who.Anyone, "Cut it and the plate became a crime scene."),
                new ReviewVoiceData.Lines(ReviewTopic.Cold, Who.Anyone, "{main} at room temperature. Whose room?"),
                new ReviewVoiceData.Lines(ReviewTopic.SlowService, Who.Anyone, "Great {main}. Bring a book."),
                new ReviewVoiceData.Lines(ReviewTopic.SlowService, Who.Tourist, "Lovely place, but we nearly missed our train."),
                new ReviewVoiceData.Lines(ReviewTopic.WalkedOut, Who.Anyone, "Waited {wait} minutes for a {ordered} {main}. Left at {left}.", "Watched three tables eat. Mine never came."),
                new ReviewVoiceData.Lines(ReviewTopic.WalkedOut, Who.Influencer, "Came for content. Left with none."),
            },
            new[]
            {
                new ReviewVoiceData.Replies(ReviewReply.Apologise, TopicGroup.Food,
                    "Sorry, {name}. That {main} deserved better from our grill.",
                    "{name}, you asked for {ordered} and we didn't listen. Sorry."),
                new ReviewVoiceData.Replies(ReviewReply.Apologise, TopicGroup.Wait,
                    "Sorry, {name}. The grill was full and we should have said so."),
                new ReviewVoiceData.Replies(ReviewReply.StandYourGround, TopicGroup.Food,
                    "A real {main} isn't for everyone."),
                new ReviewVoiceData.Replies(ReviewReply.Thank, TopicGroup.Praise,
                    "Thank you, {name}! The grill says hi."),
            },
            new[] { new ReviewVoiceData.Answers(Who.Local, "Fine. Save me a seat by the grill.") },
            new[] { new ReviewVoiceData.Answers(Who.Foodie, "A {main} is not a matter of opinion. Doneness is.") },
            new[] { "#steaknight", "#medrare", "#grillgoals", "#meatsweats" });
    }
}
