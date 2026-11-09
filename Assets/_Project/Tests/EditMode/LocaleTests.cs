using Game.Data;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Currency follows the language (user's call, 2026-11-09).</summary>
    public class LocaleTests
    {
        [TearDown]
        public void BackToEnglish() => Locale.Language = "en";

        [Test]
        public void EnglishWritesDollars()
        {
            Assert.That(Locale.Money(2340f), Is.EqualTo("$2,340"));
            Assert.That(Locale.Money(37.6f), Is.EqualTo("$37.60"), "cents when there are any");
            Assert.That(Locale.Money(-62.4f), Is.EqualTo("−$62.40"));
            Assert.That(Locale.Money(68f, MoneyStyle.Cents, signed: true), Is.EqualTo("+$68.00"));
            Assert.That(Locale.Money(37.6f, MoneyStyle.Whole), Is.EqualTo("$38"));
            Assert.That(Locale.PriceBand(2), Is.EqualTo("$$"));
        }

        [Test]
        public void SpanishWritesEurosItsOwnWay()
        {
            Locale.Language = "es";
            Assert.That(Locale.Money(2340f), Is.EqualTo("2.340 €"));
            Assert.That(Locale.Money(2340.5f), Is.EqualTo("2.340,50 €"));
            Assert.That(Locale.Money(-12f), Is.EqualTo("−12 €"));
            Assert.That(Locale.PriceBand(2), Is.EqualTo("€€"));
        }

        [Test]
        public void ZeroNeverHasASign()
        {
            Assert.That(Locale.Money(-0.001f, MoneyStyle.Whole), Is.EqualTo("$0"));
        }

        [Test]
        public void NamesAndMoneyShareTheLanguage()
        {
            GuestNames.Language = "es";
            Assert.That(Locale.Language, Is.EqualTo("es"));
            Assert.That(Locale.CurrencySymbol, Is.EqualTo("€"));
        }
    }
}
