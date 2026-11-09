using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Saving;
using Game.Simulation.Service;

namespace Game.Data
{
    /// <summary>
    /// Translations keyed by the English text they translate, the way gettext works: a review line's
    /// template ("{wait} minutes, no {main}. Left at {left}."), a dish word ("steak"), a doneness
    /// ("medium rare"). Keys are the English text itself, so rewording an English line never breaks
    /// an old save: the save keeps the old template, which still reads in English. Empty until the
    /// voices are translated (ideas.md, "Localising reviews and replies", step 3); anything without a
    /// translation shows in English.
    /// </summary>
    public static class Translations
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Tables = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>The text in the language, or the English text if there's no translation.</summary>
        public static string Get(string english, string language = null)
        {
            if (string.IsNullOrEmpty(english))
                return english ?? "";
            language ??= Locale.Language;
            return language != "en" && Tables.TryGetValue(language, out var table) && table.TryGetValue(english, out var text) && !string.IsNullOrEmpty(text)
                ? text
                : english;
        }

        /// <summary>Adds or replaces a translation (for the loaders to come, and tests).</summary>
        public static void Set(string language, string english, string translated)
        {
            if (!Tables.TryGetValue(language, out var table))
                Tables[language] = table = new Dictionary<string, string>();
            table[english] = translated;
        }

        public static void Clear(string language) => Tables.Remove(language);
    }

    /// <summary>
    /// Reviews and replies written when they're shown, in the player's language (decision
    /// 2026-11-09): from the templates and facts each review keeps, so switching language mid-campaign
    /// rewrites every review, old ones included. Reviews that couldn't be traced to their templates
    /// show the English they were saved with.
    /// </summary>
    public static class ReviewText
    {
        /// <summary>The review's line in the language (the current one by default).</summary>
        public static string Line(ReviewRecord review, string language = null) =>
            review.LineParts != null && review.LineParts.Count > 0
                ? Write(review.LineParts, review.Facts, GuestNames.FirstOf(review, language), language)
                : review.Line ?? "";

        /// <summary>The owner's reply in the language, or empty if it has none written from templates.</summary>
        public static string Reply(ReviewRecord review, string language = null) =>
            review.ReplyParts != null && review.ReplyParts.Count > 0
                ? Write(review.ReplyParts, review.Facts, GuestNames.FirstOf(review, language), language)
                : review.ReplyText ?? "";

        /// <summary>A review being written during service, before it has a name in the listing.</summary>
        public static string Line(Review review, string language = null) =>
            review.Templates != null && review.Templates.Count > 0
                ? Write(review.Templates, review.Facts, "", language)
                : review.Line ?? "";

        /// <summary>An order such as "steak, medium" in the language, word by word.</summary>
        public static string Order(string order, string language = null) =>
            string.IsNullOrEmpty(order) ? "" : string.Join(", ", order.Split(',').Select(part => Translations.Get(part.Trim(), language)));

        // Each template in the language, filled with the facts in the language, joined.
        private static string Write(IEnumerable<string> templates, ReviewFacts facts, string name, string language)
        {
            facts ??= new ReviewFacts();
            var local = new ReviewFacts
            {
                Main = Translations.Get(facts.Main, language),
                Side = Translations.Get(facts.Side, language),
                Ordered = Translations.Get(facts.Ordered, language),
                Got = Translations.Get(facts.Got, language),
                Name = name ?? "",
                Restaurant = facts.Restaurant,
                LeftAt = facts.LeftAt,
                WaitMinutes = facts.WaitMinutes,
            };
            return string.Join(" ", templates.Where(t => !string.IsNullOrEmpty(t)).Select(t => ReviewVoices.Fill(Translations.Get(t, language), local)));
        }
    }
}
