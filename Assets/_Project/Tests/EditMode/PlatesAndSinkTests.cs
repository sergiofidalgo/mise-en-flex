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
        public void AWashedPlateComesOutCleanToWhoeverFinishedIt()
        {
            var sink = new Sink();
            var plate = Dirty();
            sink.Put(plate);

            Assert.That(sink.Wash(0.5f), Is.Null);
            Assert.That(sink.WashProgress, Is.EqualTo(0.5f));
            Assert.That(sink.Wash(0.5f), Is.SameAs(plate));
            Assert.That(plate.IsDirty, Is.False);
            Assert.That(sink.DirtyCount, Is.EqualTo(0), "the sink keeps no plates");
            Assert.That(sink.CanTake, Is.False);
            Assert.That(sink.WashProgress, Is.EqualTo(0f));
        }

        [Test]
        public void WashingAnEmptySinkDoesNothing()
        {
            var sink = new Sink();
            Assert.That(sink.Wash(1f), Is.Null);
            Assert.That(sink.WashProgress, Is.EqualTo(0f));
        }

        [Test]
        public void TheSinkWashesOnePlateAtATime()
        {
            var sink = new Sink();
            sink.Put(Dirty());
            Assert.That(sink.CanAccept(Dirty()), Is.False, "busy");

            sink.Wash(1f);
            Assert.That(sink.CanAccept(Dirty()));
        }

        [Test]
        public void DirtyPlatesPileUpOnACounterToItsLimit()
        {
            var counter = new ItemSlot(3);
            var bottom = Dirty();
            counter.Put(bottom);
            counter.Put(Dirty());
            var top = Dirty();
            counter.Put(top);

            Assert.That(counter.Count, Is.EqualTo(3));
            Assert.That(counter.Current, Is.SameAs(top));
            Assert.That(counter.IndexOf(bottom), Is.EqualTo(0));
            Assert.That(counter.IndexOf(top), Is.EqualTo(2));
            Assert.That(counter.CanAccept(Dirty()), Is.False, "full");

            Assert.That(counter.Take(), Is.SameAs(top), "from the top");
            Assert.That(counter.Count, Is.EqualTo(2));
            Assert.That(counter.CanAccept(Dirty()));
        }

        [Test]
        public void OnlyDirtyPlatesPileUp()
        {
            var counter = new ItemSlot(3);
            counter.Put(Dirty());
            Assert.That(counter.CanAccept(new Plate(3)), Is.False, "a clean plate doesn't go on a dirty one");
            Assert.That(counter.CanAccept(new Item("Steak")), Is.False);

            var clean = new ItemSlot(3);
            clean.Put(new Plate(3));
            Assert.That(clean.CanAccept(Dirty()), Is.False, "nor a dirty one on a clean one");
        }

        [Test]
        public void HandsHoldOnePlateEvenDirty()
        {
            var hands = new ItemSlot();
            hands.Put(Dirty());
            Assert.That(hands.CanAccept(Dirty()), Is.False);
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
        public void TheCleanStackHoldsWhatTheDesignSays()
        {
            var equipment = UnityEditor.AssetDatabase.LoadAssetAtPath<Game.Data.EquipmentTier>("Assets/_Project/Data/Settings/EquipmentTier_Basic.asset");
            Assert.That(equipment.CleanPileCapacity, Is.EqualTo(6));
        }

        [Test]
        public void BreakingAPlateLosesItForGood()
        {
            var sink = new Sink();
            sink.CountBroken();
            Assert.That(sink.Broken, Is.EqualTo(1));
            Assert.That(sink.TakeBrokenCount(), Is.EqualTo(1));
            Assert.That(sink.Broken, Is.EqualTo(0), "replaced in the morning");
        }
    }
}
