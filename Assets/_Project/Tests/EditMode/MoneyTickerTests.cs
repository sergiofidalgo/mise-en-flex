using Game.Data;
using Game.Presentation.Hud;
using Game.Simulation.Days;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>The money card's number and animation (decision 2026-11-10).</summary>
    public class MoneyTickerTests
    {
        private static MoneyTickerSettings Settings => new MoneyTickerSettings
        {
            RollSeconds = 0.6f,
            HoldSeconds = 0.8f,
            FadeSeconds = 0.4f,
            ShakeSeconds = 0.4f,
            ShakeDistance = 6f,
            ShakeWiggles = 3f,
        };

        private static MoneyTicker At(float balance)
        {
            var ticker = new MoneyTicker(Settings);
            ticker.Set(balance);
            return ticker;
        }

        [TearDown]
        public void BackToEnglish() => Locale.Language = "en";

        [Test]
        public void TheFirstBalanceShowsAtOnce()
        {
            var ticker = At(1250f);
            Assert.That(ticker.Shown, Is.EqualTo(1250f));
            Assert.That(ticker.Direction, Is.EqualTo(MoneyChange.None));
            Assert.That(ticker.Tint, Is.EqualTo(0f));
            Assert.That(ticker.Animating, Is.False);
        }

        [Test]
        public void MoneyInRollsUpGreenThenSettles()
        {
            var ticker = At(100f);
            ticker.Set(200f);
            Assert.That(ticker.Direction, Is.EqualTo(MoneyChange.Up));
            Assert.That(ticker.Shown, Is.EqualTo(100f), "starts from what was on the card");
            Assert.That(ticker.Tint, Is.EqualTo(1f));

            ticker.Tick(0.3f);
            Assert.That(ticker.Shown, Is.GreaterThan(150f).And.LessThan(200f), "eases out: past halfway at half time");
            Assert.That(ticker.Offset, Is.EqualTo(0f), "going up never shakes");

            ticker.Tick(0.3f);
            Assert.That(ticker.Shown, Is.EqualTo(200f));
            Assert.That(ticker.Tint, Is.EqualTo(1f), "holds green after landing");

            ticker.Tick(0.8f + 0.2f);
            Assert.That(ticker.Tint, Is.EqualTo(0.5f).Within(0.01f), "halfway through the fade");

            ticker.Tick(0.3f);
            Assert.That(ticker.Tint, Is.EqualTo(0f));
            Assert.That(ticker.Animating, Is.False);
        }

        [Test]
        public void TheRollNeverOvershootsOrGoesBack()
        {
            var ticker = At(0f);
            ticker.Set(1000f);
            var last = 0f;
            for (var i = 0; i < 40; i++)
            {
                ticker.Tick(1f / 60f);
                Assert.That(ticker.Shown, Is.GreaterThanOrEqualTo(last).And.LessThanOrEqualTo(1000f));
                last = ticker.Shown;
            }
            Assert.That(ticker.Shown, Is.EqualTo(1000f));
        }

        [Test]
        public void MoneyOutChangesAtOnceAndShakesRed()
        {
            var ticker = At(500f);
            ticker.Set(420f);
            Assert.That(ticker.Direction, Is.EqualTo(MoneyChange.Down));
            Assert.That(ticker.Shown, Is.EqualTo(420f), "the new figure is readable straight away");
            Assert.That(ticker.Tint, Is.EqualTo(1f));

            var widest = 0f;
            for (var t = 0; t < 24; t++) // 0.4 s at 60 fps
            {
                ticker.Tick(1f / 60f);
                widest = System.Math.Max(widest, System.Math.Abs(ticker.Offset));
            }
            Assert.That(widest, Is.GreaterThan(2f).And.LessThanOrEqualTo(6f), "gentle: never more than the set distance");
            Assert.That(ticker.Offset, Is.EqualTo(0f).Within(0.001f), "ends centred");

            ticker.Tick(0.4f);
            Assert.That(ticker.Tint, Is.EqualTo(0f));
            Assert.That(ticker.Animating, Is.False);
        }

        [Test]
        public void TheShakeDiesDown()
        {
            var ticker = At(500f);
            ticker.Set(400f);
            ticker.Tick(0.025f); // a quarter of the first wiggle: near its peak
            var early = System.Math.Abs(ticker.Offset);
            ticker.Tick(0.3f); // a quarter of the last wiggle
            var late = System.Math.Abs(ticker.Offset);
            Assert.That(late, Is.LessThan(early));
        }

        [Test]
        public void CentsAloneDontFlashTheCard()
        {
            var ticker = At(100f);
            ticker.Set(100.3f);
            Assert.That(ticker.Direction, Is.EqualTo(MoneyChange.None));
            Assert.That(ticker.Tint, Is.EqualTo(0f));
            Assert.That(ticker.Shown, Is.EqualTo(100.3f));
        }

        [Test]
        public void ADropMidRollStartsFromWhatsShown()
        {
            var ticker = At(100f);
            ticker.Set(300f);
            ticker.Tick(0.2f);
            var midway = ticker.Shown;
            ticker.Set(150f);
            Assert.That(ticker.Direction, Is.EqualTo(midway > 150f ? MoneyChange.Down : MoneyChange.Up));
            Assert.That(ticker.Shown, Is.EqualTo(150f));
        }

        [Test]
        public void AnotherPaymentMidRollKeepsRollingFromWhereItIs()
        {
            var ticker = At(100f);
            ticker.Set(200f);
            ticker.Tick(0.3f);
            var midway = ticker.Shown;
            ticker.Set(260f);
            Assert.That(ticker.Direction, Is.EqualTo(MoneyChange.Up));
            Assert.That(ticker.Shown, Is.EqualTo(midway), "no jump back or forward");
            ticker.Tick(0.6f);
            Assert.That(ticker.Shown, Is.EqualTo(260f));
        }

        [Test]
        public void PayingOffDebtRollsUpThroughZero()
        {
            var ticker = At(-120f);
            ticker.Set(80f);
            Assert.That(ticker.Direction, Is.EqualTo(MoneyChange.Up));
            ticker.Tick(0.6f);
            Assert.That(ticker.Shown, Is.EqualTo(80f));
        }

        [Test]
        public void SnapShowsAnotherCampaignWithoutAnimating()
        {
            var ticker = At(100f);
            ticker.Set(50f);
            ticker.Snap(900f);
            Assert.That(ticker.Shown, Is.EqualTo(900f));
            Assert.That(ticker.Direction, Is.EqualTo(MoneyChange.None));
            Assert.That(ticker.Animating, Is.False);
        }

        [Test]
        public void ZeroTimingsJumpStraightToTheEnd()
        {
            var ticker = new MoneyTicker(default);
            ticker.Set(10f);
            ticker.Set(90f);
            Assert.That(ticker.Shown, Is.EqualTo(90f));
            Assert.That(ticker.Tint, Is.EqualTo(0f));
            ticker.Set(20f);
            Assert.That(ticker.Offset, Is.EqualTo(0f));
            Assert.That(ticker.Tint, Is.EqualTo(0f));
        }

        [Test]
        public void TheCardWritesWholeMoneyInThePlayersCurrency()
        {
            Assert.That(MoneyTicker.Format(1250.4f), Is.EqualTo("$1,250"));
            Assert.That(MoneyTicker.Format(-120f), Is.EqualTo("−$120"), "owed to the boss: a minus sign");
            Assert.That(MoneyTicker.Format(-0.3f), Is.EqualTo("$0"));
            Locale.Language = "es";
            Assert.That(MoneyTicker.Format(1250f), Is.EqualTo("1.250 €"));
            Assert.That(MoneyTicker.Format(-120f), Is.EqualTo("−120 €"));
        }

        [Test]
        public void ServiceTakingsCountAsTheyreMade()
        {
            Assert.That(KitchenMoneySource.Live(500f, DayPhase.Service, 84f), Is.EqualTo(584f));
            Assert.That(KitchenMoneySource.Live(500f, DayPhase.Service, -20f), Is.EqualTo(480f), "a walk-out costs straight away");
            Assert.That(KitchenMoneySource.Live(500f, DayPhase.Prep, 84f), Is.EqualTo(500f));
            Assert.That(KitchenMoneySource.Live(584f, DayPhase.Review, 84f), Is.EqualTo(584f), "banked by then: not counted twice");
            Assert.That(KitchenMoneySource.Live(-40f, DayPhase.Plan, 0f), Is.EqualTo(-40f));
        }

        [Test]
        public void TheStyleHoldsTheAgreedTimings()
        {
            var style = ScriptableObject.CreateInstance<HudStyle>();
            try
            {
                var settings = style.MoneyTicker;
                var agreed = MoneyTickerSettings.Default;
                Assert.That(settings.RollSeconds, Is.EqualTo(agreed.RollSeconds));
                Assert.That(settings.HoldSeconds, Is.EqualTo(agreed.HoldSeconds));
                Assert.That(settings.FadeSeconds, Is.EqualTo(agreed.FadeSeconds));
                Assert.That(settings.ShakeSeconds, Is.EqualTo(agreed.ShakeSeconds));
                Assert.That(settings.ShakeDistance, Is.EqualTo(agreed.ShakeDistance));
                Assert.That(settings.ShakeWiggles, Is.EqualTo(agreed.ShakeWiggles));
                Assert.That(style.MoneyCardHeight, Is.EqualTo(108f), "a player card's height");
            }
            finally
            {
                Object.DestroyImmediate(style);
            }
        }
    }
}
