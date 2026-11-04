using Game.Simulation.Items;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class PassCounterTests
    {
        private static Plate PlateOfFood()
        {
            var plate = new Plate(3);
            plate.Put(new Item("Steak"));
            return plate;
        }

        [Test]
        public void OnlyACleanPlateWithFoodGoesOnACounter()
        {
            var counter = new PassCounter();
            Assert.That(counter.CanAccept(new Plate(3)), Is.False, "empty");
            Assert.That(counter.CanAccept(new Item("Steak")), Is.False);

            var dirty = new Plate(3);
            dirty.MarkDirty();
            Assert.That(counter.CanAccept(dirty), Is.False);
            Assert.That(counter.CanAccept(PlateOfFood()));
        }

        [Test]
        public void AWaitingPlateCanBeTakenBack()
        {
            var counter = new PassCounter();
            var plate = PlateOfFood();
            counter.Put(plate);

            Assert.That(counter.IsWaitingForCustomer);
            Assert.That(counter.HasDirtyPlate, Is.False);
            Assert.That(counter.CanTake);
            Assert.That(counter.Take(), Is.SameAs(plate));
            Assert.That(counter.Current, Is.Null);
            Assert.That(plate.Items, Has.Count.EqualTo(1), "food still on it");
        }

        [Test]
        public void ACustomersPlateStaysUntilTheyFinish()
        {
            var counter = new PassCounter();
            counter.Put(PlateOfFood());
            counter.StartEating();

            Assert.That(counter.CanTake, Is.False);
            Assert.That(counter.IsWaitingForCustomer, Is.False);
            Assert.That(counter.CanAccept(PlateOfFood()), Is.False);

            counter.FinishEating();
            Assert.That(counter.HasDirtyPlate);
            Assert.That(counter.CanTake);
            Assert.That(counter.CanAccept(PlateOfFood()), Is.False, "it still blocks the stool");

            var dirty = counter.TakeDirtyPlate();
            Assert.That(dirty.IsDirty);
            Assert.That(counter.CanAccept(PlateOfFood()));
        }

        [Test]
        public void ThereAreNoDirtyPlatesToCollectFromAWaitingPlate()
        {
            var counter = new PassCounter();
            counter.Put(PlateOfFood());
            Assert.That(counter.HasDirtyPlate, Is.False);
            Assert.Throws<System.InvalidOperationException>(() => counter.TakeDirtyPlate());
        }
    }
}
