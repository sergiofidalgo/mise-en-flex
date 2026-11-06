using System.Collections;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Customers;
using Game.Gameplay.Stations;
using Game.Simulation.Cooking;
using Game.Simulation.Days;
using Game.Simulation.Items;
using Game.Simulation.Movement;
using Game.Simulation.Service;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>The real scene, running: customers' bodies in the dining room.</summary>
    public class CustomerPlayTests
    {
        private const string SceneName = "Kitchen_Greybox";

        private KitchenCompositionRoot _root;
        private CustomerDirector _customers;
        private PlayerInput _player;
        private TableStation _table;

        private IEnumerator OpenForService()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _customers = Object.FindFirstObjectByType<CustomerDirector>();

            var keyboard = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.GetDevice<Mouse>() ?? InputSystem.AddDevice<Mouse>();
            _player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Keyboard&Mouse", pairWithDevices: new InputDevice[] { keyboard, mouse });
            yield return null;
            yield return null;

            var days = _root.Services.Days;
            days.Advance(); // to Prep
            days.Advance(); // to Service
            yield return null;
        }

        // Arrivals come on the game clock; bring the next one on now.
        private Customer NextCustomer()
        {
            var service = _root.Services.Service;
            var count = service.Customers.Count;
            while (service.Customers.Count == count)
                service.Tick(1f);
            return service.Customers.Last();
        }

        private IEnumerator WaitFor(System.Func<bool> condition, float seconds)
        {
            var until = Time.time + seconds;
            while (!condition() && Time.time < until)
                yield return null;
            Assert.That(condition(), Is.True, "in time");
        }

        [UnityTest]
        public IEnumerator ACustomerWalksInFromTheStreetAndSits()
        {
            yield return OpenForService();
            var customer = NextCustomer();
            yield return null;

            var agent = _customers.Agents[customer];
            Assert.That(agent.transform.position.z, Is.LessThan(-6f), "at the street door");

            yield return WaitFor(() => customer.State == CustomerState.Waiting, 15f);
            Assert.That(agent.transform.position.z, Is.GreaterThan(-4.2f), "at a bar table, by the wall");
        }

        [UnityTest]
        public IEnumerator AChefCanPushACustomerAndTheyComeBack()
        {
            yield return OpenForService();
            var customer = NextCustomer();
            yield return WaitFor(() => customer.State == CustomerState.Waiting, 15f);
            var agent = _customers.Agents[customer];
            var seat = agent.transform.position;

            // Standing right up against them, pressing south.
            _player.GetComponent<CharacterController>().enabled = false;
            _player.transform.position = seat + new Vector3(0f, 0f, 0.2f);
            _player.GetComponent<CharacterController>().enabled = true;
            yield return null;
            yield return null;

            var apart = new Vector2(_player.transform.position.x - agent.transform.position.x, _player.transform.position.z - agent.transform.position.z).magnitude;
            Assert.That(apart, Is.GreaterThan(0.5f), "bodies don't overlap");

            _player.GetComponent<CharacterController>().enabled = false;
            _player.transform.position = new Vector3(-3f, _player.transform.position.y, 0f);
            _player.GetComponent<CharacterController>().enabled = true;
            yield return WaitFor(() => (agent.transform.position - seat).magnitude < 0.1f, 5f);
        }

        [UnityTest]
        public IEnumerator EveryoneGetsOutAgain()
        {
            yield return OpenForService();
            var customer = NextCustomer();
            yield return WaitFor(() => customer.State == CustomerState.Waiting, 15f);
            var service = _root.Services.Service;

            // Run their patience out: they walk back out through the door, and are gone.
            while (customer.State == CustomerState.Waiting)
                service.Tick(1f);
            yield return WaitFor(() => customer.State == CustomerState.Gone, 15f);
        }

        // Buys a table during Plan and opens up. The bar tables fill first.
        private IEnumerator OpenWithATable()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _customers = Object.FindFirstObjectByType<CustomerDirector>();
            var keyboard = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.GetDevice<Mouse>() ?? InputSystem.AddDevice<Mouse>();
            _player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Keyboard&Mouse", pairWithDevices: new InputDevice[] { keyboard, mouse });
            yield return null;
            yield return null;

            var editor = Object.FindFirstObjectByType<LayoutEditor>();
            var cursor = editor.Enter(_player);
            _root.Services.Days.Receive(100f);

            cursor.Position = new System.Numerics.Vector2(0f, 0f);
            Assert.That(editor.Buy(cursor, editor.Shop.First(e => e.Id == "table")), Is.True);
            _table = (TableStation)cursor.Held;
            editor.MoveCursor(cursor, System.Numerics.Vector2.Zero);
            Assert.That(editor.Place(cursor), Is.False, "not in the kitchen");
            Assert.That(cursor.Result, Is.EqualTo(Placement.OutsideTheKitchen));

            editor.MoveCursor(cursor, new System.Numerics.Vector2(-2f, -5.5f));
            Assert.That(editor.Place(cursor), Is.True, cursor.Result.ToString());
            editor.Leave(_player);

            ControlTheService();
            var days = _root.Services.Days;
            days.Advance();
            days.Advance();
            yield return null;
        }

        // A service whose timing the test controls: nobody arrives until it's asked for, and nobody
        // loses patience, whatever the settings being played with are.
        private void ControlTheService()
        {
            var settings = _root.Services.ServiceConfig.ToSettings(1);
            settings.DurationSeconds = 100000f;
            settings.LastOrdersSeconds = 0f;
            settings.ArrivalIntervalSeconds = 2000f;
            settings.ArrivalJitter = 0f;
            settings.RushMultiplier = 1f;
            settings.PatienceSeconds = 100000f;
            settings.EatSeconds = 10f;
            _root.Services.ReplaceService(new ServiceSimulation(settings, _root.Services.Service.SeatCount));
        }

        private static Plate Meal(KitchenCompositionRoot root)
        {
            var plate = new Plate(3);
            plate.Put(root.Services.CreateItem("Steak"));
            return plate;
        }

        [UnityTest]
        public IEnumerator ATableAddsASeatAndServesLikeThePass()
        {
            yield return OpenWithATable();
            var service = _root.Services.Service;
            Assert.That(service.SeatCount, Is.EqualTo(4), "the three bar tables and this one");

            Customer customer = null;
            for (var i = 0; i < 4; i++)
                customer = NextCustomer();
            Assert.That(_table.SeatIndexes.Contains(customer.Seat), Is.True, "once the bar tables are full");

            yield return WaitFor(() => customer.State == CustomerState.Waiting, 20f);
            var agent = _customers.Agents[customer];
            Assert.That((agent.transform.position - _table.SeatPosition(customer.Seat)).magnitude, Is.LessThan(0.2f), "in its chair");

            _table.Container.Put(Meal(_root));
            Assert.That(customer.State, Is.EqualTo(CustomerState.Eating));
        }

        [UnityTest]
        public IEnumerator ACustomerSitsAndOrdersAtADirtyTableButWaitsForItToBeCleared()
        {
            yield return OpenWithATable();
            var service = _root.Services.Service;
            Customer customer = null;
            for (var i = 0; i < 4; i++)
                customer = NextCustomer();
            yield return WaitFor(() => customer.State == CustomerState.Waiting, 20f);

            // Eaten: the dirty plate stays.
            _table.Container.Put(Meal(_root));
            yield return WaitFor(() => _table.HasDirtyPlate, 5f);

            var next = NextCustomer();
            Assert.That(_table.SeatIndexes.Contains(next.Seat), Is.True);
            yield return WaitFor(() => next.State == CustomerState.Waiting, 20f);
            Assert.That(next.Order, Is.Not.Null, "ordering");

            var meal = Meal(_root);
            Assert.That(_table.Container.CanAccept(meal), Is.False, "clear it first");
            var dirty = _table.Container.Take();
            Assert.That(dirty, Is.Not.Null);
            Assert.That(_table.Container.CanAccept(meal), Is.True);
            _table.Container.Put(meal);
            Assert.That(next.State, Is.EqualTo(CustomerState.Eating));
        }

        [UnityTest]
        public IEnumerator WithTheStreetDoorBoxedInTheyGiveUpAndTheServiceCarriesOn()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _customers = Object.FindFirstObjectByType<CustomerDirector>();
            var keyboard = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.GetDevice<Mouse>() ?? InputSystem.AddDevice<Mouse>();
            _player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Keyboard&Mouse", pairWithDevices: new InputDevice[] { keyboard, mouse });
            yield return null;
            yield return null;

            var editor = Object.FindFirstObjectByType<LayoutEditor>();
            var cursor = editor.Enter(_player);
            _root.Services.Days.Receive(500f);
            foreach (var spot in new[] { new System.Numerics.Vector2(1f, -6.4f), new System.Numerics.Vector2(3f, -6.4f), new System.Numerics.Vector2(2f, -5.4f) })
            {
                cursor.Position = spot;
                Assert.That(editor.Buy(cursor, editor.Shop.First(e => e.Id == "table")), Is.True);
                editor.MoveCursor(cursor, System.Numerics.Vector2.Zero);
                Assert.That(editor.Place(cursor), Is.True, cursor.Result.ToString());
            }
            editor.Leave(_player);

            ControlTheService();
            var days = _root.Services.Days;
            days.Advance();
            days.Advance();
            yield return null;

            var customer = NextCustomer();
            yield return WaitFor(() => customer.State == CustomerState.Gone, 15f);
            Assert.That(customer.WalkedOut, Is.True);
            Assert.That(_root.Services.Service.LostCovers, Is.EqualTo(1));
        }

        // A seed whose first arrival is a group of at least two, at these tables.
        private int SeedForAGroup(params int[] tables)
        {
            var settings = _root.Services.ServiceConfig.ToSettings(1);
            settings.ArrivalIntervalSeconds = 2000f;
            settings.ArrivalJitter = 0f;
            settings.DurationSeconds = 100000f;
            settings.LastOrdersSeconds = 0f;
            settings.RushMultiplier = 1f;
            for (var seed = 1; ; seed++)
            {
                settings.Seed = seed;
                var probe = new ServiceSimulation(settings, 1);
                probe.OpenSeats(tables);
                while (probe.Customers.Count == 0)
                    probe.Tick(10f);
                if (probe.Customers[0].Party.Count >= 2)
                    return seed;
            }
        }

        [UnityTest]
        public IEnumerator AGroupSitsAtATableForFourEatsAndLeavesItsPlates()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _customers = Object.FindFirstObjectByType<CustomerDirector>();
            var keyboard = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.GetDevice<Mouse>() ?? InputSystem.AddDevice<Mouse>();
            _player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Keyboard&Mouse", pairWithDevices: new InputDevice[] { keyboard, mouse });
            yield return null;
            yield return null;

            var editor = Object.FindFirstObjectByType<LayoutEditor>();
            var cursor = editor.Enter(_player);
            _root.Services.Days.Receive(500f);
            var money = _root.Services.Days.Balance;

            cursor.Position = new System.Numerics.Vector2(-1f, -5.5f);
            Assert.That(editor.Buy(cursor, editor.Shop.First(e => e.Id == "table")), Is.True);
            _table = (TableStation)cursor.Held;
            editor.MoveCursor(cursor, System.Numerics.Vector2.Zero);
            Assert.That(editor.Place(cursor), Is.True, cursor.Result.ToString());
            Assert.That(_table.ChairCount, Is.EqualTo(1), "it comes with one");

            for (var i = 0; i < 3; i++)
                Assert.That(editor.AddChair(cursor), Is.True);
            Assert.That(editor.AddChair(cursor), Is.False, "one per side");
            Assert.That(_table.ChairCount, Is.EqualTo(4));
            Assert.That(money - _root.Services.Days.Balance, Is.EqualTo(50f + 3f * editor.ChairPrice).Within(0.01f));
            editor.Leave(_player);

            var settings = _root.Services.ServiceConfig.ToSettings(1);
            settings.ArrivalIntervalSeconds = 2000f;
            settings.ArrivalJitter = 0f;
            settings.DurationSeconds = 100000f;
            settings.LastOrdersSeconds = 0f;
            settings.RushMultiplier = 1f;
            settings.PatienceSeconds = 100000f;
            settings.EatSeconds = 10f;
            settings.Seed = SeedForAGroup(1, 1, 1, 4);
            _root.Services.ReplaceService(new ServiceSimulation(settings, 3));
            var days = _root.Services.Days;
            days.Advance();
            days.Advance();
            yield return null;
            Assert.That(_root.Services.Service.SeatCount, Is.EqualTo(7));

            var party = NextCustomer().Party;
            Assert.That(party.Count, Is.GreaterThanOrEqualTo(2));
            var until = Time.time + 20f;
            while (!party.All(c => c.State == CustomerState.Waiting) && Time.time < until)
                yield return null;
            Assert.That(party.All(c => c.State == CustomerState.Waiting), Is.True,
                string.Join("; ", party.Select(c => $"{c.State} seat {c.Seat} at {_customers.Agents[c].transform.position} for {_table.SeatPosition(c.Seat)}")));
            Assert.That(party.All(c => _table.SeatIndexes.Contains(c.Seat)), Is.True, "at the table for four");
            Assert.That(party.Select(c => _customers.Agents[c].transform.position).Distinct().Count(), Is.EqualTo(party.Count), "a chair each");

            foreach (var member in party)
                _table.Container.Put(Meal(_root));
            Assert.That(party.All(c => c.State == CustomerState.Eating), Is.True, "a plate each");

            yield return WaitFor(() => party.All(c => c.State == CustomerState.Leaving || c.State == CustomerState.Gone), 5f);
            yield return null;
            for (var i = 0; i < party.Count; i++)
                Assert.That(((Plate)_table.Container.Take()).IsDirty, Is.True, "a dirty plate each");
            Assert.That(_table.IsClear, Is.True);
        }

        [UnityTest]
        public IEnumerator ABarTableAgainstTheWallHasItsChairOnTheFreeSide()
        {
            yield return OpenForBuying();
            var editor = Object.FindFirstObjectByType<LayoutEditor>();
            var cursor = editor.Enter(_player);
            _root.Services.Days.Receive(500f);

            cursor.Position = new System.Numerics.Vector2(2.4f, -3.35f);
            Assert.That(editor.Buy(cursor, editor.Shop.First(e => e.Id == "bar")), Is.True);
            var bar = (TableStation)cursor.Held;
            editor.MoveCursor(cursor, System.Numerics.Vector2.Zero);
            Assert.That(editor.Place(cursor), Is.True, cursor.Result.ToString());
            Assert.That(bar.HasChoiceOfChairs, Is.False);

            // Turned round, its chair would be in the wall.
            Assert.That(editor.PickUp(cursor), Is.True);
            editor.Rotate(cursor, 180f);
            Assert.That(editor.Place(cursor), Is.False);
            Assert.That(cursor.Result, Is.EqualTo(Placement.NoRoomForChairs));
            editor.Cancel(cursor);
        }

        [UnityTest]
        public IEnumerator ATableAgainstTheWallHasOneSideLess()
        {
            yield return OpenForBuying();
            var editor = Object.FindFirstObjectByType<LayoutEditor>();
            var cursor = editor.Enter(_player);
            _root.Services.Days.Receive(500f);

            cursor.Position = new System.Numerics.Vector2(2.4f, -3.6f);
            Assert.That(editor.Buy(cursor, editor.Shop.First(e => e.Id == "table")), Is.True);
            var table = (TableStation)cursor.Held;
            editor.MoveCursor(cursor, System.Numerics.Vector2.Zero);
            Assert.That(editor.Place(cursor), Is.True, cursor.Result.ToString());

            Assert.That(editor.AddChair(cursor), Is.True);
            Assert.That(editor.AddChair(cursor), Is.True);
            Assert.That(editor.AddChair(cursor), Is.False, "the wall side has no room");
            Assert.That(table.ChairCount, Is.EqualTo(3));
        }

        private IEnumerator OpenForBuying()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _customers = Object.FindFirstObjectByType<CustomerDirector>();
            var keyboard = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.GetDevice<Mouse>() ?? InputSystem.AddDevice<Mouse>();
            _player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Keyboard&Mouse", pairWithDevices: new InputDevice[] { keyboard, mouse });
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ADishwasherClearsABarTableFromTheKitchenSide()
        {
            yield return OpenForBuying();
            var services = _root.Services;
            var director = Object.FindFirstObjectByType<Game.Gameplay.Staff.StaffDirector>();
            services.Days.Receive(500f);
            var candidate = services.StaffConfig.CandidatesFor(1, 2).First(c => c.Role == Game.Simulation.Staff.StaffRole.Dishwasher);
            services.Staff.Hire(candidate, services.Days.Balance);
            ControlTheService();
            services.Days.Advance();
            yield return null;
            services.Days.Advance();
            yield return null;

            var customer = NextCustomer();
            yield return WaitFor(() => customer.State == CustomerState.Waiting, 20f);
            var bar = services.Stations.All.OfType<TableStation>().First(t => t.SeatIndexes.Contains(customer.Seat));
            bar.Container.Put(Meal(_root));
            yield return WaitFor(() => bar.HasDirtyPlate, 5f);

            var agent = director.Agents[0];
            yield return WaitFor(() => !bar.HasDirtyPlate, 30f);
            Assert.That(agent.transform.position.z, Is.GreaterThan(-2.9f), "from the kitchen");
        }

        // A steak on a plate, cooked in a real pan to what this customer ordered.
        private Plate CookedFor(Customer customer)
        {
            var services = _root.Services;
            var steak = (Ingredient)services.CreateItem("Steak");
            var hob = services.Stations.All.OfType<HobStation>().First();
            hob.Hob.Dial = 0.5f;
            var pan = hob.Hob.Pan;
            for (var i = 0; i < 300; i++)
                services.Cooking.Tick(1f);
            pan.Put(steak);
            var seconds = 0;
            while (steak.Doneness?.Name != customer.Order.Doneness.Name && seconds++ < 3600)
                services.Cooking.Tick(1f);
            pan.Take();
            hob.Hob.Dial = 0f;
            var plate = new Plate(3);
            plate.Put(steak);
            return plate;
        }

        private Game.Gameplay.Staff.StaffDirector HireForTheSecondRestaurant(Game.Simulation.Staff.StaffRole role)
        {
            var services = _root.Services;
            services.Days.Receive(500f);
            services.Staff.Hire(services.StaffConfig.CandidatesFor(1, 2).First(c => c.Role == role), services.Days.Balance);
            return Object.FindFirstObjectByType<Game.Gameplay.Staff.StaffDirector>();
        }

        [UnityTest]
        public IEnumerator AWaiterAnswersTheBellAndClearsUp()
        {
            yield return OpenForBuying();
            var services = _root.Services;
            var director = HireForTheSecondRestaurant(Game.Simulation.Staff.StaffRole.Waiter);
            ControlTheService();
            services.Days.Advance();
            yield return null;
            services.Days.Advance();
            yield return null;

            // The chef stays where they are, standing in a corridor: the waiter goes another way round.
            var customer = NextCustomer();
            yield return WaitFor(() => customer.State == CustomerState.Waiting, 20f);

            var counter = services.Stations.All.OfType<CounterStation>().First();
            counter.Slot.Put(CookedFor(customer));
            yield return null;
            Assert.That(customer.State, Is.EqualTo(CustomerState.Waiting), "not until the bell");

            Assert.That(services.Bell.Ring(counter.Slot), Is.True);
            var agent = director.Agents[0];
            var until = Time.time + 30f;
            while (customer.State == CustomerState.Waiting && Time.time < until)
                yield return null;
            var job = (Game.Simulation.Staff.Waiter)agent.Job;
            Assert.That(customer.State, Is.Not.EqualTo(CustomerState.Waiting), $"{job.Task} at {agent.transform.position}, carrying {job.Carried != null}, counter empty {counter.Slot.IsEmpty}, rung {services.Bell.IsRung(counter.Slot)}, order {customer.Order.Doneness.Name}");
            Assert.That(counter.Slot.IsEmpty, Is.True);

            var pile = services.Stations.All.OfType<DirtyPileStation>().First();
            until = Time.time + 40f;
            while (pile.Pile.Count != 1 && Time.time < until)
                yield return null;
            Assert.That(pile.Pile.Count, Is.EqualTo(1), $"{job.Task} at {agent.transform.position}, carrying {job.Carried != null}, customer {customer.State}");
            Assert.That(services.Staff.Hired[0].JobsToday, Is.EqualTo(2), "one served, one cleared");
        }

        [UnityTest]
        public IEnumerator WithNothingToDoAWaiterGoesToTheDiningRoom()
        {
            yield return OpenForBuying();
            var director = HireForTheSecondRestaurant(Game.Simulation.Staff.StaffRole.Waiter);
            _root.Services.Days.Advance();
            yield return null;
            var agent = director.Agents[0];
            Assert.That(agent.transform.position.z, Is.GreaterThan(-2.9f), "arrives in the kitchen");

            yield return WaitFor(() => agent.transform.position.z < -3.2f, 20f);
        }

        [UnityTest]
        public IEnumerator AHelperStaysInTheKitchen()
        {
            yield return OpenForBuying();
            var services = _root.Services;
            var director = HireForTheSecondRestaurant(Game.Simulation.Staff.StaffRole.PrepAssistant);
            services.Days.Advance();
            yield return null;
            var agent = director.Agents[0];
            for (var seconds = 0f; seconds < 5f; seconds += Time.deltaTime)
            {
                Assert.That(agent.transform.position.z, Is.GreaterThan(-2.9f));
                yield return null;
            }
        }

        [Test]
        public void TheFirstRestaurantOnlyHasHelpersForHire()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<Game.Data.StaffConfig>("Assets/_Project/Data/Settings/StaffConfig.asset");
            for (var day = 1; day <= 30; day++)
                Assert.That(config.CandidatesFor(day).All(c => c.Role == Game.Simulation.Staff.StaffRole.PrepAssistant), Is.True);
            var later = Enumerable.Range(1, 30).SelectMany(day => config.CandidatesFor(day, 2)).Select(c => c.Role).Distinct().ToArray();
            Assert.That(later, Does.Contain(Game.Simulation.Staff.StaffRole.Waiter));
            Assert.That(later, Does.Contain(Game.Simulation.Staff.StaffRole.Dishwasher));
        }

        [UnityTest]
        public IEnumerator WithNoWayRoundAWaiterWaitsForTheChefToMove()
        {
            yield return OpenForBuying();
            var services = _root.Services;
            var director = HireForTheSecondRestaurant(Game.Simulation.Staff.StaffRole.Waiter);
            ControlTheService();
            services.Days.Advance();
            yield return null;
            services.Days.Advance();
            yield return null;

            // The kitchen door is the only way between the rooms, and the chef's standing in it.
            var controller = _player.GetComponent<CharacterController>();
            var y = _player.transform.position.y;
            controller.enabled = false;
            _player.transform.position = new Vector3(-2.9f, y, -3f);
            controller.enabled = true;

            var agent = director.Agents[0];
            yield return WaitFor(() => agent.transform.position.x < -1.5f && agent.transform.position.z > -2.9f, 15f);
            for (var seconds = 0f; seconds < 2f; seconds += Time.deltaTime)
            {
                Assert.That(agent.transform.position.z, Is.GreaterThan(-2.9f), "they don't push through");
                yield return null;
            }

            controller.enabled = false;
            _player.transform.position = new Vector3(2.45f, y, 1f);
            controller.enabled = true;
            yield return WaitFor(() => agent.transform.position.z < -3.2f, 15f);
        }
    }
}
