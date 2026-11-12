using System.Collections;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Customers;
using Game.Gameplay.Stations;
using Game.Simulation.Items;
using Game.Simulation.Service;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Tests.PlayMode
{
    /// <summary>The real scene, running: a delivery counter bought, an order from the app, and its courier (decision 2026-11-12).</summary>
    public class DeliveryPlayTests
    {
        private const string SceneName = "Kitchen_Greybox";

        private KitchenCompositionRoot _root;
        private CourierDirector _couriers;
        private DeliveryCounterStation _counter;
        private DeliveryOrder _watched;

        private DeliveryService Deliveries => _root.Services.Service.Deliveries;

        // Buys a delivery counter somewhere it fits in the dining room, then opens for service.
        private IEnumerator OpenWithADeliveryCounter()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _couriers = Object.FindFirstObjectByType<CourierDirector>();
            var editor = Object.FindFirstObjectByType<LayoutEditor>();

            var keyboard = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.GetDevice<Mouse>() ?? InputSystem.AddDevice<Mouse>();
            var player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Keyboard&Mouse", pairWithDevices: new InputDevice[] { keyboard, mouse });
            yield return null;
            yield return null;

            // A new restaurant can't afford the counter on its first morning; these tests aren't about saving up.
            _root.Services.Days.Receive(500f);
            var cursor = editor.Enter(player);
            cursor.Position = new SimVector2(0f, -5f);
            Assert.That(editor.Buy(cursor, editor.Shop.First(e => e.Id == "delivery counter")), Is.True, "bought");
            _counter = (DeliveryCounterStation)cursor.Held;
            var placed = false;
            for (var z = -6f; z <= -3.4f && !placed; z += 0.4f)
            {
                for (var x = -3f; x <= 3f && !placed; x += 0.5f)
                {
                    cursor.Position = new SimVector2(x, z);
                    editor.MoveCursor(cursor, SimVector2.Zero);
                    placed = editor.Place(cursor);
                }
            }
            Assert.That(placed, Is.True, "somewhere in the dining room takes it");
            editor.Leave(player);
            yield return null;

            var days = _root.Services.Days;
            days.Advance(); // to Prep
            days.Advance(); // to Service
            yield return null;
        }

        // Orders come on the game clock; bring the next one on now.
        private DeliveryOrder NextOrder()
        {
            var service = _root.Services.Service;
            var count = Deliveries.Orders.Count;
            for (var i = 0; i < 100000 && Deliveries.Orders.Count == count; i++)
                service.Tick(1f);
            Assert.That(Deliveries.Orders.Count, Is.EqualTo(count + 1), "an order came in");
            _watched = Deliveries.Orders.Last();
            return _watched;
        }

        private void PullUp(DeliveryOrder order)
        {
            var service = _root.Services.Service;
            for (var i = 0; i < 100000 && order.State == DeliveryState.Ordered; i++)
                service.Tick(1f);
            Assert.That(order.State, Is.EqualTo(DeliveryState.CourierComing));
        }

        private IEnumerator WaitFor(System.Func<bool> condition, float seconds)
        {
            var until = Time.time + seconds;
            while (!condition() && Time.time < until)
                yield return null;
            Assert.That(condition(), Is.True, "in time: " + Where());
        }

        // Where everyone is, for a failure's message.
        private string Where()
        {
            var order = _watched;
            if (order == null)
                return "no order";
            var scooter = GameObject.Find($"Scooter_{order.Id}");
            var courier = _couriers.TryGetCourier(order, out var agent) ? agent.transform.position.ToString("F1") : "none";
            return $"order {order.State}, patience {order.Patience:F2}, scooter {(scooter != null ? scooter.transform.position.ToString("F1") : "none")}, " +
                $"courier {courier}, counter {_counter.transform.position.ToString("F1")}, door {_couriers.transform.Find("Street door")?.position.ToString("F1")}, time {Time.time:F1}";
        }

        [UnityTest]
        public IEnumerator TheCounterPutsTheRestaurantOnTheApp()
        {
            yield return OpenWithADeliveryCounter();
            Assert.That(Deliveries.SlotCount, Is.EqualTo(_counter.PlaceCount));
            var order = NextOrder();
            Assert.That(_counter.AnchorOfSlot(order.Slot), Is.Not.Null, "it claimed one of the counter's places");
            Assert.That(_couriers.TryGetCourier(order, out _), Is.False, "nobody yet");
        }

        [UnityTest]
        public IEnumerator ACourierRidesUpTakesTheBoxPaysAndRidesOff()
        {
            yield return OpenWithADeliveryCounter();
            var services = _root.Services;
            var order = NextOrder();

            // A boxed steak, left for the courier.
            var box = Plate.NewBox(services.Equipment.PlateCapacity);
            box.Put(services.CreateItem(order.Order.MainTypeId));
            Assert.That(_counter.Container.CanAccept(box));
            _counter.Container.Put(box);
            services.ItemViews.PlaceImmediately(box, _counter.AnchorFor(box));
            Assert.That(_counter.AnchorFor(box), Is.SameAs(_counter.AnchorOfSlot(order.Slot)), "on the order's place");

            var before = services.Service.Money;
            PullUp(order);
            yield return null;
            var scooter = GameObject.Find($"Scooter_{order.Id}");
            Assert.That(scooter, Is.Not.Null, "a scooter rides in");
            Assert.That(Mathf.Abs(scooter.transform.position.x), Is.GreaterThan(12f), "from off screen");

            // Parks, walks in, takes it.
            yield return WaitFor(() => _couriers.TryGetCourier(order, out _), 15f);
            yield return WaitFor(() => order.State == DeliveryState.OutForDelivery, 40f);
            Assert.That(order.Payment, Is.GreaterThan(0f));
            Assert.That(services.Service.Money - before, Is.GreaterThanOrEqualTo(order.Payment), "paid at the counter");
            Assert.That(_counter.IsClear, "the box went with them");
            Assert.That(services.ItemViews.TryGetView(box, out _), Is.False);
            Assert.That(order.Recipient.Satisfaction, Is.Not.Null, "scored as it will arrive");

            // Back out and away.
            yield return WaitFor(() => GameObject.Find($"Scooter_{order.Id}") == null, 40f);
            Assert.That(_couriers.Agents, Is.Empty);
        }

        [UnityTest]
        public IEnumerator WithNothingToTakeTheCourierWaitsThenLeavesEmptyHanded()
        {
            yield return OpenWithADeliveryCounter();
            var order = NextOrder();
            PullUp(order);

            yield return WaitFor(() => order.State == DeliveryState.CourierWaiting, 40f);
            Assert.That(_couriers.TryGetCourier(order, out var courier));
            var place = _counter.AnchorOfSlot(order.Slot).position;
            Assert.That(Vector2.Distance(new Vector2(courier.transform.position.x, courier.transform.position.z), new Vector2(place.x, place.z)), Is.LessThan(2f), "at the counter");

            // Their patience runs on the game clock; hurry it along.
            var service = _root.Services.Service;
            for (var i = 0; i < 100000 && order.State == DeliveryState.CourierWaiting; i++)
                service.Tick(1f);
            Assert.That(order.State, Is.EqualTo(DeliveryState.Cancelled));
            Assert.That(service.Reviews.Any(r => r.Delivery && r.Customer == order.Recipient));
            yield return WaitFor(() => GameObject.Find($"Scooter_{order.Id}") == null, 40f);
        }
    }
}
