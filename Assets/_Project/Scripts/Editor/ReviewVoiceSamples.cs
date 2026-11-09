using System;
using System.IO;
using System.Linq;
using System.Text;
using Game.Data;
using Game.Simulation.Saving;
using Game.Simulation.Service;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Writes a sample of what a restaurant's voice produces, to read the writing as players will
    /// meet it: a week of reviews from its mix of guests, each with a reply (or none), and guests
    /// coming back remembering how they were treated. Menu: Tools/Game/Write Review Samples.
    /// Batch: -executeMethod Game.Editor.ReviewVoiceSamples.WriteFromCommandLine [-samplesOut file]
    /// </summary>
    public static class ReviewVoiceSamples
    {
        private const string ConfigPath = "Assets/_Project/Data/Settings/ServiceConfig.asset";

        [MenuItem("Tools/Game/Write Review Samples")]
        public static void WriteFromMenu()
        {
            var file = Path.GetFullPath("Screenshots/ReviewSamples.md");
            Write(file);
            EditorUtility.RevealInFinder(file);
        }

        public static void WriteFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-samplesOut");
            Write(index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.GetFullPath("Screenshots/ReviewSamples.md"));
            EditorApplication.Exit(0);
        }

        private static void Write(string file)
        {
            var config = AssetDatabase.LoadAssetAtPath<ServiceConfig>(ConfigPath);
            var style = config.StyleVoice();
            var generic = config.GenericVoice();
            var settings = config.ToSettings().Reviews;
            var memory = new LineMemory();
            var random = new System.Random(2611);
            var topics = (ReviewTopic[])Enum.GetValues(typeof(ReviewTopic));
            string[] names = { "Inés", "Marco", "Pavla", "Aiko", "Tomáš", "Dolores", "Kwame", "Bea" };
            string[] doneness = { "rare", "medium rare", "medium", "well done" };

            var book = new GuestBook { ReturnShare = 0.5f };
            var posted = new System.Collections.Generic.List<ReviewRecord>();
            var replies = (ReviewReply[])Enum.GetValues(typeof(ReviewReply));

            var text = new StringBuilder();
            text.AppendLine($"# Review samples: {config.StyleName} ({config.RestaurantName})");
            text.AppendLine();
            text.AppendLine("A week of reviews from this style's guests, each with the reply the owner posted (picked at random here, or none). Guests who were treated well come back more often, and remember it. Not every guest posts: the chance is shown with each one.");
            for (var day = 1; day <= 7; day++)
            {
                book.Rebuild(posted, day);
                text.AppendLine();
                text.AppendLine($"## Day {day}");
                for (var i = 0; i < 5; i++)
                {
                    var topic = topics[random.Next(topics.Length)];
                    var returning = book.Draw(random);
                    var persona = returning?.Persona ?? ReviewVoices.PickPersona(style, generic, random);
                    var ordered = doneness[random.Next(doneness.Length)];
                    var got = topic == ReviewTopic.Undercooked ? "rare" : topic == ReviewTopic.Overcooked ? "well done" : ordered;
                    var name = returning?.Name ?? $"{names[random.Next(names.Length)]} {(char)('A' + random.Next(26))}.";
                    var facts = new ReviewFacts
                    {
                        Main = "steak", Side = "onions", Ordered = ordered, Got = got, Name = name.Split(' ')[0],
                        Restaurant = config.RestaurantName, LeftAt = $"19:{random.Next(10, 59)}", WaitMinutes = random.Next(18, 40),
                    };
                    var line = returning != null
                        ? ReviewVoices.ReturnLine(returning.Memory, topic, persona, facts, style, generic, memory, random)
                        : ReviewVoices.Line(topic, persona, facts, style, generic, memory, random);
                    var chance = settings.ShareChance(topic, persona);
                    var back = returning != null ? $" · **back** (visit {returning.Visits + 1}, remembers: {returning.Memory})" : "";
                    text.AppendLine();
                    text.AppendLine($"**{name}** · {persona.ToString().ToLowerInvariant()} · {topic} · posts {chance:P0}{back}");
                    text.AppendLine($"> “{line}”");

                    var record = new ReviewRecord { Day = day, Guest = name, Topic = (int)topic, Persona = (int)persona, Order = $"steak, {ordered}" };
                    var pick = random.Next(replies.Length + 1);
                    if (pick < replies.Length)
                    {
                        record.Reply = (int)replies[pick];
                        ReviewReplies.WriteReply(record, replies[pick], style, generic, memory, config.RestaurantName);
                        text.AppendLine($">  ↳ *{replies[pick]}* ({(ReviewReplies.Fits(replies[pick], topic) ? "fits" : "doesn't fit")}): {record.ReplyText}");
                    }
                    else
                    {
                        text.AppendLine(">  ↳ *no reply*");
                    }
                    posted.Add(record);
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, text.ToString());
            Debug.Log($"[ReviewVoiceSamples] Wrote {file}");
        }
    }
}
