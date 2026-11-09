using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Translations keyed by the English text they translate, the way gettext works (decision
    /// 2026-11-09, user's choice of our own tables over Unity's Localization package): UI text
    /// ("Start day {day}"), review templates ("{wait} minutes, no {main}."), words from data
    /// ("steak", "medium rare"). Keys are the English itself, so rewording English never breaks an
    /// old save or a table: the old key just reads in English until it's translated again. Each
    /// language is a CSV file (english, translation, note), loaded from <see cref="LanguageTables"/>.
    /// </summary>
    public static class Translations
    {
        /// <summary>
        /// A pretend language for testing: every translated text shows accented, bracketed and a third
        /// longer, so text that skips translation stands out, and so does layout that can't stretch.
        /// </summary>
        public const string Pseudo = "xx";

        private static readonly Dictionary<string, Dictionary<string, string>> Tables = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>The text in the language, or the English text if there's no translation.</summary>
        public static string Get(string english, string language = null)
        {
            if (string.IsNullOrEmpty(english))
                return english ?? "";
            language ??= Locale.Language;
            if (language == "en")
                return english;
            if (language == Pseudo)
                return PseudoOf(english);
            return Tables.TryGetValue(language, out var table) && table.TryGetValue(english, out var text) && !string.IsNullOrEmpty(text)
                ? text
                : english;
        }

        /// <summary>Whether the language has its own translation of this text.</summary>
        public static bool Has(string language, string english) =>
            Tables.TryGetValue(language, out var table) && table.TryGetValue(english, out var text) && !string.IsNullOrEmpty(text);

        /// <summary>Adds or replaces a translation (loaders and tests).</summary>
        public static void Set(string language, string english, string translated)
        {
            if (!Tables.TryGetValue(language, out var table))
                Tables[language] = table = new Dictionary<string, string>();
            table[english] = translated;
        }

        public static void Clear(string language) => Tables.Remove(language);

        /// <summary>Reads a language's CSV (english, translation, note), adding its translations.</summary>
        public static int LoadCsv(string language, string csv)
        {
            var count = 0;
            foreach (var row in Csv.Read(csv).Skip(1))
            {
                if (row.Count < 2 || string.IsNullOrEmpty(row[0]) || string.IsNullOrEmpty(row[1]))
                    continue;
                Set(language, row[0], row[1]);
                count++;
            }
            return count;
        }

        // Vowels accented, the text bracketed and lengthened by a third, placeholders and markup untouched.
        private static string PseudoOf(string english)
        {
            const string plain = "aeiouAEIOUcnyCNY";
            const string accented = "àéìòùÀÉÌÒÙçñýÇÑÝ";
            var text = new StringBuilder("[");
            var inside = 0;
            foreach (var c in english)
            {
                if (c == '{' || c == '<')
                    inside++;
                var index = inside == 0 ? plain.IndexOf(c) : -1;
                text.Append(index >= 0 ? accented[index] : c);
                if ((c == '}' || c == '>') && inside > 0)
                    inside--;
            }
            text.Append(' ').Append(new string('~', Math.Max(1, english.Length / 3))).Append(']');
            return text.ToString();
        }
    }

    /// <summary>
    /// Text as the player reads it: translated, with named slots filled ("Start day {day}") and the
    /// right plural form. Call these for every string players see; the key collector
    /// (Tools/Game/Update Language Tables) finds them by name in the code.
    /// </summary>
    public static class Tr
    {
        /// <summary>The text in the player's language.</summary>
        public static string Get(string english) => Translations.Get(english);

        /// <summary>
        /// For an English word that means two things ("medium" heat, "medium" steak): the key is
        /// "context|english", so each meaning gets its own translation; English, and any language
        /// without one, reads the plain word.
        /// </summary>
        public static string In(string context, string english)
        {
            var language = Locale.Language;
            var key = context + "|" + english;
            return language != "en" && Translations.Has(language, key) ? Translations.Get(key, language) : Translations.Get(english, language);
        }

        /// <summary>Translated, then each {name} filled: Tr.Format("Start day {day}", ("day", 2)).</summary>
        public static string Format(string english, params (string Name, object Value)[] slots)
        {
            var text = Translations.Get(english);
            foreach (var (name, value) in slots)
                text = text.Replace("{" + name + "}", Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
            return text;
        }

        /// <summary>
        /// The singular or plural form, translated, with {n} filled: Tr.Plural(count, "1 new review",
        /// "{n} new reviews"). English and Spanish both use the singular for exactly one; languages
        /// with other rules get a rule here when they arrive.
        /// </summary>
        public static string Plural(int count, string one, string other, params (string Name, object Value)[] slots)
        {
            var form = count == 1 ? one : other;
            var all = new (string, object)[slots.Length + 1];
            all[0] = ("n", count);
            Array.Copy(slots, 0, all, 1, slots.Length);
            return Format(form, all);
        }
    }

    /// <summary>Comma-separated values with quotes, as spreadsheets save them.</summary>
    public static class Csv
    {
        public static IEnumerable<List<string>> Read(string text)
        {
            var row = new List<string>();
            var field = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < (text ?? "").Length; i++)
            {
                var c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else if (c == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        field.Append(c);
                    }
                    continue;
                }
                switch (c)
                {
                    case '"':
                        quoted = true;
                        break;
                    case ',':
                        row.Add(field.ToString());
                        field.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(field.ToString());
                        field.Clear();
                        yield return row;
                        row = new List<string>();
                        break;
                    default:
                        field.Append(c);
                        break;
                }
            }
            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                yield return row;
            }
        }

        public static string Write(IEnumerable<IEnumerable<string>> rows) =>
            string.Join("\n", rows.Select(r => string.Join(",", r.Select(Quote)))) + "\n";

        private static string Quote(string field)
        {
            field ??= "";
            return field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 || field.StartsWith(" ") || field.EndsWith(" ")
                ? "\"" + field.Replace("\"", "\"\"") + "\""
                : field;
        }
    }
}
