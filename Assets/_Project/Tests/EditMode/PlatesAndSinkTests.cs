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
        public void TheSinkHoldsOnlySoManyPlatesDirtyOrClean()
        {
            var sink = new Sink(capacity: 2);
            sink.Put(Dirty());
            sink.Put(Dirty());
            Assert.That(sink.CanAccept(Dirty()), Is.False, "full");

            sink.Wash(1f);
            Assert.That(sink.CanAccept(Dirty()), Is.False, "a clean plate still takes up room");
            sink.Take();
            Assert.That(sink.CanAccept(Dirty()));
        }

        [Test]
        public void TheDirtyPileHasALimit()
        {
            var pile = new DirtyPile(2);
            pile.Put(Dirty());
            pile.Put(Dirty());
            Assert.That(pile.CanAccept(Dirty()), Is.False);
            Assert.That(pile.CanAccept(new Plate(3)), Is.False, "clean plates don't go here");

            pile.Take();
            Assert.That(pile.CanAccept(Dirty()));
        }

        [Test]
        public void TheCleanStackHasALimit()
        {
            var stack = new PlateStack(2, 3, capacity: 2);
            Assert.That(stack.CanAccept(new Plate(3)), Is.False);
            stack.Take();
            Assert.That(stack.CanAccept(new Plate(3)));
        }

        [Test]
        public void ThePilesAndSinkHoldWhatTheDesignSays()
        {
            var equipment = UnityEditor.AssetDatabase.LoadAssetAtPath<Game.Data.EquipmentTier>("Assets/_Project/Data/Settings/EquipmentTier_Basic.asset");
            Assert.That(equipment.DirtyPileCapacity, Is.EqualTo(6));
            Assert.That(equipment.CleanPileCapacity, Is.EqualTo(6));
            Assert.That(equipment.SinkCapacity, Is.EqualTo(2));
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
