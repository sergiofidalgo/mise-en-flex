using System.Collections.Generic;
using Game.Gameplay.Stations;
using Game.Simulation.Days;
using Game.Simulation.Movement;
using Game.Simulation.Service;
using UnityEngine;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Gameplay.Customers
{
    /// <summary>
    /// Puts delivery couriers in the scene (decision 2026-11-12). When an order's courier is due, a
    /// scooter rides in along the road and parks by the door; the courier gets off, walks in round
    /// the furniture to the delivery counter and tells the simulation they're there. They wait
    /// until the food's handed over (or their patience runs out), walk back out and ride off.
    /// They're bodies like anyone else, pushed and pushing.
    /// </summary>
    public sealed class CourierDirector : MonoBehaviour
    {
        private const float ArriveDistance = 0.05f;

        private enum Stage
        {
            RidingIn,
            ToDoor,
            ToCounter,
            AtCounter,
            ToExit,
            ToScooter,
            RidingOff,
        }

        private sealed class Trip
        {
            public DeliveryOrder Order;
            public Stage Stage;
            public Transform Scooter;
            public GameObject Rider;
            public CourierAgent Agent;
            public Vector3 Park;
            public Vector3 Leave;
            public float Speed;
            public List<SimVector2> Path;
            public int Next;
            public float Since;

            // Whether they're getting anywhere indoors, measured over a stretch of time; blocked by
            // people, they stop being a body for a moment and walk through.
            public Vector3 ProgressFrom;
            public float ProgressSince;
            public float ThroughUntil;
        }

        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private CustomerDirector _customers;
        [SerializeField] private CourierAgent _courierPrefab;
        [Tooltip("A scooter, its front towards +z, with a child named Rider: the courier sitting on it, hidden while they're on foot.")]
        [SerializeField] private GameObject _scooterPrefab;
        [Tooltip("Off screen at each end of the lane scooters ride, just off the kerb. The line between them is the lane.")]
        [SerializeField] private Transform[] _roadEnds = System.Array.Empty<Transform>();

        [Tooltip("Metres per second a scooter rides at.")]
        [SerializeField, Min(0.5f)] private float _rideSpeed = 7f;
        [Tooltip("Metres per second per second it brakes and pulls away at.")]
        [SerializeField, Min(0.5f)] private float _acceleration = 5f;
        [Tooltip("Metres between scooters parked at the kerb.")]
        [SerializeField, Min(0.5f)] private float _parkSpacing = 1.5f;
        [Tooltip("Metres along the kerb from the door to the first parked scooter, clear of the doorstep.")]
        [SerializeField, Min(0f)] private float _parkFromDoor = 1.6f;
        [Tooltip("How much faster than a guest a courier walks.")]
        [SerializeField, Min(0.1f)] private float _hurry = 1.25f;
        [Tooltip("Metres out from the door where they step in and out.")]
        [SerializeField, Min(0.3f)] private float _doorstep = 0.9f;
        [Tooltip("Real seconds after which a courier still on their way in is at the counter anyway, so a blocked room can't hold up a service.")]
        [SerializeField, Min(1f)] private float _walkTimeout = 30f;
        [Tooltip("Real seconds over which a walking courier's progress is measured, and the metres they have to cover in it.")]
        [SerializeField, Min(0.1f)] private float _stuckSeconds = 1f;
        [SerializeField, Min(0f)] private float _stuckDistance = 0.4f;
        [Tooltip("Real seconds a blocked courier walks through people, so a crowded doorway can't hold a delivery up.")]
        [SerializeField, Min(0f)] private float _throughSeconds = 1.5f;

        private readonly List<Trip> _trips = new List<Trip>();
        private readonly List<CourierAgent> _agents = new List<CourierAgent>();
        private ServiceSimulation _service;
        private StationLayout _layout;
        private int _parked;

        /// <summary>
        /// The couriers who take up space right now, for the push rules: those indoors. On the
        /// pavement they pass through the queue, as the queue's guests would never get out of a
        /// doorway's way, and one who's blocked indoors walks through for a moment.
        /// </summary>
        public IReadOnlyList<CourierAgent> Agents => _agents;

        /// <summary>Raised as a courier's scooter pulls up at the kerb.</summary>
        public event System.Action<DeliveryOrder> PulledUp;

        /// <summary>The courier on foot for this order, if they're in the scene: their bubble follows them.</summary>
        public bool TryGetCourier(DeliveryOrder order, out CourierAgent agent)
        {
            foreach (var trip in _trips)
            {
                if (trip.Order == order && trip.Agent != null)
                {
                    agent = trip.Agent;
                    return true;
                }
            }
            agent = null;
            return false;
        }

        private bool HasRoad => _roadEnds.Length >= 2 && _roadEnds[0] != null && _roadEnds[1] != null && _scooterPrefab != null;

        private void Start()
        {
            _service = _root.Services.Service;
            _service.Deliveries.CourierDue += HandleCourierDue;
            _root.Services.ServiceReplaced += HandleServiceReplaced;
            _root.Services.Days.PhaseChanged += HandlePhaseChanged;
        }

        private void OnDestroy()
        {
            if (_service == null)
                return;
            _service.Deliveries.CourierDue -= HandleCourierDue;
            _root.Services.ServiceReplaced -= HandleServiceReplaced;
            _root.Services.Days.PhaseChanged -= HandlePhaseChanged;
        }

        private void HandleServiceReplaced(ServiceSimulation old, ServiceSimulation next)
        {
            old.Deliveries.CourierDue -= HandleCourierDue;
            next.Deliveries.CourierDue += HandleCourierDue;
            _service = next;
            // Anyone still about belongs to yesterday.
            foreach (var trip in _trips)
                Remove(trip);
            _trips.Clear();
            _parked = 0;
        }

        // Furniture only moves between services, so the way through it is worked out as doors open.
        private void HandlePhaseChanged(DayPhase phase)
        {
            if (phase == DayPhase.Service)
                _layout = _root.Services.NewLayout();
        }

        private void HandleCourierDue(DeliveryOrder order)
        {
            var trip = new Trip { Order = order, Since = Time.time };
            _trips.Add(trip);
            if (!HasRoad)
            {
                // No street to ride in on: they appear in the doorway.
                trip.Stage = Stage.ToCounter;
                trip.Agent = NewAgent(order, Door.position, Door.rotation, inside: true);
                return;
            }

            // In from one end, out by the other; parked along the kerb past the door, each further on.
            var from = (order.Id & 1) == 0 ? 0 : 1;
            var start = _roadEnds[from].position;
            trip.Leave = _roadEnds[1 - from].position;
            var along = (trip.Leave - start).normalized;
            var atDoor = LanePoint(Door.position);
            trip.Park = atDoor + along * (_parkFromDoor + _parkSpacing * (_parked++ % 3));
            trip.Stage = Stage.RidingIn;
            trip.Scooter = Instantiate(_scooterPrefab, start, Quaternion.LookRotation(along, Vector3.up)).transform;
            trip.Scooter.name = $"Scooter_{order.Id}";
            trip.Rider = trip.Scooter.Find("Rider")?.gameObject;
        }

        private void Update()
        {
            if (_service == null)
                return;
            _layout ??= _root.Services.NewLayout();

            for (var i = _trips.Count - 1; i >= 0; i--)
            {
                var trip = _trips[i];
                if (trip.Agent != null)
                    trip.Agent.Intent = SimVector2.Zero;
                if (Advance(trip))
                {
                    Remove(trip);
                    _trips.RemoveAt(i);
                }
            }
            if (_trips.Count == 0)
                _parked = 0;

            _agents.Clear();
            foreach (var trip in _trips)
            {
                var indoors = trip.Stage == Stage.ToCounter || trip.Stage == Stage.AtCounter || trip.Stage == Stage.ToExit;
                if (trip.Agent != null && indoors && Time.time >= trip.ThroughUntil)
                    _agents.Add(trip.Agent);
            }
        }

        // One frame of a courier's visit. True once they've ridden off.
        private bool Advance(Trip trip)
        {
            var stride = _root.Services.ServiceConfig.WalkSpeed * _hurry * Time.deltaTime;
            switch (trip.Stage)
            {
                case Stage.RidingIn:
                    if (!Ride(trip, trip.Park, stopping: true))
                        break;
                    PulledUp?.Invoke(trip.Order);
                    if (trip.Rider != null)
                        trip.Rider.SetActive(false);
                    trip.Agent = NewAgent(trip.Order, BesideScooter(trip), trip.Scooter.rotation, inside: false);
                    trip.Since = Time.time;
                    trip.Stage = Stage.ToDoor;
                    break;

                case Stage.ToDoor:
                    // Along the pavement to the doorstep, then in through the door.
                    if (StepOutside(trip, stride, Doorstep(), Door.position))
                    {
                        trip.Agent.Slide = _layout.Slide;
                        trip.Path = null;
                        trip.Stage = Stage.ToCounter;
                    }
                    break;

                case Stage.ToCounter:
                    var counter = CounterFor(trip.Order);
                    if (counter == null || trip.Order.State != DeliveryState.CourierComing)
                    {
                        // No counter to go to: they wait where they are, and their patience does the rest.
                        _service.Deliveries.CourierAtCounter(trip.Order);
                        trip.Stage = Stage.AtCounter;
                        break;
                    }
                    if (trip.Path == null)
                    {
                        trip.Path = WayTo(trip.Agent, counter);
                        trip.Next = 0;
                    }
                    var late = Time.time - trip.Since > _walkTimeout;
                    if (late && trip.Path.Count > 0)
                        Put(trip.Agent, trip.Path[trip.Path.Count - 1]);
                    if (late || Follow(trip, stride))
                    {
                        trip.Agent.Face(counter.transform.position - trip.Agent.transform.position);
                        _service.Deliveries.CourierAtCounter(trip.Order);
                        trip.Stage = Stage.AtCounter;
                    }
                    break;

                case Stage.AtCounter:
                    var at = CounterFor(trip.Order);
                    if (trip.Order.State == DeliveryState.CourierWaiting)
                    {
                        if (at != null)
                            trip.Agent.Face(at.transform.position - trip.Agent.transform.position);
                        break;
                    }
                    // Handed over, or out of patience: back to the scooter.
                    trip.Path = _layout.FindPath(Floor(trip.Agent.transform.position), Floor(Door.position))
                        ?? new List<SimVector2> { Floor(Door.position) };
                    trip.Next = 0;
                    trip.Stage = Stage.ToExit;
                    break;

                case Stage.ToExit:
                    if (!Follow(trip, stride))
                        break;
                    trip.Agent.Slide = null;
                    trip.Path = null;
                    trip.Next = 0;
                    trip.Stage = Stage.ToScooter;
                    break;

                case Stage.ToScooter:
                    if (trip.Scooter == null)
                        return true;
                    if (!StepOutside(trip, stride, Doorstep(), BesideScooter(trip)))
                        break;
                    Destroy(trip.Agent.gameObject);
                    trip.Agent = null;
                    if (trip.Rider != null)
                        trip.Rider.SetActive(true);
                    trip.Speed = 0f;
                    trip.Stage = Stage.RidingOff;
                    break;

                case Stage.RidingOff:
                    return Ride(trip, trip.Leave, stopping: false);
            }
            return false;
        }

        // The scooter along the lane: up to speed, and braking to a stop where it parks. True once there.
        private bool Ride(Trip trip, Vector3 to, bool stopping)
        {
            var scooter = trip.Scooter;
            to.y = scooter.position.y;
            var left = (to - scooter.position).magnitude;
            if (left <= ArriveDistance)
                return true;
            // v² = 2·a·d: the speed it can still shed in the distance left.
            var limit = stopping ? Mathf.Sqrt(2f * _acceleration * left) + 0.3f : _rideSpeed;
            if (trip.Stage == Stage.RidingIn && trip.Speed <= 0f)
                trip.Speed = _rideSpeed; // already moving as it comes into view
            trip.Speed = Mathf.Min(Mathf.MoveTowards(trip.Speed, _rideSpeed, _acceleration * Time.deltaTime), limit);
            var direction = (to - scooter.position) / left;
            scooter.rotation = Quaternion.RotateTowards(scooter.rotation, Quaternion.LookRotation(direction, Vector3.up), 240f * Time.deltaTime);
            scooter.position = Vector3.MoveTowards(scooter.position, to, trip.Speed * Time.deltaTime);
            return (to - scooter.position).magnitude <= ArriveDistance;
        }

        // On the street: straight from point to point, nothing to walk round. True at the last.
        private bool StepOutside(Trip trip, float stride, Vector3 first, Vector3 second)
        {
            var agent = trip.Agent;
            var before = agent.transform.position;
            var target = trip.Next == 0 ? first : second;
            var arrived = agent.WalkTowards(target, stride, ArriveDistance);
            var moved = agent.transform.position - before;
            agent.Intent = new SimVector2(moved.x, moved.z);
            if (!arrived)
                return false;
            if (trip.Next == 0)
            {
                trip.Next = 1;
                return false;
            }
            trip.Next = 0;
            return true;
        }

        // Inside: along the way worked out round the furniture. True at its end.
        private bool Follow(Trip trip, float stride)
        {
            var agent = trip.Agent;
            if (trip.Path == null || trip.Next >= trip.Path.Count)
                return true;
            var before = agent.transform.position;
            var point = trip.Path[trip.Next];
            var arrived = agent.WalkTowards(new Vector3(point.X, before.y, point.Y), stride, ArriveDistance);
            var moved = agent.transform.position - before;
            agent.Intent = new SimVector2(moved.x, moved.z);
            if (arrived)
                trip.Next++;

            // Not getting anywhere (someone in the way who won't give): through them, for a moment.
            if (Time.time - trip.ProgressSince >= _stuckSeconds)
            {
                if (trip.ProgressSince > 0f && (agent.transform.position - trip.ProgressFrom).magnitude < _stuckDistance)
                    trip.ThroughUntil = Time.time + _throughSeconds;
                trip.ProgressFrom = agent.transform.position;
                trip.ProgressSince = Time.time;
            }
            return trip.Next >= trip.Path.Count;
        }

        // The shortest way from where they stand to any side of the counter that can be reached. With
        // none (boxed in by the furniture), straight to it: a courier always gets there, so a bad
        // layout shows as an odd walk, not as orders that can never be collected.
        private List<SimVector2> WayTo(CourierAgent agent, DeliveryCounterStation counter)
        {
            var here = Floor(agent.transform.position);
            List<SimVector2> best = null;
            var bestLength = float.MaxValue;
            foreach (var side in _layout.FreeSides(counter.Footprint))
            {
                var path = _layout.FindPath(here, side);
                if (path == null)
                    continue;
                var length = 0f;
                var from = here;
                foreach (var point in path)
                {
                    length += (point - from).Length();
                    from = point;
                }
                if (length < bestLength)
                {
                    best = path;
                    bestLength = length;
                }
            }
            return best ?? new List<SimVector2> { _layout.StandingSpot(counter.Footprint) ?? Floor(counter.transform.position) };
        }

        private DeliveryCounterStation CounterFor(DeliveryOrder order)
        {
            foreach (var station in _root.Services.Stations.All)
            {
                if (station is DeliveryCounterStation counter && counter.AnchorOfSlot(order.Slot) != null)
                    return counter;
            }
            return null;
        }

        private CourierAgent NewAgent(DeliveryOrder order, Vector3 position, Quaternion rotation, bool inside)
        {
            var agent = Instantiate(_courierPrefab, position, rotation);
            agent.name = $"Courier_{order.Id}";
            agent.Initialize(order);
            agent.Slide = inside ? _layout.Slide : null;
            return agent;
        }

        private void Remove(Trip trip)
        {
            if (trip.Agent != null)
            {
                Destroy(trip.Agent.gameObject);
            }
            if (trip.Scooter != null)
                Destroy(trip.Scooter.gameObject);
        }

        private Transform Door => _customers.Door;

        private Vector3 Doorstep() => Door.position - Door.forward * _doorstep;

        // On the pavement side of the parked scooter, where they get off and on.
        private Vector3 BesideScooter(Trip trip)
        {
            var towardsDoor = Door.position - trip.Park;
            towardsDoor.y = 0f;
            var side = Vector3.Cross(Vector3.up, trip.Scooter.forward);
            if (Vector3.Dot(side, towardsDoor) < 0f)
                side = -side;
            var spot = trip.Park + side * 0.7f;
            spot.y = Door.position.y;
            return spot;
        }

        // The nearest point on the lane (the line between the road's ends).
        private Vector3 LanePoint(Vector3 position)
        {
            var a = _roadEnds[0].position;
            var b = _roadEnds[1].position;
            var along = b - a;
            var t = Mathf.Clamp01(Vector3.Dot(position - a, along) / Mathf.Max(along.sqrMagnitude, 1e-4f));
            return a + along * t;
        }

        private static void Put(CourierAgent agent, SimVector2 point) =>
            agent.transform.position = new Vector3(point.X, agent.transform.position.y, point.Y);

        private static SimVector2 Floor(Vector3 position) => new SimVector2(position.x, position.z);
    }
}
