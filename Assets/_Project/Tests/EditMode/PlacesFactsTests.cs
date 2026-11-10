using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>forkcast's listing card: the facts it shows, real and made up (2026-11-10).</summary>
    public class PlacesFactsTests
    {
        [Test]
        public void The_same_restaurant_always_gets_the_same_made_up_details()
        {
            Assert.That(PlacesFacts.Pick("The Copper Pan", "address", 4), Is.EqualTo(PlacesFacts.Pick("The Copper Pan", "address", 4)));
        }

        [Test]
        public void A_pick_is_always_one_of_the_lines()
        {
            for (var i = 0; i < 50; i++)
                Assert.That(PlacesFacts.Pick("Restaurant " + i, "joke", 3), Is.InRange(0, 2));
            Assert.That(PlacesFacts.Pick("x", "joke", 0), Is.Zero);
        }

        [Test]
        public void Price_per_person_spans_the_menu_rounded_to_fives()
        {
            Assert.That(PlacesFacts.PricePerPerson(new[] { 16f, 24f }), Is.EqualTo((15, 25)));
            Assert.That(PlacesFacts.PricePerPerson(new[] { 20f }), Is.EqualTo((20, 25)), "one price: a five-wide band");
            Assert.That(PlacesFacts.PricePerPerson(new float[0]), Is.Null);
            Assert.That(PlacesFacts.PricePerPerson(new[] { 0f }), Is.Null);
        }
    }
}
