using System.Collections;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Simulation.Days;
using Game.Simulation.Furniture;
using Game.Simulation.Items;
using Game.Simulation.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Vector2 = System.Numerics.Vector2;

namespace Game.Tests.PlayMode
{
    /// <summary>The real scene, running: furniture bought into it and sold out of it.</summary>
    public class BuyingPlayTests
    {
        private const string SceneName = "Kitchen_Greybox";

        // Open floor, clear of the starting layout and the chef's spawn.
        private static readonly Vector2 OpenFloor = new Vector2(0f, -0.5f);

        private KitchenCompositionRoot _root;
        private LayoutEditor _editor;
        private LayoutEditor.Cursor _cursor;

        private DayCycle Days => _root.Services.Days;

        private IEnumerator OpenKitchenWithCursor()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _editor = Object.FindFirstObjectByType<LayoutEditor>();

            var keyboard = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.GetDevice<Mouse>() ?? InputSystem.AddDevice<Mouse>();
            var player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Keyboard&Mouse", pairWithDevices: new InputDevice[] { keyboard, mouse });
            yield return null;
            yield return null;
            _cursor = _editor.Enter(player);
            _cursor.Position = OpenFloor;
        }

        private T Buy<T>(string id, Vector2 where) where T : Interactable
        {
            _cursor.Position = where;
            Assert.That(_editor.Buy(_cursor, _editor.Shop.First(e => e.Id == id)), Is.True, "bought");
            _editor.MoveCursor(_cursor, Vector2.Zero);
            var station = (T)_cursor.Held;
            Assert.That(_editor.Place(_cursor), Is.True, _cursor.Result.ToString());
            return station;
        }

        [UnityTest]
        public IEnumerator YouPayWhenItGoesDownAndItsThenFurniture()
        {
            yield return OpenKitchenWithCursor();
            var before = Days.Balance;
            var stations = _root.Services.Stations.All.Count;

            Assert.That(_editor.Buy(_cursor, _editor.Shop.First(e => e.Id == "counter")), Is.True);
            _editor.MoveCursor(_cursor, Vector2.Zero);
            Assert.That(Days.Balance, Is.EqualTo(before), "not yet");
            var counter = _cursor.Held;
            Assert.That(_editor.Place(_cursor), Is.True, _cursor.Result.ToString());

            Assert.That(Days.Balance, Is.LessThan(before));
            Assert.That(_root.Services.Stations.All.Count, Is.EqualTo(stations + 1));
            Assert.That(_editor.StationUnder(_cursor), Is.SameAs(counter));
            Assert.That(counter.Container, Is.Not.Null, "it works");
        }

        [UnityTest]
        public IEnumerator BackingOutCostsNothingAndLeavesNothing()
        {
            yield return OpenKitchenWithCursor();
            var before = Days.Balance;
            var stations = _root.Services.Stations.All.Count;

            _editor.Buy(_cursor, _editor.Shop.First(e => e.Id == "counter"));
            var counter = _cursor.Held;
            _editor.Cancel(_cursor);
            yield return null;

            Assert.That(counter == null, "gone");
            Assert.That(Days.Balance, Is.EqualTo(before));
            Assert.That(_root.Services.Stations.All.Count, Is.EqualTo(stations));
        }

        [UnityTest]
        public IEnumerator WithoutTheMoneyItCantBeBoughtOrPlaced()
        {
            yield return OpenKitchenWithCursor();
            var expensive = _editor.Shop.OrderByDescending(e => e.Price).First();
            var cheap = _editor.Shop.OrderBy(e => e.Price).First();
            Days.TrySpend(Days.Balance);

            Assert.That(_editor.Buy(_cursor, expensive), Is.False);
            Assert.That(_cursor.Held, Is.Null);

            // Two players carrying at once: the money can run out before it goes down.
            Days.Receive(cheap.Price);
            _editor.Buy(_cursor, cheap);
            Days.TrySpend(1f);
            _editor.MoveCursor(_cursor, Vector2.Zero);
            Assert.That(_editor.Place(_cursor), Is.False);
            Assert.That(_cursor.Result, Is.EqualTo(Placement.CantAfford));
            _editor.Cancel(_cursor);
        }

        [UnityTest]
        public IEnumerator ABoughtHobCooksAndSellingItTakesItsPanToo()
        {
            yield return OpenKitchenWithCursor();
            Days.Receive(500f);
            var cooking = _root.Services.Cooking;
            var hobs = cooking.Hobs.Count;
            var pans = cooking.Pans.Count;

            var hob = Buy<HobStation>("hob", OpenFloor);
            yield return null;
            Assert.That(cooking.Hobs.Count, Is.EqualTo(hobs + 1));
            Assert.That(cooking.Pans.Count, Is.EqualTo(pans + 1));
            Assert.That(hob.Hob.Pan, Is.Not.Null);
            Assert.That(_root.Services.ItemViews.TryGetView(hob.Hob.Pan, out _), Is.True, "its pan shows");

            var pan = hob.Hob.Pan;
            var before = Days.Balance;
            Assert.That(_editor.Sell(_cursor), Is.True);
            yield return null;

            Assert.That(hob == null, "gone");
            Assert.That(Days.Balance, Is.GreaterThan(before));
            Assert.That(cooking.Hobs.Count, Is.EqualTo(hobs));
            Assert.That(cooking.Pans.Count, Is.EqualTo(pans));
            Assert.That(_root.Services.ItemViews.TryGetView(pan, out _), Is.False, "its pan went with it");
            Assert.That(_editor.StationUnder(_cursor), Is.Null);
        }

