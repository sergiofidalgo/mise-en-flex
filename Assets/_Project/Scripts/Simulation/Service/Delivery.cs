using System;
using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Items;
using Game.Simulation.Quality;

namespace Game.Simulation.Service
{
    /// <summary>Tuning for delivery orders, in in-game seconds. Filled from the ServiceConfig asset.</summary>
    public sealed class DeliverySettings
    {
        /// <summary>Average in-game seconds between delivery orders outside the rush, at the suggested prices.</summary>
        public float OrderIntervalSeconds { get; set; } = 45f * 60f;

        /// <summary>±fraction of random jitter on each gap between orders.</summary>
        public float OrderJitter { get; set; } = 0.35f;

        /// <summary>From the order coming in to the courier pulling up: the time there is to cook it.</summary>
        public float CourierLeadSeconds { get; set; } = 25f * 60f;

        /// <summary>How long a courier waits at the counter for food that isn't there, at a patience dial of 1.</summary>
        public float CourierPatienceSeconds { get; set; } = 12f * 60f;

        /// <summary>From pick-up to the customer's door, when the review comes in.</summary>
        public float RideSeconds { get; set; } = 20f * 60f;

        /// <summary>
        /// Cooking seconds the food spends cooling on the ride as if it sat out on a plate. A box
        /// keeps it warm: it only feels <see cref="BoxCoolingFactor"/> of this.
        /// </summary>
        public float RideCoolingSeconds { get; set; } = 240f;

        /// <summary>The share of the ride's cooling that reaches food in a delivery box (1 = no better than a plate).</summary>
        public float BoxCoolingFactor { get; set; } = 0.35f;

        /// <summary>The app's share of the order's price. The courier pays the rest, and nobody tips.</summary>
        public float Commission { get; set; } = 0.25f;

        /// <summary>Stars left by a customer whose courier went away empty-handed.</summary>
        public float CancelledStars { get; set; } = 1f;

        /// <summary>What a customer whose order never came says, one picked at random.</summary>
        public string[] CancelledLines { get; set; } =
        {
            "Ordered on the app. The courier left without my food.",
            "Order cancelled after an hour of watching the map.",
            "The app says cancelled. My stomach says worse.",
        };
    }

    public enum DeliveryState
    {
        /// <summary>The order is in and its place on the counter is marked; the courier is on the way.</summary>
        Ordered,
        /// <summary>The courier has pulled up and is walking in (gameplay moves them and calls CourierAtCounter).</summary>
        CourierComing,
        /// <summary>The courier stands at the counter. Their patience drains while there's nothing to take.</summary>
        CourierWaiting,
        /// <summary>Picked up and paid for; on its way to the customer.</summary>
        OutForDelivery,
        /// <summary>At the customer's door, scored and (maybe) reviewed.</summary>
        Delivered,
        /// <summary>The courier gave up waiting.</summary>
        Cancelled,
    }

    /// <summary>One delivery order, from the app's ping to the review.</summary>
    public sealed class DeliveryOrder
    {
        internal DeliveryOrder(int id, Order order, int slot, Customer recipient, float lead)
        {
            Id = id;
            Order = order;
            Slot = slot;
            Recipient = recipient;
            LeadSeconds = lead;
            CourierIn = lead;
        }

        public int Id { get; }

        public Order Order { get; }

        /// <summary>The place on the delivery counter it claimed, kept until it's picked up or cancelled.</summary>
        public int Slot { get; }

        public DeliveryState State { get; internal set; }

        /// <summary>Who ordered: never seen in the restaurant, but they pay, rate and review as a guest does.</summary>
        public Customer Recipient { get; }

        /// <summary>In-game seconds until the courier pulls up, and what it started at.</summary>
        public float CourierIn { get; internal set; }
        public float LeadSeconds { get; }

        /// <summary>1 when the order came in, 0 as the courier pulls up: the ring on its bubble.</summary>
        public float CountdownLeft => LeadSeconds > 0f ? Math.Clamp(CourierIn / LeadSeconds, 0f, 1f) : 0f;

