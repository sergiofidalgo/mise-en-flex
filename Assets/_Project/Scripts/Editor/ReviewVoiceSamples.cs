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
    /// meet it: a week of reviews from its mix of guests, each with a reply that fits and one that
    /// doesn't, and the guest's answer. Menu: Tools/Game/Write Review Samples.
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

            var text = new StringBuilder();
            text.AppendLine($"# Review samples: {config.StyleName} ({config.RestaurantName})");
            text.AppendLine();
            text.AppendLine("A week of guests from this style's mix. Each review shows a reply that fits and one that doesn't, with the guest's answer. Not every guest posts: the chance is shown with each one.");
            for (var day = 1; day <= 7; day++)
            {
                text.AppendLine();
                text.AppendLine($"## Day {day}");
                for (var i = 0; i < 5; i++)
                {
                    var topic = topics[random.Next(topics.Length)];
                    var persona = ReviewVoices.PickPersona(style, generic, random);
                    var ordered = doneness[random.Next(doneness.Length)];
                    var got = topic == ReviewTopic.Undercooked ? "rare" : topic == ReviewTopic.Overcooked ? "well done" : ordered;
                    var facts = new ReviewFacts
                    {
                        Main = "steak", Side = "onions", Ordered = ordered, Got = got, Name = names[random.Next(names.Length)],
                        Restaurant = config.RestaurantName, LeftAt = $"19:{random.Next(10, 59)}", WaitMinutes = random.Next(18, 40),
                    };
                    var line = ReviewVoices.Line(topic, persona, facts, style, generic, memory, random);
                    var chance = settings.ShareChance(topic, persona);
                    text.AppendLine();
                    text.AppendLine($"**{facts.Name}** · {persona.ToString().ToLowerInvariant()} · {topic} · posts {chance:P0}");
                    text.AppendLine($"> “{line}”");
                    foreach (var reply in (ReviewReply[])Enum.GetValues(typeof(ReviewReply)))
                    {
                        var fits = ReviewReplies.Fits(reply, topic);
                        var thread = ReviewVoices.Reply(reply, topic, fits, persona, facts, style, generic, memory, random);
                        text.AppendLine($">  ↳ *{reply}* ({(fits ? "fits" : "doesn't fit")}): {thread.Owner}  ·  {facts.Name}: “{thread.Answer}” · {thread.Likes} like{(thread.Likes == 1 ? "" : "s")}");
                    }
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, text.ToString());
            Debug.Log($"[ReviewVoiceSamples] Wrote {file}");
        }
    }
}
