using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Data
{
    /// <summary>How an amount is written: whole, with cents, or with cents only when there are any.</summary>
    public enum MoneyStyle
    {
        Auto,
        Whole,
        Cents,
    }

    /// <summary>
    /// The player's language and what follows from it (user's call, 2026-11-09): guests' names
    /// (GuestNames) and how money is written. Currency follows the language: dollars in English,
    /// euros in Spanish, each with its own separators and symbol position. Amounts are the same
    /// numbers in every language; only how they're written changes. The language comes from the
    /// player's settings; text itself is translated by <see cref="Tr"/>.
    /// </summary>
    public static class Locale
    {
        private sealed class MoneyFormat
        {
            public string Symbol;
            public bool SymbolFirst;
            public string Space = "";
            public NumberFormatInfo Numbers;
        }

        private static readonly Dictionary<string, MoneyFormat> Formats = new Dictionary<string, MoneyFormat>
        {
            // $2,340.50
            ["en"] = new MoneyFormat { Symbol = "$", SymbolFirst = true, Numbers = new NumberFormatInfo { NumberGroupSeparator = ",", NumberDecimalSeparator = "." } },
            // 2.340,50 €
            ["es"] = new MoneyFormat { Symbol = "€", SymbolFirst = false, Space = " ", Numbers = new NumberFormatInfo { NumberGroupSeparator = ".", NumberDecimalSeparator = "," } },
        };

        /// <summary>The language, by code ("en", "es"), set from the player's settings.</summary>
        public static string Language { get; set; } = "en";

        public static IEnumerable<string> Languages => Formats.Keys;

        private static MoneyFormat Format => Formats.TryGetValue(Language, out var format) ? format : Formats["en"];

        public static string CurrencySymbol => Format.Symbol;

        /// <summary>
        /// An amount as the player's language writes it, e.g. "$2,340" or "2.340 €"; negative ones
        /// with a minus sign ("−$12"). With <paramref name="signed"/>, positive ones get a plus.
        /// </summary>
        public static string Money(float amount, MoneyStyle style = MoneyStyle.Auto, bool signed = false)
        {
            var format = Format;
            var size = Math.Abs(amount);
            var cents = style == MoneyStyle.Cents || style == MoneyStyle.Auto && Math.Abs(size - Math.Round(size)) > 0.004f;
            var number = size.ToString(cents ? "#,0.00" : "#,0", format.Numbers);
            var written = format.SymbolFirst ? format.Symbol + format.Space + number : number + format.Space + format.Symbol;
            var sign = amount < 0f && number.Trim('0', '.', ',') != "" ? "−" : signed && amount > 0f ? "+" : "";
            return sign + written;
        }

        private static readonly Dictionary<string, string[]> Months = new Dictionary<string, string[]>
        {
            ["en"] = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" },
            ["es"] = new[] { "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sept", "oct", "nov", "dic" },
        };

        /// <summary>A short date as the language writes it: "2 Nov", "2 nov".</summary>
        public static string ShortDate(DateTime date)
        {
            var months = Months.TryGetValue(Language, out var names) ? names : Months["en"];
            return $"{date.Day} {months[date.Month - 1]}";
        }

        /// <summary>A price band as listings show it: "$$" in English, "€€" in Spanish.</summary>
        public static string PriceBand(int band) => string.Concat(System.Linq.Enumerable.Repeat(Format.Symbol, Math.Max(1, band)));
    }
}