        /// <summary>The courier's patience, 1 to 0, draining only while they wait at the counter.</summary>
        public float Patience { get; internal set; } = 1f;

        /// <summary>What the courier took: a box, or a plate that won't be coming back.</summary>
        public Plate Taken { get; internal set; }

        /// <summary>What the courier paid at the counter: the order's price less the app's share.</summary>
        public float Payment { get; internal set; }

        /// <summary>Money given back when the customer found something missing; 0 otherwise.</summary>
        public float Refund { get; internal set; }

        /// <summary>In-game seconds of the ride left.</summary>
        public float RideLeft { get; internal set; }

        /// <summary>True while the order holds its place on the counter.</summary>
        public bool HoldsSlot => State == DeliveryState.Ordered || State == DeliveryState.CourierComing || State == DeliveryState.CourierWaiting;

        /// <summary>True once nothing more will happen to it.</summary>
        public bool IsOver => State == DeliveryState.Delivered || State == DeliveryState.Cancelled;
    }

    /// <summary>
    /// Delivery orders (decision 2026-11-12). An order comes in through the app and claims a free
    /// place on a delivery counter; a courier pulls up <see cref="DeliverySettings.CourierLeadSeconds"/>
    /// later, walks in, takes whatever stands on that place (right or wrong), pays the price less
    /// the app's commission and rides off. The customer rates it as it reaches them, after the ride,
    /// and their review comes in then. A courier kept waiting too long leaves, and the order is
    /// cancelled with a bad review. No counter, no orders. Plain C#, part of
    /// <see cref="ServiceSimulation"/>, which ticks it and takes its money and reviews.
    /// </summary>
    public sealed class DeliveryService
    {
        private readonly ServiceSettings _service;
        private readonly Func<int> _nextCustomerId;
        private readonly List<DeliveryOrder> _orders = new List<DeliveryOrder>();
        // Their own dice, so delivery changes nobody's arrival, order or review in the dining room.
        private readonly Random _random;
        private readonly Random _orderRandom;
        private readonly Random _reviewRandom;
        private readonly Random _lookRandom;
        private DeliveryOrder[] _slots = new DeliveryOrder[0];
        private float _nextOrderIn = -1f;
        private int _nextId = 1;

        public DeliveryService(ServiceSettings service, Func<int> nextCustomerId)
        {
            _service = service;
            _nextCustomerId = nextCustomerId;
            _random = new Random(unchecked(service.Seed * 31 + 67));
            _orderRandom = new Random(unchecked(service.Seed * 31 + 71));
            _reviewRandom = new Random(unchecked(service.Seed * 31 + 73));
            _lookRandom = new Random(unchecked(service.Seed * 31 + 79));
        }

        private DeliverySettings Settings => _service.Delivery;

        /// <summary>Every order this service, oldest first.</summary>
        public IReadOnlyList<DeliveryOrder> Orders => _orders;

        /// <summary>Places on the delivery counters: how many orders can be open at once.</summary>
        public int SlotCount => _slots.Length;

        /// <summary>The order holding this place, or null.</summary>
        public DeliveryOrder AtSlot(int slot) => slot >= 0 && slot < _slots.Length ? _slots[slot] : null;

        /// <summary>Money from deliveries: what couriers paid, less refunds.</summary>
        public float Money { get; private set; }

        public int PickedUp { get; private set; }

        public int CancelledCount { get; private set; }

        /// <summary>True when no order is still open or on the road, so a service can finish.</summary>
        public bool IsSettled
        {
            get
            {
                foreach (var order in _orders)
                {
                    if (!order.IsOver)
                        return false;
                }
                return true;
            }
        }

        /// <summary>A new order has claimed its place on the counter: the tablet pings.</summary>
        public event Action<DeliveryOrder> Ordered;

        /// <summary>The courier has pulled up outside; gameplay walks them to the counter.</summary>
        public event Action<DeliveryOrder> CourierDue;

        /// <summary>The courier has taken the food and paid.</summary>
        public event Action<DeliveryOrder> Collected;

        /// <summary>The courier gave up waiting.</summary>
        public event Action<DeliveryOrder> Cancelled;

