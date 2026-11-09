using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Simulation.Saving;

namespace Game.Simulation.Service
{
    /// <summary>
    /// Brings reviews saved as finished English sentences up to date (decision 2026-11-09): finds the
    /// template each sentence was written from, and the facts that filled it, so it can be shown in
    /// any language. Each template becomes a pattern ("{wait} minutes, no {main}." matches "23
    /// minutes, no steak."), and replies built from parts are matched part by part. The voices are a
    /// known, finite set, so nearly everything is found; what isn't (lines from before the voices)
    /// keeps its saved sentence. Safe to run on every load: reviews already traced are left alone.
    /// </summary>
    public static class ReviewTextRecovery
    {
        private static readonly Dictionary<(string, bool), Regex> Patterns = new Dictionary<(string, bool), Regex>();

        /// <summary>Traces every review it can. Returns how many lines and replies were traced.</summary>
        public static int Recover(IEnumerable<ReviewRecord> reviews, Voice style, Voice generic)
        {
            var lines = ReviewTemplates(style).Concat(ReviewTemplates(generic)).Distinct().OrderByDescending(t => t.Length).ToList();
            var tags = (style?.Hashtags ?? new List<string>()).Concat(generic?.Hashtags ?? new List<string>()).Distinct().ToList();
            var wholes = ReplyWholes(style).Concat(ReplyWholes(generic)).Distinct().OrderByDescending(t => t.Length).ToList();
            var openers = ReplyPieces(style, ReplyPart.Opener).Concat(ReplyPieces(generic, ReplyPart.Opener)).Distinct().OrderByDescending(t => t.Length).ToList();
            var bodies = ReplyPieces(style, ReplyPart.Body).Concat(ReplyPieces(generic, ReplyPart.Body)).Distinct().OrderByDescending(t => t.Length).ToList();
            var closers = ReplyPieces(style, ReplyPart.Closer).Concat(ReplyPieces(generic, ReplyPart.Closer)).Distinct().OrderByDescending(t => t.Length).ToList();

            var traced = 0;
            foreach (var review in reviews)
            {
                review.Facts ??= new ReviewFacts();
                review.LineParts ??= new List<string>();
                review.ReplyParts ??= new List<string>();
                if (review.LineParts.Count == 0 && !string.IsNullOrEmpty(review.Line) && TraceLine(review, lines, tags))
                    traced++;
                if (review.ReplyParts.Count == 0 && !string.IsNullOrEmpty(review.ReplyText) && TraceReply(review, wholes, openers, bodies, closers))
                    traced++;
            }
            return traced;
        }

        // A review line, maybe with an influencer's hashtag at the end.
        private static bool TraceLine(ReviewRecord review, List<string> templates, List<string> tags)
        {
            var text = review.Line.Trim();
            string tag = null;
            var space = text.LastIndexOf(' ');
            if (space > 0 && tags.Contains(text.Substring(space + 1)))
            {
                tag = text.Substring(space + 1);
                text = text.Substring(0, space);
            }
            foreach (var template in templates)
            {
                var match = Pattern(template, true).Match(text);
                if (!match.Success)
                    continue;
                Keep(match, review.Facts);
                review.LineParts = tag == null ? new List<string> { template } : new List<string> { template, tag };
                return true;
            }
            return false;
        }

        // A one-line reply, or a greeting, a heart and a sign-off one after the other.
        private static bool TraceReply(ReviewRecord review, List<string> wholes, List<string> openers, List<string> bodies, List<string> closers)
        {
            var text = review.ReplyText.Trim();
            foreach (var template in wholes.Concat(bodies))
            {
                var match = Pattern(template, true).Match(text);
                if (match.Success)
                {
                    Keep(match, review.Facts);
                    review.ReplyParts = new List<string> { template };
                    return true;
                }
            }
            foreach (var opener in new string[] { null }.Concat(openers))
            {
                var rest = text;
                Match first = null;
                if (opener != null)
                {
                    first = Pattern(opener).Match(rest);
                    if (!first.Success || first.Length >= rest.Length || rest[first.Length] != ' ')
                        continue;
                    rest = rest.Substring(first.Length + 1);
                }
                foreach (var body in bodies)
                {
                    var parts = new List<string>();
                    if (opener != null)
                        parts.Add(opener);
                    parts.Add(body);
                    var whole = Pattern(body, true).Match(rest);
                    if (whole.Success && opener != null)
                    {
                        Keep(first, review.Facts);
                        Keep(whole, review.Facts);
                        review.ReplyParts = parts;
                        return true;
                    }
                    var middle = Pattern(body).Match(rest);
                    if (!middle.Success)
                        continue;
                    if (middle.Length >= rest.Length || rest[middle.Length] != ' ')
                        continue;
                    var tail = rest.Substring(middle.Length + 1);
                    foreach (var closer in closers)
                    {
                        var last = Pattern(closer, true).Match(tail);
                        if (!last.Success)
                            continue;
                        if (first != null)
                            Keep(first, review.Facts);
                        Keep(middle, review.Facts);
                        Keep(last, review.Facts);
                        parts.Add(closer);
                        review.ReplyParts = parts;
                        return true;
                    }
                }
            }
            return false;
        }

