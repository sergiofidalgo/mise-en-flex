using System.Collections;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Items;
using Game.Gameplay.Stations;
using Game.Simulation.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// The chef's arms (decision 2026-11-08): a knife rides in the belt, a pan is held by its handle.
    /// Only where things end up; how the motion feels is checked by playing.
    /// </summary>
    public class ChefArmsPlayTests
    {
        private KitchenCompositionRoot _root;
        private PlayerInput _player;
        private PlayerInteractor _chef;

        private IEnumerator OpenKitchen()
        {
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            var keyboard = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.GetDevice<Mouse>() ?? InputSystem.AddDevice<Mouse>();
            _player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Keyboard&Mouse", pairWithDevices: new InputDevice[] { keyboard, mouse });
            yield return null;
            yield return null;
            _chef = _player.GetComponent<PlayerInteractor>();
        }

        private static IEnumerator Wait(float seconds)
        {
            var end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }

        private static ItemView ViewOf(Item item) =>
            Object.FindObjectsByType<ItemView>(FindObjectsSortMode.None).First(v => v.Item == item);

        // Where a hand is: the arm hangs 0.38 m down from its shoulder (CHEF_ARM_LENGTH).
        private Vector3 Hand(string shoulder)
        {
            var t = _player.transform.Find("Visuals").GetComponentsInChildren<Transform>().First(c => c.name == shoulder);
            return t.position + t.rotation * new Vector3(0f, -0.38f, 0f);
        }

        [UnityTest]
        public IEnumerator APickedUpKnifeRidesInTheBeltAwayFromTheBoard()
        {
            yield return OpenKitchen();
            var counter = _root.Services.Stations.All.OfType<CounterStation>().Single(c => c.Slot.Current is Knife);
            var knife = counter.Slot.Current;
            Assert.That(counter.TryExchangeTool(_chef), Is.True);
            yield return Wait(1.5f);

            var anchor = _player.transform.Find("ToolAnchor");
            var view = ViewOf(knife);
            Assert.That(view.transform.parent, Is.SameAs(anchor));
            Assert.That(view.transform.position.y, Is.LessThan(anchor.position.y - 0.05f), "down at the belt, not up in the hand");
            var local = _player.transform.InverseTransformPoint(view.transform.position);
            Assert.That(local.z, Is.LessThan(0.15f), "at the hip, not held out in front");
        }

        private Renderer Sheath => _player.GetComponentsInChildren<Renderer>(true).First(r => r.name == "Sheath");

        [UnityTest]
        public IEnumerator TheSheathShowsOnlyWhileTheChefHasAKnife()
        {
            yield return OpenKitchen();
            yield return null;
            Assert.That(Sheath.enabled, Is.False, "no knife, no sheath");

            var counter = _root.Services.Stations.All.OfType<CounterStation>().Single(c => c.Slot.Current is Knife);
            Assert.That(counter.TryExchangeTool(_chef), Is.True);
            yield return null;
            Assert.That(Sheath.enabled, Is.True);

            Assert.That(counter.TryExchangeTool(_chef), Is.True, "put back down");
            yield return null;
            Assert.That(Sheath.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator AKnifeOnACounterLiesAcrossItBigger()
        {
            yield return OpenKitchen();
            var counter = _root.Services.Stations.All.OfType<CounterStation>().Single(c => c.Slot.Current is Knife);
            var view = ViewOf(counter.Slot.Current);
            yield return Wait(1.5f);

            Assert.That(view.transform.lossyScale.x, Is.GreaterThan(1.3f), "bigger than its real size");
            Assert.That(Quaternion.Angle(view.transform.localRotation, Quaternion.identity), Is.GreaterThan(20f), "diagonal, not pointing straight away");
        }

        [UnityTest]
        public IEnumerator FoodSitsInADifferentSpotAndTurnOnEachCounter()
        {
            // Decision 2026-11-08: food on a counter sits a little off centre and turned, differently per counter.
            yield return OpenKitchen();
            var counters = _root.Services.Stations.All.OfType<CounterStation>().Where(c => c.Slot.Current == null).Take(2).ToArray();
            Assert.That(counters.Length, Is.EqualTo(2), "two empty counters");
            var views = new ItemView[2];
            for (var i = 0; i < 2; i++)
            {
                var onion = _root.Services.Cooking.AddIngredient(_root.Services.Catalog.Find("Onion").ToProperties());
                counters[i].Slot.Put(onion);
                _root.Services.ItemViews.PlaceImmediately(onion, counters[i].AnchorFor(onion));
                views[i] = ViewOf(onion);
            }
            yield return Wait(1.5f);

            for (var i = 0; i < 2; i++)
                Assert.That(views[i].transform.localPosition.magnitude, Is.LessThan(0.1f), "still on its counter");
            var turns = Quaternion.Angle(views[0].transform.localRotation, views[1].transform.localRotation);
            var spots = Vector3.Distance(views[0].transform.localPosition, views[1].transform.localPosition);
            Assert.That(turns > 5f || spots > 0.01f, "the two sit differently (turn {0:0}°, spot {1:0.000} m apart)", turns, spots);
        }

        [UnityTest]
        public IEnumerator APlateOnACounterShowsBiggerStill()
        {
            // Decision 2026-11-08: counters show things at 1.4x, and plates and dishes 1.3x on top of that.
            yield return OpenKitchen();
            var counter = _root.Services.Stations.All.OfType<CounterStation>().First(c => c.Slot.Current == null);
            var plate = new Plate(3);
            counter.Slot.Put(plate);
            _root.Services.ItemViews.PlaceImmediately(plate, counter.AnchorFor(plate));
            yield return Wait(1.5f);

            Assert.That(ViewOf(plate).transform.lossyScale.x, Is.EqualTo(1.4f * 1.3f).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator APileOfDirtyPlatesLooksDroppedNotStacked()
        {
            // The user's note (2026-11-08): each plate in a pile sits its own way, all at the plate size.
            yield return OpenKitchen();
            var counter = _root.Services.Stations.All.OfType<CounterStation>().First(c => c.Slot.Current == null);
            var views = new ItemView[3];
            for (var i = 0; i < 3; i++)
            {
                var plate = new Plate(3);
                plate.MarkDirty();
                counter.Slot.Put(plate);
                _root.Services.ItemViews.PlaceImmediately(plate, counter.AnchorFor(plate));
                views[i] = ViewOf(plate);
            }
            yield return Wait(1.5f);

            for (var i = 0; i < 3; i++)
                Assert.That(views[i].transform.lossyScale.x, Is.EqualTo(1.4f * 1.3f).Within(0.05f), $"plate {i} at the plate size");
            Assert.That(Vector3.Angle(views[0].transform.up, Vector3.up), Is.LessThan(0.5f), "the bottom one lies flat");
            Assert.That(Vector3.Angle(views[1].transform.up, Vector3.up) + Vector3.Angle(views[2].transform.up, Vector3.up),
                Is.GreaterThan(1f), "the ones on top tip a little");
            Assert.That(Vector3.Distance(views[1].transform.localPosition, views[2].transform.localPosition), Is.GreaterThan(0.002f),
                "not stacked exactly on each other");
        }

        [UnityTest]
        public IEnumerator APanIsCarriedByItsHandleWithBothHands()
        {
            yield return OpenKitchen();
            var hob = _root.Services.Stations.All.OfType<HobStation>().First(h => h.Hob?.Pan != null);
            var pan = hob.Container.Take();
            _chef.ReceiveItem(pan, hob.transform.position);
            yield return Wait(1.5f);

            var view = ViewOf(pan);
            Assert.That(Quaternion.Angle(view.transform.localRotation, Quaternion.identity), Is.GreaterThan(5f), "turned, its handle back towards the chef");
            // Both hands are on the handle (local -z), within a hand's size.
            var handle = view.transform.TransformPoint(new Vector3(0f, 0.1f, -0.35f));
            Assert.That(Vector3.Distance(Hand("Shoulder_L"), handle), Is.LessThan(0.2f), "left hand on the handle");
            Assert.That(Vector3.Distance(Hand("Shoulder_R"), handle), Is.LessThan(0.2f), "right hand on the handle");
        }
    }
}
