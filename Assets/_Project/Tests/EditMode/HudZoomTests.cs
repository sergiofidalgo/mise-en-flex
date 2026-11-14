using Game.Simulation.Display;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class HudZoomTests
    {
        private static readonly HudZoomSettings Settings = new HudZoomSettings();

        // The camera's shots (decisions 2026-11-08 to 14): kitchen, whole restaurant, street, road.
        [TestCase(7.9f, HudBand.Near)]
        [TestCase(9.75f, HudBand.Middle)]
        [TestCase(14.6f, HudBand.Far)]
        [TestCase(23.7f, HudBand.Far)]
        public void EachShotIsInItsBand(float height, HudBand band)
        {
            var zoom = new HudZoom();
            zoom.Tick(height, 0.016f, Settings);
            Assert.AreEqual(band, zoom.Band);
        }

        [Test]
        public void HoveringBetweenThresholdsKeepsTheBandItCameFrom()
        {
            // 8.8 m is between NearBelow (8.6) and MiddleFrom (9): whichever band it was in, it stays.
            Assert.AreEqual(HudBand.Near, HudZoom.BandAt(8.8f, HudBand.Near, Settings));
            Assert.AreEqual(HudBand.Middle, HudZoom.BandAt(8.8f, HudBand.Middle, Settings));
            Assert.AreEqual(HudBand.Middle, HudZoom.BandAt(13f, HudBand.Middle, Settings));
            Assert.AreEqual(HudBand.Far, HudZoom.BandAt(13f, HudBand.Far, Settings));
        }

        [Test]
        public void ComingDownFromFarPastBothThresholdsLandsNear()
        {
            Assert.AreEqual(HudBand.Near, HudZoom.BandAt(7.9f, HudBand.Far, Settings));
            Assert.AreEqual(HudBand.Far, HudZoom.BandAt(20f, HudBand.Near, Settings));
        }

        [Test]
        public void SharedPiecesShrinkWithTheSceneDownToTheSmallestSize()
        {
            Assert.AreEqual(1f, HudZoom.ScaleAt(7.9f, Settings), 1e-4f);
            Assert.AreEqual(1f, HudZoom.ScaleAt(6.5f, Settings), 1e-4f, "never bigger than full");
            Assert.AreEqual(7.9f / 9.75f, HudZoom.ScaleAt(9.75f, Settings), 1e-4f);
            Assert.AreEqual(Settings.SmallestScale, HudZoom.ScaleAt(23.7f, Settings), 1e-4f);
        }

        [Test]
        public void TheFirstTickLandsAtOnce()
        {
            var zoom = new HudZoom();
            zoom.Tick(14.6f, 0.016f, Settings);
            Assert.AreEqual(new HudLook(Settings.SmallestScale, 1f, 0f), zoom.Shared);
        }

        [Test]
        public void ChangingBandEasesOverTheChangeTimeWithoutJumping()
        {
            var zoom = new HudZoom();
            zoom.Tick(7.9f, 0.016f, Settings);
            zoom.Tick(9.75f, Settings.ChangeSeconds / 2f, Settings);
            Assert.AreEqual(HudBand.Middle, zoom.Band);
            Assert.AreEqual(0.5f, zoom.Simple, 1e-4f, "halfway to the simple form");
            zoom.Tick(9.75f, Settings.ChangeSeconds, Settings);
            Assert.AreEqual(1f, zoom.Simple, 1e-4f);
            Assert.AreEqual(0f, zoom.Hidden, 1e-4f, "the middle band doesn't fade");
        }

        [Test]
        public void TheFarBandFadesSharedPiecesOut()
        {
            var zoom = new HudZoom();
            zoom.Tick(9.75f, 0.016f, Settings);
            for (var i = 0; i < 30; i++)
                zoom.Tick(14.6f, 0.016f, Settings);
            Assert.AreEqual(0f, zoom.Shared.Alpha, 1e-4f);
        }

        [Test]
        public void UrgentPiecesStayFullAtEveryHeight()
        {
            foreach (var height in new[] { 7.9f, 9.75f, 14.6f, 23.7f })
            {
                var zoom = new HudZoom();
                zoom.Tick(height, 0.016f, Settings);
                Assert.AreEqual(HudLook.Full, zoom.For(1f), $"at {height} m");
            }
        }

        [Test]
        public void HalfUrgentIsHalfwayBetweenSharedAndFull()
        {
            var zoom = new HudZoom();
            zoom.Tick(23.7f, 0.016f, Settings);
            var look = zoom.For(0.5f);
            Assert.AreEqual((Settings.SmallestScale + 1f) / 2f, look.Scale, 1e-4f);
            Assert.AreEqual(0.5f, look.Simple, 1e-4f);
            Assert.AreEqual(0.5f, look.Alpha, 1e-4f);
        }

        [Test]
        public void EaseMovesAtAFixedRateAndStopsAtTheTarget()
        {
            Assert.AreEqual(0.4f, HudZoom.Ease(0f, 1f, 0.1f, 0.25f), 1e-4f);
            Assert.AreEqual(1f, HudZoom.Ease(0.9f, 1f, 0.1f, 0.25f), 1e-4f);
            Assert.AreEqual(0.6f, HudZoom.Ease(1f, 0f, 0.1f, 0.25f), 1e-4f);
            Assert.AreEqual(1f, HudZoom.Ease(0f, 1f, 0.1f, 0f), 1e-4f, "no time: at once");
        }
    }
}