        // The facts a match captured, kept where the review doesn't already know them.
        private static void Keep(Match match, ReviewFacts facts)
        {
            string Group(string name) => match.Groups[name].Success ? match.Groups[name].Value : null;
            facts.Main = string.IsNullOrEmpty(facts.Main) ? Lower(Group("main")) ?? "" : facts.Main;
            facts.Side = string.IsNullOrEmpty(facts.Side) ? Lower(Group("side")) ?? "" : facts.Side;
            facts.Ordered = string.IsNullOrEmpty(facts.Ordered) ? Lower(Group("ordered")) ?? "" : facts.Ordered;
            facts.Got = string.IsNullOrEmpty(facts.Got) ? Lower(Group("got")) ?? "" : facts.Got;
            facts.Restaurant = string.IsNullOrEmpty(facts.Restaurant) ? Group("restaurant") ?? "" : facts.Restaurant;
            facts.LeftAt = string.IsNullOrEmpty(facts.LeftAt) ? Group("left") ?? "" : facts.LeftAt;
            if (facts.WaitMinutes == 0 && int.TryParse(Group("wait"), out var wait))
                facts.WaitMinutes = wait;
        }

        // Dish and doneness words are lower case in the facts; a line that opens with one capitalised it.
        private static string Lower(string word) => string.IsNullOrEmpty(word) ? word : char.ToLowerInvariant(word[0]) + word.Substring(1);

        /// <summary>
        /// A template as a pattern: placeholders capture what filled them, a repeated one must match
        /// the same words, and the first letter may have been capitalised. Matches from the start, and
        /// to the end when <paramref name="whole"/>.
        /// </summary>
        private static Regex Pattern(string template, bool whole = false)
        {
            if (Patterns.TryGetValue((template, whole), out var cached))
                return cached;
            var pattern = new System.Text.StringBuilder("^");
            var seen = new HashSet<string>();
            var i = 0;
            var first = true;
            while (i < template.Length)
            {
                var open = template.IndexOf('{', i);
                var close = open >= 0 ? template.IndexOf('}', open) : -1;
                var literal = open < 0 || close < 0 ? template.Substring(i) : template.Substring(i, open - i);
                if (literal.Length > 0)
                {
                    pattern.Append(first ? FirstLetterEitherCase(literal) : Regex.Escape(literal));
                    first = false;
                }
                if (open < 0 || close < 0)
                    break;
                var name = template.Substring(open + 1, close - open - 1);
                if (seen.Contains(name))
                {
                    // The same words again, whatever their case: the first may have opened the sentence.
                    pattern.Append($@"(?i:\k<{name}>)");
                }
                else
                {
                    seen.Add(name);
                    pattern.Append(name switch
                    {
                        "wait" => @"(?<wait>\d+)",
                        "left" => @"(?<left>\d{1,2}:\d{2})",
                        _ => $"(?<{name}>.+?)",
                    });
                }
                first = false;
                i = close + 1;
            }
            if (whole)
                pattern.Append('$');
            var regex = new Regex(pattern.ToString(), RegexOptions.CultureInvariant);
            Patterns[(template, whole)] = regex;
            return regex;
        }

        private static string FirstLetterEitherCase(string literal)
        {
            var c = literal[0];
            if (!char.IsLetter(c))
                return Regex.Escape(literal);
            return $"[{char.ToUpperInvariant(c)}{char.ToLowerInvariant(c)}]" + Regex.Escape(literal.Substring(1));
        }

        private static IEnumerable<string> ReviewTemplates(Voice voice) =>
            voice == null ? Enumerable.Empty<string>()
                : voice.Lines.Values.SelectMany(l => l).Select(l => l.Text).Concat(voice.ReturnLines.Values.SelectMany(l => l).Select(l => l.Text));

        private static IEnumerable<string> ReplyWholes(Voice voice) =>
            voice == null ? Enumerable.Empty<string>() : voice.Replies.Values.SelectMany(l => l);

        private static IEnumerable<string> ReplyPieces(Voice voice, ReplyPart part) =>
            voice == null ? Enumerable.Empty<string>() : voice.ReplyParts.Where(p => p.Key.Item3 == part).SelectMany(p => p.Value).Select(l => l.Text);
    }
}