        /// <summary>The food has reached the customer.</summary>
        public event Action<DeliveryOrder> Delivered;

        /// <summary>A customer was given money back for something missing.</summary>
        public event Action<DeliveryOrder> Refunded;

        /// <summary>A delivery customer has posted a review.</summary>
        public event Action<Review> Reviewed;

        /// <summary>
        /// Sets today's places on the delivery counters, before the first order. None: no delivery.
        /// </summary>
        public void OpenSlots(int count)
        {
            if (_orders.Count > 0)
                throw new InvalidOperationException("Delivery places are set before the first order.");
            _slots = new DeliveryOrder[Math.Max(0, count)];
            _nextOrderIn = -1f;
        }

        /// <param name="at">In-game seconds into the service, for the rush.</param>
        /// <param name="takingOrders">False from last orders on: what's open runs its course, nothing new comes in.</param>
        /// <param name="patienceMultiplier">The customer patience dial, which couriers share; 0 = they never leave while open.</param>
        /// <param name="closed">True once the service time is up: endless patience ends then.</param>
        public void Tick(float seconds, float at, bool takingOrders, float patienceMultiplier = 1f, bool closed = false)
        {
            if (seconds <= 0f)
                return;

            foreach (var order in _orders.ToArray())
            {
                switch (order.State)
                {
                    case DeliveryState.Ordered:
                        order.Recipient.SecondsInRestaurant += seconds;
                        order.CourierIn -= seconds;
                        if (order.CourierIn <= 0f)
                        {
                            order.CourierIn = 0f;
                            order.State = DeliveryState.CourierComing;
                            CourierDue?.Invoke(order);
                        }
                        break;
                    case DeliveryState.CourierComing:
                        order.Recipient.SecondsInRestaurant += seconds;
                        break;
                    case DeliveryState.CourierWaiting:
                        order.Recipient.SecondsInRestaurant += seconds;
                        DrainPatience(order, seconds, patienceMultiplier, closed);
                        break;
                    case DeliveryState.OutForDelivery:
                        order.Recipient.SecondsInRestaurant += seconds;
                        order.RideLeft -= seconds;
                        if (order.RideLeft <= 0f)
                            Deliver(order);
                        break;
                }
            }

            if (_slots.Length == 0 || Settings.OrderIntervalSeconds <= 0f || !takingOrders)
                return;
            if (_nextOrderIn < 0f)
                _nextOrderIn = NextGap(at);
            _nextOrderIn -= seconds;
            if (_nextOrderIn > 0f)
                return;
            // With every place taken the app shows the restaurant as busy: that order goes elsewhere.
            _nextOrderIn = NextGap(at);
            var slot = Array.IndexOf(_slots, null);
            if (slot >= 0)
                Place(slot, at);
        }

        /// <summary>Gameplay: the courier has reached the counter. They take the food if it's there, or start waiting.</summary>
        public void CourierAtCounter(DeliveryOrder order)
        {
            if (order.State == DeliveryState.CourierComing)
                order.State = DeliveryState.CourierWaiting;
        }

        /// <summary>
        /// The courier takes what stands on the order's place and pays for the order as placed: they
        /// don't look in the box. What the customer makes of it comes later, with the review.
        /// </summary>
        /// <param name="score">The dish as it will reach the customer, after the ride, against their order.</param>
        public bool Collect(DeliveryOrder order, Plate plate, DishScore score)
        {
            if (order.State != DeliveryState.CourierWaiting || plate == null || plate.Items.Count == 0 || score == null)
                return false;

            var recipient = order.Recipient;
            recipient.ServedPlate = plate;
            recipient.Satisfaction = score;
            // A courier kept waiting is a late delivery: the wait reaches the review as slow service.
            recipient.PatienceWhenServed = order.Patience;

            order.Taken = plate;
            order.Payment = Round(Price(order.Order) * (1f - Math.Clamp(Settings.Commission, 0f, 1f)));
            recipient.Bill = new Bill(order.Payment, 0f);
            Money += order.Payment;
            PickedUp++;
            order.RideLeft = Settings.RideSeconds;
            order.State = DeliveryState.OutForDelivery;
            Free(order);
            Collected?.Invoke(order);
            if (order.RideLeft <= 0f)
                Deliver(order);
            return true;
        }

