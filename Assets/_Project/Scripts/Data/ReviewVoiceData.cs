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

    /// <summary>Which mood a piece of reply is for, in the inspector.</summary>
    public enum Mood
    {
        Any,
        Fresh,
        Tired,
        FedUp,
    }

    /// <summary>
    /// How a kind of restaurant is talked about (ideas.md, 2026-11-09): what its guests write, how its
    /// owner replies, who comes, what returning guests write, and its hashtags. Used together with the generic
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
        public sealed class Returns
        {
            [Tooltip("How they were treated last time.")]
            [SerializeField] private GuestMemory _remembers;
            [Tooltip("Tonight went well (praise or decent) rather than badly.")]
            [SerializeField] private bool _better;
            [SerializeField] private Who _who;
            [SerializeField, TextArea] private string[] _lines;

            public Returns() { }

            public Returns(GuestMemory remembers, bool better, Who who, params string[] lines)
            {
                _remembers = remembers;
                _better = better;
                _who = who;
                _lines = lines;
            }

            public GuestMemory Remembers => _remembers;
            public bool Better => _better;
            public Who Who => _who;
            public string[] Text => _lines ?? new string[0];
        }

        [Serializable]
        public sealed class Parts
        {
            [SerializeField] private ReviewReply _reply;
            [SerializeField] private ReplyPart _part;
            [Tooltip("Any mood, or only a fresh, tired or fed-up owner's.")]
            [SerializeField] private Mood _mood;
            [Tooltip("For every kind of review; otherwise only for the one below.")]
            [SerializeField] private bool _everyTopic;
            [SerializeField] private TopicGroup _about;
            [SerializeField, TextArea] private string[] _lines;

            public Parts() { }

            public Parts(ReviewReply reply, ReplyPart part, Mood mood, TopicGroup? about, params string[] lines)
            {
                _reply = reply;
                _part = part;
                _mood = mood;
                _everyTopic = about == null;
                _about = about ?? TopicGroup.Praise;
                _lines = lines;
            }

            public ReviewReply Reply => _reply;
            public ReplyPart Part => _part;
            public Mood Mood => _mood;
            public bool EveryTopic => _everyTopic;
            public TopicGroup About => _about;
            public string[] Text => _lines ?? new string[0];
        }

        [Tooltip("Restaurant styles that speak this way, e.g. Grill and Steakhouse. Empty: the generic voice.")]
        [SerializeField] private string[] _styles;
        [Tooltip("How often each kind of guest comes: Regular, Foodie, Influencer, Critic, Tourist, Local.")]
        [SerializeField] private float[] _guests;
        [SerializeField] private Lines[] _reviews;
        [Tooltip("The owner's replies, by reply and what the review is about. Each should read as what it is: an apology, thanks, or digging in.")]
        [SerializeField] private Replies[] _replies;
        [Tooltip("Influencers end their reviews with one.")]
        [SerializeField] private string[] _hashtags;
        [Tooltip("What a returning guest writes, remembering how they were treated last time.")]
        [SerializeField] private Returns[] _returns;
        [Tooltip("Pieces of longer replies: openers, bodies and closers, some only for a tired or fed-up owner.")]
        [SerializeField] private Parts[] _replyParts;

        public ReviewVoiceData() { }

        public ReviewVoiceData(string[] styles, float[] guests, Lines[] reviews, Replies[] replies, string[] hashtags)
        {
            _styles = styles;
            _guests = guests;
            _reviews = reviews;
            _replies = replies;
            _hashtags = hashtags;
        }

        /// <summary>Adds what returning guests write (kept out of the constructor, which lists first visits).</summary>
        public ReviewVoiceData WithReturns(params Returns[] returns)
        {
            _returns = returns;
            return this;
        }

        /// <summary>Adds pieces for longer replies.</summary>
        public ReviewVoiceData WithParts(params Parts[] parts)
        {
            _replyParts = parts;
            return this;
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
            foreach (var piece in _replyParts ?? new Parts[0])
            {
                var groups = piece.EveryTopic ? (TopicGroup[])Enum.GetValues(typeof(TopicGroup)) : new[] { piece.About };
                OwnerMood? mood = piece.Mood == Mood.Any ? (OwnerMood?)null : (OwnerMood)((int)piece.Mood - 1);
                foreach (var group in groups)
                {
                    var key = (piece.Reply, group, piece.Part);
                    if (!voice.ReplyParts.TryGetValue(key, out var list))
                        voice.ReplyParts[key] = list = new List<MoodLine>();
                    list.AddRange(piece.Text.Select(t => new MoodLine(t, mood)));
                }
            }
            foreach (var group in _returns ?? new Returns[0])
            {
                var key = (group.Remembers, group.Better);
                if (!voice.ReturnLines.TryGetValue(key, out var list))
                    voice.ReturnLines[key] = list = new List<VoiceLine>();
                list.AddRange(group.Text.Select(t => new VoiceLine(t, Persona(group.Who))));
            }
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
                new ReviewVoiceData.Lines(ReviewTopic.Dirty, Who.Anyone, "Crumbs from the last guy. Still there."),
                new ReviewVoiceData.Lines(ReviewTopic.Dirty, Who.Critic, "The food was fine. The table was not."),
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
            new[] { "#foodie", "#datenight", "#eatlocal" })
            .WithReturns(
                B(GuestMemory.Apologised, true, Who.Anyone, "Came back after the apology. Glad I did.", "Gave you a second chance. You took it."),
                B(GuestMemory.Apologised, false, Who.Anyone, "Came back after the apology. Same again.", "Second chance, same {main}. Hmm."),
                B(GuestMemory.Ignored, true, Who.Anyone, "Back again, quietly. Better this time.", "Nobody answered me. The {main} did."),
                B(GuestMemory.Ignored, false, Who.Anyone, "Still no reply. Still not right.", "Back for round two. Lost again."),
                B(GuestMemory.Dismissed, true, Who.Anyone, "Despite that reply, it was good.", "Ignored the attitude. Liked the {main}."),
                B(GuestMemory.Dismissed, false, Who.Anyone, "Came back to be sure. I was right.", "Still bad. Still smug, I bet."),
                B(GuestMemory.Thanked, true, Who.Anyone, "Back as promised. Still great.", "Back again. Still the best."),
                B(GuestMemory.Thanked, false, Who.Anyone, "Came back for the magic. Didn't find it.", "Not like last time. Off night?"),
                B(GuestMemory.Unthanked, true, Who.Anyone, "Loved it twice now.", "Back again. Still great. Say hi sometime."),
                B(GuestMemory.Unthanked, false, Who.Anyone, "Loved it last time. Not tonight."))
            .WithParts(
                P(ReviewReply.Apologise, ReplyPart.Opener, Mood.Any, null, "Hi {name}.", "{name}, thank you for the honest review."),
                P(ReviewReply.Apologise, ReplyPart.Closer, Mood.Any, null, "We hope to welcome you back.", "Thank you for the second chance, if you give us one."),
                P(ReviewReply.Apologise, ReplyPart.Closer, Mood.FedUp, null, "Long day. Sorry."),
                P(ReviewReply.Apologise, ReplyPart.Body, Mood.Any, TopicGroup.Wait, "You waited far too long, and we're sorry."),
                P(ReviewReply.Apologise, ReplyPart.Body, Mood.Any, TopicGroup.Food, "That's not the meal we want anyone to have."),
                P(ReviewReply.Apologise, ReplyPart.Body, Mood.Any, TopicGroup.Doneness, "That isn't how you ordered it, and we're sorry."),
                P(ReviewReply.Thank, ReplyPart.Opener, Mood.Any, null, "Hi {name}!"),
                P(ReviewReply.Thank, ReplyPart.Closer, Mood.Any, null, "Hope to see you soon."),
                P(ReviewReply.Thank, ReplyPart.Body, Mood.Any, TopicGroup.Praise, "Reviews like yours keep the kitchen going."),
                P(ReviewReply.StandYourGround, ReplyPart.Opener, Mood.Any, null, "Hi {name}."),
                P(ReviewReply.StandYourGround, ReplyPart.Closer, Mood.FedUp, null, "Have a good evening anyway."),
                P(ReviewReply.StandYourGround, ReplyPart.Body, Mood.Any, TopicGroup.Food, "We're proud of that dish as it is."),
                P(ReviewReply.StandYourGround, ReplyPart.Body, Mood.Any, TopicGroup.Wait, "Busy nights are busy."));

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

                L(ReviewTopic.Dirty, Who.Anyone, "Great {main}. Shame about the table.", "Grease on the table, ash on the floor."),
                L(ReviewTopic.Dirty, Who.Influencer, "Had to crop the sticky table out."),
                L(ReviewTopic.Dirty, Who.Local, "My shoes stuck to the floor. Again."),

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
                    "Too long a wait, {name}. We're fixing it.",
                    "{name}, your evening deserved better pacing. Sorry.",
                    "{name}, that wait was on us. Truly sorry.",
                    "We kept you waiting, {name}. Not okay. Sorry."),
                R(ReviewReply.Apologise, TopicGroup.Food,
                    "Sorry, {name}. That {main} deserved better from our grill.",
                    "Our grill let you down, {name}. It won't happen twice.",
                    "That's not the {main} we're proud of, {name}. Sorry.",
                    "{name}, you're right, and we're embarrassed.",
                    "Bad plate, bad night, our fault. Sorry, {name}."),
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
            new[] { "#steaknight", "#medrare", "#grillgoals", "#meatsweats", "#crustgoals", "#fridaygrill" })
            .WithReturns(
                B(GuestMemory.Apologised, true, Who.Anyone, "They said sorry. Then they grilled it right.", "Apology accepted. {ordered}, nailed."),
                B(GuestMemory.Apologised, true, Who.Influencer, "Redemption arc: complete."),
                B(GuestMemory.Apologised, true, Who.Anyone,
                    "Gave them a second shot. Bullseye.", "Whatever you changed, keep changing it.", "Redemption {main}. Worth coming back.",
                    "Told my wife it'd be better. It was.", "Comeback of the year, {restaurant}.", "The apology was real. So was the crust."),
                B(GuestMemory.Apologised, false, Who.Anyone,
                    "Two apologies, zero good steaks.", "Gave you another shot. You missed again.", "Sorry was nice. Steak was not.",
                    "Fool me twice, {restaurant}.", "The apology was better than the steak.", "Second visit, second disappointment.",
                    "I believed you. That's on me now."),
                B(GuestMemory.Ignored, true, Who.Anyone, "No reply, but the {main} said sorry.", "Silent treatment, great steak. I'll take it.",
                    "You never answered, but tonight did.", "Didn't reply, but they listened. Better."),
                B(GuestMemory.Ignored, false, Who.Anyone, "Back again. Grill still sulking.", "Still waiting on a reply. And a good steak.",
                    "No reply last time. No luck this time.", "Third strike pending, {restaurant}.", "Talking to myself again, apparently.",
                    "Back to check. Nothing has changed."),
                B(GuestMemory.Ignored, false, Who.Local, "The whole street knows. Still no reply."),
                B(GuestMemory.Dismissed, true, Who.Anyone, "Smug reply. Good {main}. Annoying.", "Hate to admit it: they were right.",
                    "Still annoyed. Steak was great, though."),
                B(GuestMemory.Dismissed, false, Who.Anyone, "Came back to prove a point. Proved it.", "Your grill, your rules, your empty tables.",
                    "They told me I was wrong. I wasn't.", "Back to see if the attitude improved. No.", "Same steak, same smug reply incoming."),
                B(GuestMemory.Dismissed, false, Who.Critic, "A second visit confirms the first."),
                B(GuestMemory.Thanked, true, Who.Anyone, "Back for the crust. Crust delivered.", "They thanked me. I thanked my stomach.",
                    "Promised I'd be back. Kept my promise.", "Same table, same joy, new favorite night.", "Third time I've said it: best steak around."),
                B(GuestMemory.Thanked, true, Who.Regular, "Usual table, usual {ordered}, usual joy."),
                B(GuestMemory.Thanked, false, Who.Anyone, "Last time: magic. Tonight: grill fail.", "Thanked me last time. Owe me one now.",
                    "Off night. Still rooting for you.", "Where did my perfect {main} go?"),
                B(GuestMemory.Unthanked, true, Who.Anyone, "Second great {main}. Third on Friday.", "Good again. Still waiting for my hello.",
                    "Twice great, never thanked. Rude, tasty."),
                B(GuestMemory.Unthanked, false, Who.Anyone, "Was my favorite. Was.", "Loved you once. Tonight, not so much."))
            .WithParts(
                // Apologising: warm at first, weary later, passive-aggressive at the end of a long night.
                P(ReviewReply.Apologise, ReplyPart.Opener, Mood.Any, null,
                    "{name}, thank you for telling us.", "Hey {name}. We read this twice.", "First things first, {name}: you're right.",
                    "Thanks for being straight with us, {name}.", "{name}, you deserve more than a one-liner."),
                P(ReviewReply.Apologise, ReplyPart.Opener, Mood.Tired, null, "{name}, it's late, but you deserve an answer.", "Long night here, {name}, but this one matters."),
                P(ReviewReply.Apologise, ReplyPart.Opener, Mood.FedUp, null, "Okay, {name}. Deep breath.", "{name}. Sigh. Fine."),
                P(ReviewReply.Apologise, ReplyPart.Closer, Mood.Any, null,
                    "Next time, ask for the chef at the pass.", "The next one will be right.", "We hope you give us another shot.",
                    "Come back on a Tuesday. It's calmer.", "Thanks for not giving up on us yet."),
                P(ReviewReply.Apologise, ReplyPart.Closer, Mood.Tired, null, "Now, if you'll excuse us, there's a grill to scrub.", "Off to soak our feet. Sorry again."),
                P(ReviewReply.Apologise, ReplyPart.Closer, Mood.FedUp, null, "That's all we've got tonight.", "We'll do better. Probably."),
                P(ReviewReply.Apologise, ReplyPart.Body, Mood.Any, TopicGroup.Wait,
                    "Nobody should wait that long for a steak, least of all on a Friday.", "We let the tickets pile up and you paid for it.",
                    "The grill was full and we should have told you at the door.", "That wait was on us, not on you.",
                    "Too many steaks, not enough grill, and you felt every minute of it."),
                P(ReviewReply.Apologise, ReplyPart.Body, Mood.Tired, TopicGroup.Wait, "We had one grill and twelve tables. Not your problem, but it's the truth.",
                    "Long queue, short staff, and you caught both.", "The tickets ran away from us tonight, and yours was one of them.",
                    "We were drowning back there. You noticed. We're sorry."),
                P(ReviewReply.Apologise, ReplyPart.Body, Mood.FedUp, TopicGroup.Wait, "Yes, it was slow. Everyone has mentioned it. Repeatedly.", "Sorry for the wait. Sorry for the next one too, while we're at it.",
                    "We are aware of the wait. We were there for all of it."),
                P(ReviewReply.Apologise, ReplyPart.Body, Mood.Any, TopicGroup.Food,
                    "That plate should never have left our pass.", "Our grill let you down, and we know it.",
                    "You came for a proper {main} and got our worst one.", "Whoever plated that has been spoken to. It was me. I've spoken to me."),
                P(ReviewReply.Apologise, ReplyPart.Body, Mood.Tired, TopicGroup.Food, "Not our finest plate. Not our finest night either.",
                    "It's been one of those nights, and your plate showed it.", "The grill had a bad night, and you got the worst of it."),
                P(ReviewReply.Apologise, ReplyPart.Body, Mood.Tired, TopicGroup.Doneness, "We rushed your {ordered}, and it showed."),
                P(ReviewReply.Apologise, ReplyPart.Body, Mood.FedUp, TopicGroup.Food, "Fine. It wasn't great. We noticed too.", "You're right. Happy now?"),
                P(ReviewReply.Apologise, ReplyPart.Body, Mood.Any, TopicGroup.Doneness,
                    "You asked for {ordered} and we served you something else.", "{ordered} means {ordered}, and we forgot that tonight.",
                    "We missed your {ordered} by a mile, and that's on the cook."),
                P(ReviewReply.Apologise, ReplyPart.Body, Mood.FedUp, TopicGroup.Doneness, "Doneness is hard at nine o'clock. Still, sorry."),
                P(ReviewReply.Apologise, ReplyPart.Body, Mood.Any, TopicGroup.Praise, "We're sorry if anything felt less than perfect?", "Apologies for the five stars, we guess. Habit."),

                // Thanking: glowing for praise, tone-deaf for a complaint.
                P(ReviewReply.Thank, ReplyPart.Opener, Mood.Any, null, "{name}! Thank you.", "Thanks for coming in, {name}.", "{name}, this made our night.", "Well, {name}, thank you."),
                P(ReviewReply.Thank, ReplyPart.Opener, Mood.Tired, null, "Thanks, {name}. Long night, but this helped."),
                P(ReviewReply.Thank, ReplyPart.Opener, Mood.FedUp, null, "Thanks, {name}. Noted."),
                P(ReviewReply.Thank, ReplyPart.Closer, Mood.Any, null, "See you soon!", "Save room for dessert next time.", "Same table next Friday?", "The grill says hi."),
                P(ReviewReply.Thank, ReplyPart.Closer, Mood.FedUp, null, "Have a night.", "Bye now."),
                P(ReviewReply.Thank, ReplyPart.Body, Mood.Any, TopicGroup.Praise,
                    "The whole line read this out loud.", "We're printing this and taping it above the grill.",
                    "That crust was no accident; we've been practicing.", "A {ordered} {main} done right is all we want to cook.",
                    "Reviews like this are why we light the grill at four."),
                P(ReviewReply.Thank, ReplyPart.Body, Mood.Tired, TopicGroup.Praise, "Some nights a review like this is all that keeps the grill lit.",
                    "Long night, but this one made it worth it."),
                P(ReviewReply.Thank, ReplyPart.Body, Mood.Tired, TopicGroup.Food, "Thanks for coming in on such a busy night!"),
                P(ReviewReply.Thank, ReplyPart.Body, Mood.FedUp, TopicGroup.Praise, "Finally, someone happy. Framing this one."),
                P(ReviewReply.Thank, ReplyPart.Body, Mood.Any, TopicGroup.Food, "We're so glad you chose us for your evening!", "We love hearing from our guests!", "Our {main} is a neighborhood favorite!"),
                P(ReviewReply.Thank, ReplyPart.Body, Mood.FedUp, TopicGroup.Food, "We got your feedback. It's in the pile with the others."),
                P(ReviewReply.Thank, ReplyPart.Body, Mood.Any, TopicGroup.Wait, "Thanks for your patience! Good things take time!", "We hope the {main} was worth the wait!"),
                P(ReviewReply.Thank, ReplyPart.Body, Mood.FedUp, TopicGroup.Wait, "Thanks for waiting. Everyone waited. It was that kind of night."),
                P(ReviewReply.Thank, ReplyPart.Body, Mood.Any, TopicGroup.Doneness, "We love that you tried our {ordered} {main}!"),

                // Standing your ground: proud at first, then openly rude when the night's been long.
                P(ReviewReply.StandYourGround, ReplyPart.Opener, Mood.Any, null, "With respect, {name}.", "{name}, let's be honest.", "Hi {name}.", "{name}, we hear you. We disagree."),
                P(ReviewReply.StandYourGround, ReplyPart.Opener, Mood.Tired, null, "{name}, it's late, so we'll be brief."),
                P(ReviewReply.StandYourGround, ReplyPart.Opener, Mood.FedUp, null, "Oh, {name}.", "Wow, {name}.", "Listen, {name}."),
                P(ReviewReply.StandYourGround, ReplyPart.Closer, Mood.Any, null, "The grill stands by its work.", "Hope to see you again. Or not.", "We'll keep cooking it our way."),
                P(ReviewReply.StandYourGround, ReplyPart.Closer, Mood.FedUp, null, "Good night, {name}.", "Enjoy the diner.", "Bon appétit somewhere else."),
                P(ReviewReply.StandYourGround, ReplyPart.Body, Mood.Any, TopicGroup.Food,
                    "A real {main} isn't for everyone.", "We cook it the way the grill wants it cooked.", "Our regulars order it exactly like that, every week."),
                P(ReviewReply.StandYourGround, ReplyPart.Body, Mood.Tired, TopicGroup.Food, "We served eighty plates tonight. Yours was one of them.",
                    "Every plate tonight came off the same grill as yours."),
                P(ReviewReply.StandYourGround, ReplyPart.Body, Mood.Tired, TopicGroup.Wait, "We were slammed. That's the whole story."),
                P(ReviewReply.StandYourGround, ReplyPart.Body, Mood.FedUp, TopicGroup.Food,
                    "If you wanted a boiled steak, there's a cafeteria down the road.", "Maybe try the chain place by the highway. They have a laminated menu."),
                P(ReviewReply.StandYourGround, ReplyPart.Body, Mood.Any, TopicGroup.Wait, "Fire takes time, and so does quality.", "We were full because people like us.", "We don't rush steak."),
                P(ReviewReply.StandYourGround, ReplyPart.Body, Mood.FedUp, TopicGroup.Wait, "Next time, come at six like a reasonable person.", "Patience is also on the menu. You skipped it."),
                P(ReviewReply.StandYourGround, ReplyPart.Body, Mood.Any, TopicGroup.Doneness, "That is what {ordered} looks like here.", "We've grilled for twenty years. We know {ordered}."),
                P(ReviewReply.StandYourGround, ReplyPart.Body, Mood.FedUp, TopicGroup.Doneness, "Your {ordered} was perfect. Your thermometer is lying."),
                P(ReviewReply.StandYourGround, ReplyPart.Body, Mood.Any, TopicGroup.Praise, "We know.", "Naturally.", "Tell your friends. Or don't; we're full anyway."));

        private static ReviewVoiceData.Lines L(ReviewTopic topic, Who who, params string[] lines) => new ReviewVoiceData.Lines(topic, who, lines);

        private static ReviewVoiceData.Replies R(ReviewReply reply, TopicGroup about, params string[] lines) => new ReviewVoiceData.Replies(reply, about, lines);

        private static ReviewVoiceData.Parts P(ReviewReply reply, ReplyPart part, Mood mood, TopicGroup? about, params string[] lines) =>
            new ReviewVoiceData.Parts(reply, part, mood, about, lines);

        private static ReviewVoiceData.Returns B(GuestMemory remembers, bool better, Who who, params string[] lines) => new ReviewVoiceData.Returns(remembers, better, who, lines);
    }
}
