using Game.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class CloseUpPanelLayoutTests
    {
        private static readonly Vector2 Screen = new Vector2(1920f, 1080f);
        private const float Size = 576f; // 30% of 1920
        private const float Margin = 24f;

        private static Rect Place(Vector2 station) => CloseUpPanelLayout.Place(station, Screen, Size, Margin);

        private static bool InsideScreen(Rect rect) =>
            rect.xMin >= Margin - 0.01f && rect.yMin >= Margin - 0.01f &&
            rect.xMax <= Screen.x - Margin + 0.01f && rect.yMax <= Screen.y - Margin + 0.01f;

        [Test]
        public void StaysInsideTheScreenWherever()
        {
            foreach (var station in new[] { new Vector2(10f, 10f), new Vector2(1900f, 1070f), new Vector2(960f, 1060f), new Vector2(5f, 540f) })
                Assert.That(InsideScreen(Place(station)), $"Panel off screen for a station at {station}");
        }

        [Test]
        public void DoesNotCoverTheStationWhenThereIsRoom()
        {
            var station = new Vector2(1500f, 900f); // a hob on the back wall, right of centre
            Assert.That(Place(station).Contains(station), Is.False);
        }

        [Test]
        public void SitsOnTheScreenCentresSideOfTheStation()
        {
            var station = new Vector2(1500f, 900f);
            var panel = Place(station);
            Assert.That(panel.center.x, Is.LessThan(station.x));
            Assert.That(panel.center.y, Is.LessThan(station.y));
        }

        [Test]
        public void IsSquareOfTheRequestedSize()
        {
            var panel = Place(new Vector2(400f, 300f));
            Assert.That(panel.width, Is.EqualTo(Size).Within(0.01f));
            Assert.That(panel.height, Is.EqualTo(Size).Within(0.01f));
        }

        [Test]
        public void StationAtTheScreenCentreStillGetsAPanel()
        {
            Assert.That(InsideScreen(Place(Screen / 2f)));
        }
    }
}
