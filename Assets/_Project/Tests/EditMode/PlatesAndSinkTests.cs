using Game.Simulation.Items;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class PlatesAndSinkTests
    {
        private static Plate Dirty()
        {
            var plate = new Plate(3);
            plate.MarkDirty();
            return plate;
        }

        [Test]
        public void TheStackRunsOut()
        {
            var stack = new PlateStack(2, 3);
            stack.Take();
            Assert.That(stack.CanTake);
            stack.Take();
            Assert.That(stack.CanTake, Is.False);
        }

        [Test]
        public void OnlyCleanEmptyPlatesGoBackOnTheStack()
        {
            var stack = new PlateStack(0, 3);
            var withFood = new Plate(3);
            withFood.Put(new Item("Steak"));

            Assert.That(stack.CanAccept(Dirty()), Is.False);
            Assert.That(stack.CanAccept(withFood), Is.False);
            Assert.That(stack.CanAccept(new Item("Steak")), Is.False);
            Plate returned = null;
            stack.Returned += p => returned = p;
            var clean = new Plate(3);
            stack.Put(clean);
            Assert.That(stack.Count, Is.EqualTo(1));
            Assert.That(returned, Is.SameAs(clean));
        }

        [Test]
        public void ADirtyPlateLosesItsFoodAndTakesNoMore()
        {
            var plate = new Plate(3);
            plate.Put(new Item("Steak"));
            plate.MarkDirty();

            Assert.That(plate.IsDirty);
            Assert.That(plate.Items, Is.Empty);
            Assert.That(plate.CanAccept(new Item("Steak")), Is.False);
            plate.Clean();
            Assert.That(plate.CanAccept(new Item("Steak")));
        }

        [Test]
        public void TheSinkOnlyTakesDirtyPlates()
        {
            var sink = new Sink();
            Assert.That(sink.CanAccept(new Plate(3)), Is.False);
            Assert.That(sink.CanAccept(new Item("Steak")), Is.False);
            Assert.That(sink.CanAccept(Dirty()));
            Assert.That(sink.CanTake, Is.False);
        }

        [Test]
        public void WashingFinishesOnePlateAtATime()
        {
            var sink = new Sink();
            var first = Dirty();
            var second = Dirty();
            sink.Put(first);
            sink.Put(second);

            Assert.That(sink.Wash(0.5f), Is.Null);
            Assert.That(sink.WashProgress, Is.EqualTo(0.5f));
            Assert.That(sink.Wash(0.5f), Is.SameAs(first), "oldest first");
            Assert.That(first.IsDirty, Is.False);
            Assert.That(sink.DirtyCount, Is.EqualTo(1));
            Assert.That(sink.CleanCount, Is.EqualTo(1));
            Assert.That(sink.WashProgress, Is.EqualTo(0f));
            Assert.That(sink.Take(), Is.SameAs(first));
        }

        [Test]
        public void WashingAnEmptySinkDoesNothing()
        {
            var sink = new Sink();
            Assert.That(sink.Wash(1f), Is.Null);
            Assert.That(sink.WashProgress, Is.EqualTo(0f));
        }

        [Test]
        public void BreakingAPlateLosesItForGood()
        {
            var sink = new Sink();
            sink.Put(Dirty());
            sink.Wash(1f);
            sink.BreakClean();

            Assert.That(sink.CleanCount, Is.EqualTo(0));
            Assert.That(sink.Broken, Is.EqualTo(1));
        }
    }
}
