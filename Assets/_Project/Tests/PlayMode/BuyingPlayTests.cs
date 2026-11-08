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
        // Between the island and the pass: the back-right gap is the tray rack's since 2026-11-07.
        private static readonly Vector2 OpenFloor = new Vector2(-0.4f, -2.0f);

        // The second chef's spawn, across the kitchen from the bought grill.
        private static readonly Vector3 OutOfTheGrillsWay = new Vector3(2.45f, 0f, -0.8f);

        private KitchenCompositionRoot _root;
        private LayoutEditor _editor;
        private LayoutEditor.Cursor _cursor;
        private PlayerInput _player;

        private DayCycle Days => _root.Services.Days;

        private IEnumerator OpenKitchenWithCursor()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _editor = Object.FindFirstObjectByType<LayoutEditor>();

            var keyboard = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.GetDevice<Mouse>() ?? InputSystem.AddDevice<Mouse>();
            _player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Keyboard&Mouse", pairWithDevices: new InputDevice[] { keyboard, mouse });
            yield return null;
            yield return null;
            _cursor = _editor.Enter(_player);
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
            // Furniture that stands on the floor: kitchenware isn't carried, and a board goes on a counter.
            var cheap = _editor.Shop.Where(e => !e.IsItem && e.Prefab.GetComponent<CounterTopStation>() == null).OrderBy(e => e.Price).First();
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
        public IEnumerator AnInductionHobStepsOnePullAtATime()
        {
            yield return OpenKitchenWithCursor();
            Days.Receive(500f); // it costs more than the first day's money
            var hob = Buy<HobStation>("induction hob", OpenFloor);

            Assert.That(hob.Hob.Kind, Is.EqualTo(Game.Simulation.Cooking.HeatKind.Induction));
            hob.AdjustHeat(0f, 1f, 0.02f);
            Assert.That(hob.Hob.Step, Is.EqualTo(1));
            hob.AdjustHeat(0f, 1f, 0.02f);
            Assert.That(hob.Hob.Step, Is.EqualTo(1), "held, not pressed again");
            hob.AdjustHeat(0f, 0f, 0.02f);
            hob.AdjustHeat(0f, 1f, 0.02f);
            Assert.That(hob.Hob.Step, Is.EqualTo(2));
            hob.AdjustHeat(0f, 0f, 0.02f);
            hob.AdjustHeat(1f, 0f, 0.02f);
            Assert.That(hob.Hob.Step, Is.EqualTo(1));
            Assert.That(hob.Hob.Pan, Is.Not.Null, "it comes with a pan");
        }

        [UnityTest]
        public IEnumerator TheGasHobCanGoOnceThereIsAnInductionHob()
        {
            yield return OpenKitchenWithCursor();
            var gas = _root.Services.Stations.All.OfType<HobStation>().Single();
            Assert.That(_editor.SaleProblemOf(_cursor, gas), Is.EqualTo(SaleProblem.LastOne));

            Days.Receive(500f);
            Buy<HobStation>("induction hob", OpenFloor);

            Assert.That(_editor.SaleProblemOf(_cursor, gas), Is.EqualTo(SaleProblem.None));
        }

        [UnityTest]
        public IEnumerator ABoughtGrillIsLitWithAHotAndACoolSide()
        {
            yield return OpenKitchenWithCursor();
            Days.Receive(500f);
            // The chef spawns 0.4 m from where the grill goes: closer than a body and its personal space.
            yield return this.StandAt(OutOfTheGrillsWay); // "this.": one test has its own StandAt
            var grill = Buy<GrillStation>("grill", new Vector2(-1.5f, -1.7f));
            yield return null;

            Assert.That(grill.Grill.Hot.Grate.Temperature, Is.GreaterThan(grill.Grill.Cool.Grate.Temperature + 50f));
            var glow = grill.GetComponentInChildren<Light>();
            Assert.That(glow, Is.Not.Null);
            Assert.That(glow.enabled && glow.intensity > 0f, Is.True, "always lit");
        }

        [UnityTest]
        public IEnumerator AChefWorksTheZoneTheyFace()
        {
            yield return OpenKitchenWithCursor();
            Days.Receive(500f);
            // The chef spawns 0.4 m from where the grill goes: closer than a body and its personal space.
            yield return this.StandAt(OutOfTheGrillsWay); // "this.": one test has its own StandAt
            var grill = Buy<GrillStation>("grill", new Vector2(-1.5f, -1.7f));
            _editor.Leave(_player);
            var chef = _player.GetComponent<PlayerInteractor>();
            var controller = _player.GetComponent<CharacterController>();
            var middle = grill.transform.position;

            void StandAt(float x, float z, float facing)
            {
                controller.enabled = false;
                _player.transform.SetPositionAndRotation(new Vector3(middle.x + x, _player.transform.position.y, middle.z + z), Quaternion.Euler(0f, facing, 0f));
                controller.enabled = true;
            }

            StandAt(-0.5f, -0.9f, 0f); // in front, facing the left half
            Assert.That(grill.ZoneFacing(chef), Is.SameAs(grill.Grill.Hot));
            StandAt(0.5f, -0.9f, 0f);
            Assert.That(grill.ZoneFacing(chef), Is.SameAs(grill.Grill.Cool));
            StandAt(-0.5f, 0.9f, 180f); // from the other long side
            Assert.That(grill.ZoneFacing(chef), Is.SameAs(grill.Grill.Hot));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ABoughtKnifeLiesOnAFreeCounterAtOnce()
        {
            yield return OpenKitchenWithCursor();
            var before = Days.Balance;
            int Knives() => _root.Services.Stations.All.OfType<CounterStation>().Count(c => c.Slot.Current is Knife);
            var knives = Knives();

            Assert.That(_editor.BuyItem(_editor.Shop.First(e => e.Id == "knife")), Is.EqualTo(Placement.Ok));

            Assert.That(Knives(), Is.EqualTo(knives + 1));
            Assert.That(Days.Balance, Is.LessThan(before), "paid for at once");
        }

        [UnityTest]
        public IEnumerator ABoughtPanLiesOnAFreeCounter()
        {
            yield return OpenKitchenWithCursor();
            var pans = _root.Services.Cooking.Pans.Count;

            Assert.That(_editor.BuyItem(_editor.Shop.First(e => e.Id == "pan")), Is.EqualTo(Placement.Ok));

            Assert.That(_root.Services.Cooking.Pans.Count, Is.EqualTo(pans + 1));
            Assert.That(_root.Services.Stations.All.OfType<CounterStation>().Any(c => c.Slot.Current is Game.Simulation.Cooking.Pan));
        }

        [UnityTest]
        public IEnumerator WithNoFreeCounterKitchenwareIsntBoughtOrPaidFor()
        {
            yield return OpenKitchenWithCursor();
            foreach (var counter in _root.Services.Stations.All.OfType<CounterStation>().Where(c => c.IsClear).ToArray())
                counter.Slot.Put(new Item("Spare"));
            var before = Days.Balance;

            Assert.That(_editor.BuyItem(_editor.Shop.First(e => e.Id == "knife")), Is.EqualTo(Placement.NoFreeCounter));
            Assert.That(Days.Balance, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator ABoughtBoardGoesOnAFreeCounterAndARackJoinsTheNearestBoard()
        {
            yield return OpenKitchenWithCursor();
            Days.Receive(100f);
            var free = _root.Services.Stations.All.OfType<CounterStation>().First(c => c.IsClear);

            var board = Buy<PrepStation>("board", new Vector2(free.transform.position.x, free.transform.position.z));
            Assert.That(board.OnCounter, Is.SameAs(free));
            Assert.That(_root.Services.Stations.All.OfType<PrepStation>().Count(), Is.EqualTo(2));

            // Bought with the cursor on the new board, the rack goes straight onto its counter.
            Assert.That(_editor.Buy(_cursor, _editor.Shop.First(e => e.Id == "knife rack")), Is.True);
            Assert.That(_cursor.Held, Is.Null);
            var rack = Object.FindObjectsByType<KnifeRackStation>(FindObjectsSortMode.None).Single();
            Assert.That(rack.OnCounter, Is.SameAs(free));

            // The kitchen's knife goes from its counter onto the rack.
            _editor.Leave(_player);
            var chef = _player.GetComponent<PlayerInteractor>();
            var knifeCounter = _root.Services.Stations.All.OfType<CounterStation>().Single(c => c.Slot.Current is Knife);
            Assert.That(knifeCounter.TryExchangeTool(chef), Is.True);
            Assert.That(rack.TryExchangeTool(chef), Is.True);
            Assert.That(rack.KnifeCount, Is.EqualTo(1));
            Assert.That(chef.Tool, Is.Null);
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
            var counter = _root.Services.Stations.All.OfType<CounterStation>().First(c => c.IsClear);
            counter.Slot.Put(new Item("Spare"));

            _cursor.Position = new Vector2(counter.transform.position.x, counter.transform.position.z);
            Assert.That(_editor.SaleProblemOf(_cursor, counter), Is.EqualTo(SaleProblem.NotEmpty));
            Assert.That(_editor.Sell(_cursor), Is.False);
        }

        [UnityTest]
        public IEnumerator TheSinkIsNotForSale()
        {
            yield return OpenKitchenWithCursor();
            var sink = _root.Services.Stations.All.OfType<SinkStation>().First();
            Assert.That(_editor.SaleProblemOf(_cursor, sink), Is.EqualTo(SaleProblem.NotForSale));
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
            Assert.That(door.transform.position.x, Is.EqualTo(-3.55f).Within(0.01f));
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

        [UnityTest]
        public IEnumerator FurnitureAgainstTheWallCanBeReachedFromTheOtherRoom()
        {
            yield return OpenKitchenWithCursor();
            Days.Receive(500f);
            var against = Buy<CounterStation>("counter", new Vector2(3.4f, -2.4f));
            var away = Buy<CounterStation>("counter", new Vector2(2.4f, -2.1f));
            _editor.Leave(_player);

            // A chef faces north unless they've turned, so they look from the dining room.
            var interactor = _player.GetComponent<PlayerInteractor>();
            yield return StandAt(new Vector3(3.4f, 0f, -3.45f));
            Assert.That(interactor.Target, Is.SameAs(against), "over the wall");

            yield return StandAt(new Vector3(2.4f, 0f, -3.45f));
            Assert.That(interactor.Target, Is.Not.SameAs(away), "in reach, but through the wall");
        }

        private IEnumerator StandAt(Vector3 position)
        {
            var controller = _player.GetComponent<CharacterController>();
            controller.enabled = false;
            _player.transform.position = new Vector3(position.x, _player.transform.position.y, position.z);
            controller.enabled = true;
            yield return null;
            yield return null;
        }
    }
}
