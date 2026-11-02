using Game.Simulation.Items;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class ItemTransferTests
    {
        private ItemSlot _hands;

        [SetUp]
        public void CreateHands() => _hands = new ItemSlot();

        [Test]
        public void EmptyHandsTakeAFreshItemFromACrate()
        {
            var crate = new ItemDispenser("Tomato");

            var outcome = ItemTransfer.Interact(_hands, crate);

            Assert.That(outcome, Is.EqualTo(InteractionOutcome.PickedUp));
            Assert.That(_hands.Current.TypeId, Is.EqualTo("Tomato"));
        }

        [Test]
        public void CrateGivesADifferentItemEachTime()
        {
            var crate = new ItemDispenser("Tomato");
            Assert.That(crate.Take(), Is.Not.SameAs(crate.Take()));
        }

        [Test]
        public void FullHandsPlaceOnAnEmptyCounter()
        {
            var counter = new ItemSlot();
            var tomato = new Item("Tomato");
            _hands.Put(tomato);

            var outcome = ItemTransfer.Interact(_hands, counter);

            Assert.That(outcome, Is.EqualTo(InteractionOutcome.Placed));
            Assert.That(_hands.IsEmpty);
            Assert.That(counter.Current, Is.SameAs(tomato));
        }

        [Test]
        public void EmptyHandsPickUpFromACounter()
        {
            var counter = new ItemSlot();
            var onion = new Item("Onion");
            counter.Put(onion);

            var outcome = ItemTransfer.Interact(_hands, counter);

            Assert.That(outcome, Is.EqualTo(InteractionOutcome.PickedUp));
            Assert.That(_hands.Current, Is.SameAs(onion));
            Assert.That(counter.IsEmpty);
        }

        [Test]
        public void FullHandsCantPlaceOnAFullCounter()
        {
            var counter = new ItemSlot();
            var onCounter = new Item("Onion");
            var inHands = new Item("Tomato");
            counter.Put(onCounter);
            _hands.Put(inHands);

            var outcome = ItemTransfer.Interact(_hands, counter);

            Assert.That(outcome, Is.EqualTo(InteractionOutcome.Nothing));
            Assert.That(_hands.Current, Is.SameAs(inHands));
            Assert.That(counter.Current, Is.SameAs(onCounter));
        }

        [Test]
        public void EmptyHandsAtAnEmptyCounterDoNothing()
        {
            Assert.That(ItemTransfer.Interact(_hands, new ItemSlot()), Is.EqualTo(InteractionOutcome.Nothing));
        }

        [Test]
        public void CratesDontTakeItemsBack()
        {
            var tomato = new Item("Tomato");
            _hands.Put(tomato);

            var outcome = ItemTransfer.Interact(_hands, new ItemDispenser("Tomato"));

            Assert.That(outcome, Is.EqualTo(InteractionOutcome.Nothing));
            Assert.That(_hands.Current, Is.SameAs(tomato));
        }

        [Test]
        public void SlotRaisesChangedOnPutAndTake()
        {
            var changes = 0;
            var slot = new ItemSlot();
            slot.Changed += () => changes++;

            slot.Put(new Item("Tomato"));
            slot.Take();

            Assert.That(changes, Is.EqualTo(2));
        }
    }
}