        [UnityTest]
        public IEnumerator TheLastHobCantBeSold()
        {
            yield return OpenKitchenWithCursor();
            Days.Receive(500f);
            var hob = Buy<HobStation>("hob", OpenFloor);
            yield return null;

            // Sell every other hob, then this one is the last.
            foreach (var other in _root.Services.Stations.All.OfType<HobStation>().Where(h => h != hob).ToList())
            {
                _cursor.Position = new Vector2(other.transform.position.x, other.transform.position.z);
                Assert.That(_editor.Sell(_cursor), Is.True);
            }

            _cursor.Position = OpenFloor;
            Assert.That(_editor.SaleProblemOf(_cursor, hob), Is.EqualTo(SaleProblem.LastOne));
            Assert.That(_editor.Sell(_cursor), Is.False);
        }

        [UnityTest]
        public IEnumerator ACounterWithSomethingOnItCantBeSold()
        {
            yield return OpenKitchenWithCursor();
            var counter = _root.Services.Stations.All.OfType<CounterStation>().First();
            counter.Slot.Put(new Item("Spare"));

            _cursor.Position = new Vector2(counter.transform.position.x, counter.transform.position.z);
            Assert.That(_editor.SaleProblemOf(_cursor, counter), Is.EqualTo(SaleProblem.NotEmpty));
            Assert.That(_editor.Sell(_cursor), Is.False);
        }

        [UnityTest]
        public IEnumerator ThePassIsNotForSale()
        {
            yield return OpenKitchenWithCursor();
            var pass = _root.Services.Stations.All.OfType<PassStation>().First();
            Assert.That(_editor.SaleProblemOf(_cursor, pass), Is.EqualTo(SaleProblem.NotForSale));
        }

        [UnityTest]
        public IEnumerator SellingTheOldFridgeSendsDeliveriesToTheNewOne()
        {
            yield return OpenKitchenWithCursor();
            Days.Receive(500f);
            var old = _root.Services.Stations.All.OfType<StorageStation>().First(s => s.IsRefrigerated);
            var bought = Buy<StorageStation>("fridge", OpenFloor);

            _cursor.Position = new Vector2(old.transform.position.x, old.transform.position.z);
            Assert.That(_editor.Sell(_cursor), Is.True);

            Days.Advance(); // to Prep: the morning delivery
            yield return null;
            Assert.That(bought.Unit.Slots.Sum(s => s.Count), Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator TheStreetDoorMovesAlongTheOutsideWallsAndCustomersUseIt()
        {
            yield return OpenKitchenWithCursor();
            var door = Object.FindFirstObjectByType<StreetDoor>();
            Assert.That(_root.Services.Stations.All.Contains(door), Is.False, "nobody works at it");
            Assert.That(_editor.SaleProblemOf(_cursor, door), Is.EqualTo(SaleProblem.NotForSale));

            var at = door.transform.position;
            _cursor.Position = new Vector2(at.x, at.z);
            Assert.That(_editor.PickUp(_cursor), Is.True);

            _editor.MoveCursor(_cursor, new Vector2(-at.x, -5f - at.z)); // the middle of the dining room
            Assert.That(_editor.Place(_cursor), Is.False);
            Assert.That(_cursor.Result, Is.EqualTo(Placement.NotOnAWall));

            _editor.MoveCursor(_cursor, new Vector2(-4f, 0f)); // near the west wall
            Assert.That(_editor.Place(_cursor), Is.True, _cursor.Result.ToString());
            yield return null;
            Assert.That(door.transform.position.x, Is.EqualTo(-4.55f).Within(0.01f));
            Assert.That(Mathf.DeltaAngle(door.transform.eulerAngles.y, 90f), Is.EqualTo(0f).Within(0.01f), "faces into the room");
        }

        [UnityTest]
        public IEnumerator FurnitureCantLandInTheDoorway()
        {
            yield return OpenKitchenWithCursor();
            Days.Receive(500f);
            var door = Object.FindFirstObjectByType<StreetDoor>();

            _cursor.Position = new Vector2(door.transform.position.x, door.transform.position.z + 0.6f);
            Assert.That(_editor.Buy(_cursor, _editor.Shop.First(e => e.Id == "table")), Is.True);
            _editor.MoveCursor(_cursor, Vector2.Zero);
            Assert.That(_editor.Place(_cursor), Is.False);
            Assert.That(_cursor.Result, Is.EqualTo(Placement.OnAnotherStation));
            _editor.Cancel(_cursor);
        }
    }
}
