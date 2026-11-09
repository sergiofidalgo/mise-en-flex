using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The translation tables, the Tr calls and the key collector (decision 2026-11-09), plus checks
    /// on the shipped Spanish table: every text players read has a translation, with the same slots.
    /// </summary>
    public class LocalizationTests
    {
        private const string SpanishTable = "Assets/_Project/Data/Localization/es.csv";

        [TearDown]
        public void BackToEnglish()
        {
            Locale.Language = "en";
            Translations.Clear("es");
        }

        [Test]
        public void CsvKeepsCommasQuotesNewlinesAndSpaces()
        {
            var rows = new[] { new[] { "english", "translation", "note" }, new[] { " · day {day}", "say \"hi\", then go", "two\nlines" } };
            var parsed = Csv.Read(Csv.Write(rows)).ToList();
            Assert.That(parsed[1], Is.EqualTo(rows[1]));
        }

        [Test]
        public void TextReadsInTheLanguageWithSlotsFilled()
        {
            Translations.LoadCsv("es", "english,translation,note\nStart day {day},Empezar el día {day},\n1 more review,1 reseña más,\n{n} more reviews,{n} reseñas más,\n");
            Assert.That(Tr.Format("Start day {day}", ("day", 4)), Is.EqualTo("Start day 4"));
            Locale.Language = "es";
            Assert.That(Tr.Format("Start day {day}", ("day", 4)), Is.EqualTo("Empezar el día 4"));
            Assert.That(Tr.Plural(1, "1 more review", "{n} more reviews"), Is.EqualTo("1 reseña más"));
            Assert.That(Tr.Plural(3, "1 more review", "{n} more reviews"), Is.EqualTo("3 reseñas más"));
            Assert.That(Tr.Get("Not in the table"), Is.EqualTo("Not in the table"), "untranslated text reads in English");
        }

        [Test]
        public void AWordWithTwoMeaningsTranslatesByContext()
        {
            Translations.Set("es", "medium", "al punto");
            Translations.Set("es", "heat|medium", "medio");
            Assert.That(Tr.In("heat", "medium"), Is.EqualTo("medium"));
            Locale.Language = "es";
            Assert.That(Tr.In("heat", "medium"), Is.EqualTo("medio"));
            Assert.That(Tr.Get("medium"), Is.EqualTo("al punto"));
            Translations.Clear("es");
            Translations.Set("es", "medium", "al punto");
            Assert.That(Tr.In("heat", "low"), Is.EqualTo("low"), "no context translation: the plain word");
        }

        [Test]
        public void ThePseudoLanguageMarksTextButKeepsSlotsAndMarkup()
        {
            Locale.Language = Translations.Pseudo;
            var text = Tr.Format("Start day {day} <b>now</b>", ("day", 2));
            Assert.That(text, Does.StartWith("[").And.EndWith("]"));
            Assert.That(text, Does.Contain(" 2 ").And.Contain("<b>").And.Contain("</b>"));
            Assert.That(text, Does.Not.Contain("Start"), "vowels are accented, so untranslated text stands out");
        }

        [Test]
        public void ShortDatesFollowTheLanguage()
        {
            var date = new DateTime(2026, 11, 2);
            Assert.That(Locale.ShortDate(date), Is.EqualTo("2 Nov"));
            Locale.Language = "es";
            Assert.That(Locale.ShortDate(date), Is.EqualTo("2 nov"));
        }

        [Test]
        public void TheCollectorFindsEveryKindOfCall()
        {
            const string source = @"
                // Tr.Get(""an example in a comment"")
                [SerializeField] private string _joinText = ""Press A to join"";
                [SerializeField] private string _unshown = ""Never translated"";
                void Draw()
                {
                    Show(Tr.Get(_joinText));
                    Label(Tr.Get(""Settings""), Tr.Format(""Day {day}"", (""day"", 1)));
                    Pill(Tr.Plural(count, ""1 day"", ""{n} days""), Game.Data.Tr.Get(""Say \""hi\""""));
                    Hob(Tr.In(""heat"", ""medium""));
                }";
            var keys = TranslationKeys.InCode(source).ToList();
            Assert.That(keys, Is.EquivalentTo(new[] { "Press A to join", "Settings", "Day {day}", "1 day", "{n} days", "Say \"hi\"", "heat|medium" }));
        }

        [Test]
        public void MergingKeepsTranslationsAndMarksWhatsGone()
        {
            var old = "english,translation,note\nSettings,Ajustes,ui\nOld text,Texto viejo,ui\n";
            var merged = TranslationKeys.Merge(old, new[] { ("Settings", "ui: MainMenu"), ("Credits", "ui: MainMenu") });
            var rows = Csv.Read(merged).ToList();
            Assert.That(rows[1], Is.EqualTo(new[] { "Settings", "Ajustes", "ui" }), "a translator's note is kept");
            Assert.That(rows[2], Is.EqualTo(new[] { "Credits", "", "ui: MainMenu" }));
            Assert.That(rows[3], Is.EqualTo(new[] { "Old text", "Texto viejo", TranslationKeys.Unused }));
            Assert.That(TranslationKeys.Missing(merged), Is.EqualTo(new[] { "Credits" }));
        }

        [Test]
        public void EveryTextInTheCodeHasASpanishTranslation()
        {
            var table = File.ReadAllText(SpanishTable);
            Translations.LoadCsv("es", table);
            var missing = Directory.GetFiles("Assets/_Project/Scripts", "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Replace('\\', '/').Contains("/Editor/") && !f.EndsWith("CookingDebugOverlay.cs"))
                .SelectMany(f => TranslationKeys.InCode(File.ReadAllText(f)))
                .Distinct()
                .Where(key => !Translations.Has("es", key))
                .ToList();
            Assert.That(missing, Is.Empty, "Run Tools/Game/Update Language Tables, then translate:\n" + string.Join("\n", missing));
        }

        [Test]
        public void EverySpanishTranslationKeepsItsSlots()
        {
            var wrong = Csv.Read(File.ReadAllText(SpanishTable)).Skip(1)
                .Where(r => r.Count > 1 && !string.IsNullOrEmpty(r[1]) && Slots(r[0]) != Slots(r[1]))
                .Select(r => $"{r[0]} → {r[1]}")
                .ToList();
            Assert.That(wrong, Is.Empty, string.Join("\n", wrong));
        }

        private static string Slots(string text) =>
            string.Join(",", Regex.Matches(text, @"\{\w+\}").Cast<Match>().Select(m => m.Value).OrderBy(s => s));
    }
}
