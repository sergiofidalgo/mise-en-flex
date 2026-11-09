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
                new ReviewVoiceData.Lines(ReviewTopic.WalkedOut, Who.Anyone, "Waited {wait} minutes. Left at {left}."),
                new ReviewVoiceData.Lines(ReviewTopic.WalkedOut, Who.Critic, "I cannot review the food. I never met it."),
                new ReviewVoiceData.Lines(ReviewTopic.Cold, Who.Anyone, "Lukewarm {main}. Like a limp handshake."),
            },
            new[]
            {
                new ReviewVoiceData.Replies(ReviewReply.Apologise, TopicGroup.Wait,
                    "Sorry, {name}. We got slammed and you paid for it.",
                    "That wait isn't who we are, {name}. Truly sorry."),
                new ReviewVoiceData.Replies(ReviewReply.Apologise, TopicGroup.Food,
                    "Sorry, {name}. That {main} should never have left the pass.",
                    "You're right, {name}. We'll get the next one right."),
                new ReviewVoiceData.Replies(ReviewReply.Apologise, TopicGroup.Doneness,
                    "Sorry, {name}. You asked for {ordered} and deserved {ordered}."),
                new ReviewVoiceData.Replies(ReviewReply.Thank, TopicGroup.Doneness,
                    "Thanks for trying our {main}, {name}!"),
                new ReviewVoiceData.Replies(ReviewReply.StandYourGround, TopicGroup.Doneness,
                    "Our {main} is cooked exactly how the chef likes it."),
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

        // The grill: a steakhouse on a residential street. Its guests talk crust, pink and smoke; its
        // owner talks fire. Lines fit the dashboard: about 44 characters for a review (hashtag
        // included), 72 for a reply, 60 for an answer, once the facts are filled in (VoiceFitTests).
        private static ReviewVoiceData Grill() => new ReviewVoiceData(
            new[] { "Grill", "Steakhouse" },
            // Regular, Foodie, Influencer, Critic, Tourist, Local
            new[] { 4f, 2f, 1.2f, 0.6f, 1f, 3f },
            new[]
            {
                L(ReviewTopic.Praise, Who.Anyone, "{ordered}, exactly. Witchcraft.", "That crust. That pink. I'm in love.", "Best {main} on the block. Tell no one.", "Cut like butter. Ate like a raccoon."),
                L(ReviewTopic.Praise, Who.Regular, "Back again. Still perfect. Still happy."),
                L(ReviewTopic.Praise, Who.Foodie, "Proper sear, proper rest. Someone cares.", "Salted early, seared hard, rested right."),
                L(ReviewTopic.Praise, Who.Influencer, "POV: the perfect {ordered} {main}.", "So good I forgot to film it."),
                L(ReviewTopic.Praise, Who.Critic, "A {main} that knows exactly what it is.", "Salt, fire, patience. Nothing else."),
                L(ReviewTopic.Praise, Who.Tourist, "Best meal of the trip. Worth the detour."),
                L(ReviewTopic.Praise, Who.Local, "Finally, a grill this street deserves."),

                L(ReviewTopic.Decent, Who.Anyone, "Solid {main}. Not life-changing, not sad.", "Good, not great. Would grill again.", "A perfectly fine Tuesday {main}."),
                L(ReviewTopic.Decent, Who.Foodie, "Good meat, timid salt. Be braver."),
                L(ReviewTopic.Decent, Who.Influencer, "Mid, but the lighting slaps."),
                L(ReviewTopic.Decent, Who.Critic, "Competent. Occasionally more."),
                L(ReviewTopic.Decent, Who.Tourist, "Nice. Big portions. Very American."),
                L(ReviewTopic.Decent, Who.Local, "Does the job. Like the old place did.", "Fine. My dad's is better. Don't tell him."),

                L(ReviewTopic.Undercooked, Who.Anyone, "Asked for {ordered}. It mooed.", "{got}? Bold take on {ordered}.", "Cold in the middle. The {main}, not me."),
                L(ReviewTopic.Undercooked, Who.Foodie, "Thermometers cost ten dollars."),
                L(ReviewTopic.Undercooked, Who.Critic, "{got}, where {ordered} was promised."),
                L(ReviewTopic.Undercooked, Who.Influencer, "Plot twist: it's {got}."),
                L(ReviewTopic.Undercooked, Who.Tourist, "Is {got} the local {ordered}?"),
                L(ReviewTopic.Undercooked, Who.Local, "A good vet could've saved it."),

                L(ReviewTopic.Overcooked, Who.Anyone, "Ordered {ordered}. Received a shoe.", "My {main} was cooked into retirement.", "{ordered}? More like {got}."),
                L(ReviewTopic.Overcooked, Who.Foodie, "Grey to the middle. That's a crime."),
                L(ReviewTopic.Overcooked, Who.Critic, "The {main} arrived {got}. Finished."),
                L(ReviewTopic.Overcooked, Who.Influencer, "Ordered {ordered}. Got jerky."),
                L(ReviewTopic.Overcooked, Who.Tourist, "Great if you like {got}. We didn't."),
                L(ReviewTopic.Overcooked, Who.Local, "Grandma cooks steak like this. She's 94."),

                L(ReviewTopic.Burnt, Who.Anyone, "I can still taste the smoke alarm.", "Charcoal is a fuel, not a doneness.", "Blackened. Not the Cajun kind."),
                L(ReviewTopic.Burnt, Who.Foodie, "Carbon is not a crust."),
                L(ReviewTopic.Burnt, Who.Critic, "Burnt, and served anyway. That's it."),
                L(ReviewTopic.Burnt, Who.Influencer, "Steak, but make it goth."),
                L(ReviewTopic.Burnt, Who.Tourist, "Our {main} was very, very dark."),
                L(ReviewTopic.Burnt, Who.Local, "Smelled it from my porch."),

                L(ReviewTopic.NoMain, Who.Anyone, "Ordered a {main}. Got a plate of hope.", "Paid for a {main}. Met the garnish."),
                L(ReviewTopic.NoMain, Who.Critic, "The {main} did not attend."),
                L(ReviewTopic.NoMain, Who.Influencer, "Main character: missing."),
                L(ReviewTopic.NoMain, Who.Local, "Nice {side}. I came for {main}."),

                L(ReviewTopic.NoSide, Who.Anyone, "Lonely {main}. Where were the {side}?", "The {side} were promised. Never came."),
                L(ReviewTopic.NoSide, Who.Local, "No {side}. I noticed. I always notice."),
                L(ReviewTopic.NoSide, Who.Foodie, "A {main} needs its {side}. Basic."),

                L(ReviewTopic.Cold, Who.Anyone, "{main} at room temperature. Whose room?", "Hot grill, cold plate. Explain."),
                L(ReviewTopic.Cold, Who.Foodie, "Rested so long it went to sleep."),
                L(ReviewTopic.Cold, Who.Critic, "Served tepid. An insult of its own."),
                L(ReviewTopic.Cold, Who.Tourist, "Colder than our motel pool."),

                L(ReviewTopic.PaleCrust, Who.Anyone, "Grey all over. Grill was off duty.", "Steamed, not seared. This is a grill?", "No crust. Just a beige rectangle."),
                L(ReviewTopic.PaleCrust, Who.Foodie, "Wet pan, no sear, no flavor."),
                L(ReviewTopic.PaleCrust, Who.Critic, "A {main} without crust is a rumor."),
                L(ReviewTopic.PaleCrust, Who.Influencer, "Pale steak. Unfollow."),

                L(ReviewTopic.NotRested, Who.Anyone, "Cut it. Plate became a crime scene.", "Juice everywhere but in the {main}."),
                L(ReviewTopic.NotRested, Who.Foodie, "Five minutes of rest. That's all."),
                L(ReviewTopic.NotRested, Who.Local, "Needed a mop, not a napkin."),

                L(ReviewTopic.NotFresh, Who.Anyone, "That {main} had seen things.", "Tasted like yesterday's special."),
                L(ReviewTopic.NotFresh, Who.Critic, "Not fresh. You know it. I know it."),
                L(ReviewTopic.NotFresh, Who.Local, "Fridge smell. Clean your fridge."),

                L(ReviewTopic.BadCut, Who.Anyone, "{side} from tiny to boulder.", "Who cut the {side}? With what?"),
                L(ReviewTopic.BadCut, Who.Foodie, "Uneven cut, uneven cook. Physics."),

                L(ReviewTopic.SideOff, Who.Anyone, "Good {main}. The {side} need help.", "Great {main}, sad {side}."),
                L(ReviewTopic.SideOff, Who.Foodie, "Raw {side} on a cooked plate. Why."),
                L(ReviewTopic.SideOff, Who.Critic, "The {side} were an afterthought."),

                L(ReviewTopic.SlowService, Who.Anyone, "Great {main}. Bring a book.", "Worth it? Yes. Fast? Absolutely not.", "Ordered hungry, ate starving."),
                L(ReviewTopic.SlowService, Who.Regular, "Love you, but the wait's getting worse."),
                L(ReviewTopic.SlowService, Who.Critic, "Worth a wait. Not this wait."),
                L(ReviewTopic.SlowService, Who.Influencer, "Waited so long my phone died."),
                L(ReviewTopic.SlowService, Who.Tourist, "Nearly missed our train for it."),
                L(ReviewTopic.SlowService, Who.Local, "My kids aged a year. Steak was good."),

                L(ReviewTopic.WalkedOut, Who.Anyone, "{wait} minutes, no {main}. Left at {left}.", "Watched three tables eat. Not mine.", "Smelled great. Never found out why.", "Left hungry. Got pizza on the way."),
                L(ReviewTopic.WalkedOut, Who.Regular, "First time I've walked out. Last, I hope."),
                L(ReviewTopic.WalkedOut, Who.Critic, "I can't review a {main} I never met."),
                L(ReviewTopic.WalkedOut, Who.Influencer, "Came for content. Left with none."),
                L(ReviewTopic.WalkedOut, Who.Tourist, "Waited {wait} minutes, then the bus came."),
                L(ReviewTopic.WalkedOut, Who.Local, "Gave up at {left}. Diner it is."),
            },
            new[]
            {
                // Apologies read as apologies, thanks as thanks, digging in as digging in: the guest's
                // answer then tells players whether it was the right call (decision 2026-11-09).
                R(ReviewReply.Apologise, TopicGroup.Wait,
                    "Sorry, {name}. The grill was full and we should've said so.",
                    "{name}, that wait was on us. Truly sorry.",
                    "We kept you waiting, {name}. Not okay. Sorry."),
                R(ReviewReply.Apologise, TopicGroup.Food,
                    "Sorry, {name}. That {main} deserved better from our grill.",
                    "Our grill let you down, {name}. It won't happen twice."),
                R(ReviewReply.Apologise, TopicGroup.Doneness,
                    "{name}, you asked for {ordered} and we didn't listen. Sorry.",
                    "Sorry, {name}. {ordered} means {ordered}. Our grill forgot."),
                R(ReviewReply.Apologise, TopicGroup.Praise,
                    "So sorry, {name}! For... the stars?",
                    "Apologies, {name}! We'll try to be less good?"),
                R(ReviewReply.Thank, TopicGroup.Praise,
                    "Thank you, {name}! The grill says hi.",
                    "{name}, you made the whole line grin tonight.",
                    "Thanks, {name}. Same table next Friday?"),
                R(ReviewReply.Thank, TopicGroup.Food,
                    "Thanks for dining with us, {name}! Enjoy your evening!",
                    "Thank you for the feedback! We love our {main} too!"),
                R(ReviewReply.Thank, TopicGroup.Wait,
                    "Thanks for your patience, {name}! Good things take time!",
                    "Thanks for waiting, {name}! Hope the {main} was worth it!"),
                R(ReviewReply.StandYourGround, TopicGroup.Food,
                    "A real {main} isn't for everyone.",
                    "That's how fire works, {name}."),
                R(ReviewReply.StandYourGround, TopicGroup.Doneness,
                    "Our grill, our rules. That's {ordered} here.",
                    "We've grilled for years, {name}. We know {ordered}."),
                R(ReviewReply.Thank, TopicGroup.Doneness,
                    "Thanks for trying our {ordered}, {name}! Come again!"),
                R(ReviewReply.StandYourGround, TopicGroup.Wait,
                    "Fire takes time. So does quality.",
                    "We don't rush steak. Or apologies.",
                    "Busy grill, happy grill."),
                R(ReviewReply.StandYourGround, TopicGroup.Praise,
                    "We know.",
                    "Naturally.",
                    "Tell your friends. Or don't. We're full anyway."),
            },
            new[]
            {
                A(Who.Anyone, "Fair. Save me a seat by the grill.", "Okay. I'll give the {main} another shot.", "Classy reply. See you Friday."),
                A(Who.Regular, "That's why I keep coming back."),
                A(Who.Foodie, "Respect. Back soon for the {ordered}."),
                A(Who.Influencer, "Okay, this reply is a vibe. Reposting."),
                A(Who.Critic, "Gracious. I'll revisit."),
                A(Who.Tourist, "Sweet! Next time we're passing through."),
                A(Who.Local, "Fine. Usual table, usual order."),
            },
            new[]
            {
                A(Who.Anyone, "Cool reply. Still hungry.", "Did a bot write this?", "This is why it's not three stars."),
                A(Who.Regular, "Wow. After all these Fridays?"),
                A(Who.Foodie, "Doneness is not a matter of opinion."),
                A(Who.Influencer, "Screenshotted. For my followers."),
                A(Who.Critic, "Defensiveness: the house special."),
                A(Who.Tourist, "Next time we'll try the diner."),
                A(Who.Local, "Telling the whole street."),
            },
            new[] { "#steaknight", "#medrare", "#grillgoals", "#meatsweats", "#crustgoals", "#fridaygrill" });

        private static ReviewVoiceData.Lines L(ReviewTopic topic, Who who, params string[] lines) => new ReviewVoiceData.Lines(topic, who, lines);

        private static ReviewVoiceData.Replies R(ReviewReply reply, TopicGroup about, params string[] lines) => new ReviewVoiceData.Replies(reply, about, lines);

        private static ReviewVoiceData.Answers A(Who who, params string[] lines) => new ReviewVoiceData.Answers(who, lines);
    }
}