        private void Place(int slot, float at)
        {
            var order = _service.Menu?.Pick(_orderRandom);
            if (order == null)
                return;
            var recipient = new Customer(_nextCustomerId())
            {
                Order = order,
                ArrivedAt = at,
                State = CustomerState.Gone, // never in the room
                Look = _service.CustomerLooks > 0 ? _lookRandom.Next(_service.CustomerLooks) : -1,
                OrderedDelivery = true,
            };
            recipient.Party = new[] { recipient };
            var placed = new DeliveryOrder(_nextId++, order, slot, recipient, Math.Max(0f, Settings.CourierLeadSeconds));
            _slots[slot] = placed;
            _orders.Add(placed);
            Ordered?.Invoke(placed);
        }

        // Linear, as a guest's: full to empty over CourierPatienceSeconds × the dial.
        private void DrainPatience(DeliveryOrder order, float seconds, float dial, bool closed)
        {
            if (Settings.CourierPatienceSeconds <= 0f)
                return;
            if (dial <= 0f && !closed)
                return;
            if (dial <= 0f)
                dial = 1f;
            order.Patience = Math.Max(0f, order.Patience - seconds / (Settings.CourierPatienceSeconds * dial));
            if (order.Patience > 0f)
                return;

            order.State = DeliveryState.Cancelled;
            CancelledCount++;
            Free(order);
            var recipient = order.Recipient;
            recipient.WalkedOut = true;
            recipient.RanOutOfPatience = true;
            Cancelled?.Invoke(order);

            var lines = Settings.CancelledLines;
            var line = lines != null && lines.Length > 0 ? lines[_reviewRandom.Next(lines.Length)] : string.Empty;
            Post(order, new Review(recipient, Settings.CancelledStars, ReviewTopic.WalkedOut, line));
        }

        // At the customer's door: anything they ordered and didn't get is refunded, and they review it.
        private void Deliver(DeliveryOrder order)
        {
            order.RideLeft = 0f;
            order.State = DeliveryState.Delivered;
            var score = order.Recipient.Satisfaction;
            var missing = 0f;
            if (score.Missing.Contains(order.Order.MainTypeId))
                missing += order.Order.MainPrice;
            if (order.Order.SideTypeId != null && score.Missing.Contains(order.Order.SideTypeId))
                missing += order.Order.SidePrice;
            if (missing > 0f)
            {
                order.Refund = Math.Min(order.Payment, Round(missing * (1f - Math.Clamp(Settings.Commission, 0f, 1f))));
                order.Recipient.Bill = new Bill(order.Payment - order.Refund, 0f);
                Money -= order.Refund;
                Refunded?.Invoke(order);
            }
            Delivered?.Invoke(order);
            Post(order, ReviewWriter.Write(order.Recipient, _service.Reviews, _reviewRandom));
        }

        private void Post(DeliveryOrder order, Review review)
        {
            if (review == null)
                return;
            review.Delivery = true;
            order.Recipient.Review = review;
            Reviewed?.Invoke(review);
        }

        private void Free(DeliveryOrder order)
        {
            if (order.Slot >= 0 && order.Slot < _slots.Length && _slots[order.Slot] == order)
                _slots[order.Slot] = null;
        }

        private static float Price(Order order) => order.MainPrice + (order.SideTypeId != null ? order.SidePrice : 0f);

        // The gap to the next order: prices and the rush move it as they move the guests at the door.
        private float NextGap(float at)
        {
            var interval = Settings.OrderIntervalSeconds / Math.Max(0.01f, _service.Menu?.Demand ?? 1f);
            if (_service.InRush(at))
                interval /= Math.Max(0.01f, _service.RushMultiplier);
            var jitter = 1f + Settings.OrderJitter * (float)(_random.NextDouble() * 2.0 - 1.0);
            return Math.Max(1f, interval * jitter);
        }

        private static float Round(float money) => (float)Math.Round(money, MidpointRounding.AwayFromZero);
    }
}
