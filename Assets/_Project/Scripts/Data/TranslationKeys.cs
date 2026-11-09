using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Game.Data
{
    /// <summary>
    /// Finding what needs translating, and merging it into a language's table without losing work
    /// (decision 2026-11-09). Plain text in, plain text out, so it can be tested; the Editor tool
    /// (Tools/Game/Update Language Tables) feeds it the code and the data assets.
    /// </summary>
    public static class TranslationKeys
    {
        // Tr.Get("…") and Tr.Format("…", …): the first literal is the key.
        private static readonly Regex Single = new Regex(@"\bTr\.(?:Get|Format)\(\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

        // Tr.In("context", "…"): the key is "context|…".
        private static readonly Regex InContext = new Regex(@"\bTr\.In\(\s*""((?:[^""\\]|\\.)*)""\s*,\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

        // Tr.Plural(count, "…", "…"): both forms are keys.
        private static readonly Regex Plural = new Regex(@"\bTr\.Plural\([^,""]+,\s*""((?:[^""\\]|\\.)*)""\s*,\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

        // [SerializeField] private string _joinText = "…"; when the file shows it through Tr.Get(_joinText).
        private static readonly Regex Field = new Regex(@"private string (_\w+) = ""((?:[^""\\]|\\.)*)"";", RegexOptions.Compiled);

        /// <summary>Every key a C# file passes to Tr, in the order they appear.</summary>
        public static IEnumerable<string> InCode(string source)
        {
            // Comment lines show examples ("Tr.Format(\"Start day {day}\" …"), not text players read.
            source = string.Join("\n", (source ?? "").Split('\n').Select(line => line.TrimStart().StartsWith("//") ? "" : line));
            var found = new List<(int At, string Key)>();
            foreach (Match match in Single.Matches(source))
                found.Add((match.Index, Unescape(match.Groups[1].Value)));
            foreach (Match match in InContext.Matches(source))
                found.Add((match.Index, Unescape(match.Groups[1].Value) + "|" + Unescape(match.Groups[2].Value)));
            foreach (Match match in Plural.Matches(source))
            {
                found.Add((match.Index, Unescape(match.Groups[1].Value)));
                found.Add((match.Index + 1, Unescape(match.Groups[2].Value)));
            }
            foreach (Match match in Field.Matches(source))
            {
                if (source.Contains($"Tr.Get({match.Groups[1].Value})"))
                    found.Add((match.Index, Unescape(match.Groups[2].Value)));
            }
            return found.OrderBy(f => f.At).Select(f => f.Key).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct();
        }

        private static string Unescape(string literal) => literal.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\\\", "\\");

        /// <summary>
        /// The table with every key in <paramref name="keys"/> (key → where it's from), keeping each
        /// existing translation. Keys nobody uses any more stay, marked "unused", so a translation
        /// isn't thrown away by a rewording that might be undone; delete those rows by hand.
        /// </summary>
        public static string Merge(string existingCsv, IEnumerable<(string Key, string Source)> keys)
        {
            var old = new Dictionary<string, (string Translation, string Note)>();
            foreach (var row in Csv.Read(existingCsv ?? "").Skip(1))
            {
                if (row.Count > 0 && !string.IsNullOrEmpty(row[0]) && !old.ContainsKey(row[0]))
                    old[row[0]] = (row.Count > 1 ? row[1] : "", row.Count > 2 ? row[2] : "");
            }

            var rows = new List<string[]> { new[] { "english", "translation", "note" } };
            var seen = new HashSet<string>();
            foreach (var (key, source) in keys)
            {
                if (string.IsNullOrEmpty(key) || !seen.Add(key))
                    continue;
                var translation = old.TryGetValue(key, out var was) ? was.Translation : "";
                var note = was.Note;
                if (string.IsNullOrEmpty(note) || note == Unused)
                    note = source;
                rows.Add(new[] { key, translation, note });
            }
            foreach (var pair in old.Where(p => !seen.Contains(p.Key)))
                rows.Add(new[] { pair.Key, pair.Value.Translation, Unused });
            return Csv.Write(rows);
        }

        public const string Unused = "unused";

        /// <summary>The keys a table has no translation for.</summary>
        public static IEnumerable<string> Missing(string csv) =>
            Csv.Read(csv ?? "").Skip(1).Where(r => r.Count > 0 && !string.IsNullOrEmpty(r[0]) && (r.Count < 2 || string.IsNullOrEmpty(r[1])) && (r.Count < 3 || r[2] != Unused)).Select(r => r[0]);
    }
}
