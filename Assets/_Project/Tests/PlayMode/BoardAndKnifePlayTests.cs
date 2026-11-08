using System.Collections;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Stations;
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
    /// <summary>
    /// Boards and knives on counters (decision 2026-11-07): the board and the knife rack sit on
    /// counters and move only in management mode; the kitchen starts with one knife lying loose.
    /// </summary>
    public class BoardAndKnifePlayTests
    {
        private const string SceneName = "Kitchen_Greybox";

        private KitchenCompositionRoot _root;
        private LayoutEditor _editor;
        private PlayerInput _player;
        private PlayerInteractor _chef;

        private IEnumerator OpenKitchen()
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
            _chef = _player.GetComponent<PlayerInteractor>();
        }

        private CounterStation[] Counters => _root.Services.Stations.All.OfType<CounterStation>().ToArray();

        private PrepStation Board => _root.Services.Stations.All.OfType<PrepStation>().Single();

        private CounterStation KnifeCounter => Counters.Single(c => c.Slot.Current is Knife);

        private CounterStation FreeCounter(params CounterStation[] not) =>
            Counters.First(c => c.IsClear && !not.Contains(c));

        private static Vector2 Floor(Component c) => new Vector2(c.transform.position.x, c.transform.position.z);

        [UnityTest]
        public IEnumerator TheKitchenStartsWithOneKnifeOnACounterAndNoRack()
        {
            yield return OpenKitchen();

            Assert.That(Counters.Count(c => c.Slot.Current is Knife), Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<KnifeRackStation>(FindObjectsSortMode.None), Is.Empty);
        }

        [UnityTest]
        public IEnumerator TakingTheKnifeFreesItsCounterAndPuttingItDownNeedsEmptyHands()
        {
            yield return OpenKitchen();
            var counter = KnifeCounter;

            Assert.That(counter.Container.CanTake, Is.False, "a knife isn't picked up into the hands");
            Assert.That(counter.TryExchangeTool(_chef), Is.True);
            Assert.That(_chef.Tool, Is.TypeOf<Knife>());
            Assert.That(counter.IsClear, Is.True, "free again");

            _chef.Hands.Put(new Plate(3));
            Assert.That(counter.TryExchangeTool(_chef), Is.False, "hands full: the plate would go down, not the knife");
            _chef.Hands.Take();

            Assert.That(counter.TryExchangeTool(_chef), Is.True);
            Assert.That(counter.Slot.Current, Is.TypeOf<Knife>());
            Assert.That(_chef.Tool, Is.Null);
        }

        [UnityTest]
        public IEnumerator TheBoardTakesOverItsCounter()
        {
            yield return OpenKitchen();
            var under = Board.OnCounter;

            Assert.That(under, Is.Not.Null);
            Assert.That(under.IsCovered, Is.True);
            Assert.That(under.IsClear, Is.False, "a counter with a board on it can't be sold");

            var cursor = _editor.Enter(_player);
            cursor.Position = Floor(under);
            Assert.That(_editor.StationUnder(cursor), Is.SameAs(Board), "the board, not the counter");
        }

        [UnityTest]
        public IEnumerator TheBoardMovesToAFreeCounterAndLeavesItsOldOneFree()
        {
            yield return OpenKitchen();
            var board = Board;
            var from = board.OnCounter;
            var to = FreeCounter(from);
            var cursor = _editor.Enter(_player);

            cursor.Position = Floor(from);
            Assert.That(_editor.PickUp(cursor), Is.True);
            cursor.Position = Floor(to);
            _editor.MoveCursor(cursor, Vector2.Zero);
            Assert.That(_editor.Place(cursor), Is.True, cursor.Result.ToString());
            yield return null;

            Assert.That(board.OnCounter, Is.SameAs(to));
            Assert.That(to.IsCovered, Is.True);
            Assert.That(from.IsCovered, Is.False);
            Assert.That(from.Container.CanAccept(new Plate(3)), Is.True, "a plain counter again");
            Assert.That((board.transform.position - to.transform.position).sqrMagnitude, Is.LessThan(1e-4f), "it sits on its new counter");
        }

        [UnityTest]
        public IEnumerator TheBoardCantGoOnTheFloorOrOnACounterInUse()
        {
            yield return OpenKitchen();
            var from = Board.OnCounter;
            var cursor = _editor.Enter(_player);
            cursor.Position = Floor(from);
            _editor.PickUp(cursor);

            cursor.Position = new Vector2(2.4f, 0.95f); // open floor
            _editor.MoveCursor(cursor, Vector2.Zero);
            Assert.That(cursor.Result, Is.EqualTo(Placement.NotOnAFreeCounter), "the floor");

            cursor.Position = Floor(KnifeCounter);
            _editor.MoveCursor(cursor, Vector2.Zero);
            Assert.That(cursor.Result, Is.EqualTo(Placement.NotOnAFreeCounter), "the knife's there");
            Assert.That(_editor.Place(cursor), Is.False);

            _editor.Cancel(cursor);
            yield return null;
            Assert.That(Board.OnCounter, Is.SameAs(from), "back where it was");
        }

        [UnityTest]
        public IEnumerator ACounterMovesOnlyOnceItsBoardIsOff()
        {
            yield return OpenKitchen();
            var board = Board;
            var under = board.OnCounter;
            var cursor = _editor.Enter(_player);

            cursor.Position = Floor(under);
            _editor.PickUp(cursor);
            Assert.That(cursor.Held, Is.SameAs(board), "the board comes first");
            cursor.Position = Floor(FreeCounter(under));
            _editor.MoveCursor(cursor, Vector2.Zero);
            _editor.Place(cursor);

            cursor.Position = Floor(under);
            _editor.PickUp(cursor);
            Assert.That(cursor.Held, Is.SameAs(under), "now the counter");
            _editor.Cancel(cursor);
        }

        [UnityTest]
        public IEnumerator ChefsCantReachTheCounterUnderTheBoard()
        {
            yield return OpenKitchen();
            var under = Board.OnCounter;

            // Stand in front of the board, facing it.
            var controller = _player.GetComponent<CharacterController>();
            controller.enabled = false;
            _player.transform.SetPositionAndRotation(under.transform.position + new Vector3(0f, 0f, -0.9f), Quaternion.identity);
            controller.enabled = true;
            yield return null;
            yield return null;

            Assert.That(_chef.Target, Is.SameAs(Board));
        }

        // A bought rack goes on the free counter nearest a board (decision 2026-11-08).

        private KnifeRackStation BuyRack(LayoutEditor.Cursor cursor)
        {
            Assert.That(_editor.Buy(cursor, _editor.Shop.First(e => e.Id == "knife rack")), Is.True);
            Assert.That(cursor.Held, Is.Null, "nothing to carry: it's already down");
            return Object.FindObjectsByType<KnifeRackStation>(FindObjectsSortMode.None).Single();
        }

        [UnityTest]
        public IEnumerator ABoughtRackTakesTheFreeCounterNearestTheBoard()
        {
            yield return OpenKitchen();
            _root.Services.Days.Receive(100f);
            var board = Floor(Board);
            var nearest = Counters.Where(c => c.IsClear)
                .OrderBy(c => Vector2.DistanceSquared(Floor(c), board))
                .First();
            var balance = _root.Services.Days.Balance;
            var cursor = _editor.Enter(_player);

            var rack = BuyRack(cursor);

            Assert.That(rack.OnCounter, Is.SameAs(nearest));
            Assert.That(nearest.IsCovered, Is.True, "it takes the whole counter, as a board does");
            Assert.That(_root.Services.Days.Balance, Is.LessThan(balance), "paid for");
            Assert.That(_editor.StationUnder(cursor), Is.SameAs(rack), "the cursor shows where it went");
        }

        [UnityTest]
        public IEnumerator ABoughtKnifeHangsOnARackWithRoom()
        {
            yield return OpenKitchen();
            _root.Services.Days.Receive(100f);
            var rack = BuyRack(_editor.Enter(_player));
            _editor.Leave(_player);

            Assert.That(_editor.BuyItem(_editor.Shop.First(e => e.Id == "knife")), Is.EqualTo(Placement.Ok));
            Assert.That(rack.KnifeCount, Is.EqualTo(1));
        }
    }
}
