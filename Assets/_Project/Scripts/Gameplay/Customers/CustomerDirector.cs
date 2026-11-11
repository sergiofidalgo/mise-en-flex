using System.Collections.Generic;
using Game.Gameplay.Staff;
using Game.Gameplay.Stations;
using Game.Simulation.Days;
using Game.Simulation.Movement;
using Game.Simulation.Service;
using UnityEngine;
using UnityEngine.InputSystem;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Gameplay.Customers
{
    /// <summary>
    /// Puts the service's customers in the scene: a body for each arrival, walking from the street
    /// door around the furniture to their seat (or a queue spot by the door), and walking back out.
    /// They're bodies like anyone else: people give way to them and can push them. Reports to the
    /// simulation when they've sat down, given up, or left.
    /// </summary>
    public sealed class CustomerDirector : MonoBehaviour
    {
        // Within this distance a customer has reached where they were going.
        private const float ArriveDistance = 0.05f;

        // Close enough to the door to be out of it, with someone else possibly in the doorway.
        private const float DoorReach = 0.4f;

        private enum WalkResult
        {
            Walking,
            Arrived,
            NoWay,
        }

        private sealed class Progress
        {
            public SimVector2 From;
            public float Since;
            public float SqueezingUntil;
            public float WalkingThroughUntil;
        }

        private sealed class CustomerWalk
        {
            public SimVector2 Goal;
            public List<SimVector2> Points;
            public int Next;
        }

        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private StaffDirector _staff;
        [SerializeField] private CustomerAgent _customerPrefab;
        [SerializeField] private Transform _door;

        [Tooltip("Spots by the door where customers wait for a stool, nearest the door first.")]
        [SerializeField] private Transform[] _queueSpots = System.Array.Empty<Transform>();

        [Tooltip("How wide a berth routes give a chair, in metres from its centre, on top of a walker's own width.")]
        [SerializeField, Min(0f)] private float _seatedBodyHalfWidth = 0.3f;
        [Tooltip("Real seconds over which someone's progress is measured.")]
        [SerializeField, Min(0.1f)] private float _stuckSeconds = 1f;
        [Tooltip("Metres they have to cover in that time, or they start squeezing through people.")]
        [SerializeField, Min(0f)] private float _stuckDistance = 0.5f;
        [Tooltip("Metres they have to cover while squeezing, or they're pinned by furniture and walk through.")]
        [SerializeField, Min(0f)] private float _pinnedDistance = 0.05f;
        [Tooltip("How close, in metres beyond touching, counts as someone pressing against them.")]
        [SerializeField, Min(0f)] private float _contactMargin = 0.05f;
        [Tooltip("Real seconds after being pushed before a seated customer goes back to their seat.")]
        [SerializeField, Min(0f)] private float _returnDelay = 0.8f;
        [Tooltip("Real seconds they keep squeezing through once they've started.")]
        [SerializeField, Min(0f)] private float _squeezeSeconds = 1.5f;

        [Header("The street (decision 2026-11-11)")]
        [Tooltip("Where customers appear and walk off: off screen at each end of the pavement. The line between them is the way along it. Empty: they appear in the doorway, as before.")]
        [SerializeField] private Transform[] _streetEnds = System.Array.Empty<Transform>();
        [Tooltip("Metres out from the door where they step in, and the queue's distance from the wall.")]
        [SerializeField, Min(0.3f)] private float _doorstep = 0.9f;
        [Tooltip("Metres along the wall from the doorstep to the first in the queue, clear of the doorway.")]
        [SerializeField, Min(0f)] private float _queueStart = 1.2f;
        [Tooltip("Metres between people queueing.")]
        [SerializeField, Min(0.3f)] private float _queueSpacing = 0.75f;
        [Tooltip("Metres between members of a group walking in, one behind another.")]
        [SerializeField, Min(0.3f)] private float _partyGap = 0.8f;
        [Tooltip("Real seconds after which someone still walking in is at the door anyway, so a blocked street can't hold up a service.")]
        [SerializeField, Min(1f)] private float _approachTimeout = 40f;

        private readonly Dictionary<Customer, CustomerAgent> _agents = new Dictionary<Customer, CustomerAgent>();
        private readonly Dictionary<CustomerAgent, CustomerWalk> _walks = new Dictionary<CustomerAgent, CustomerWalk>();
        private readonly Dictionary<CustomerAgent, Progress> _progress = new Dictionary<CustomerAgent, Progress>();
        private readonly Dictionary<CustomerAgent, float> _backToSeatAt = new Dictionary<CustomerAgent, float>();
        private readonly List<SimVector2> _others = new List<SimVector2>();
        private readonly List<Customer> _gone = new List<Customer>();
        private ServiceSimulation _service;
        private readonly HashSet<Customer> _walkedOut = new HashSet<Customer>();
        private StationLayout _layout;

        // The same, keeping clear of every chair: for planning a way, not for where they can step.
        private StationLayout _routes;
        private readonly Dictionary<int, ServingStation> _serving = new Dictionary<int, ServingStation>();

        // Outside: who's on the street (walking in, at the doorstep, queueing), the way each is
        // following there, when they set off, and who's walking off after their visit.
        private sealed class StreetWalk
        {
            public readonly List<Vector3> Points = new List<Vector3>();
            public int Next;
            public float Since;
        }

        private readonly HashSet<CustomerAgent> _outside = new HashSet<CustomerAgent>();
        private readonly Dictionary<CustomerAgent, StreetWalk> _streetWalks = new Dictionary<CustomerAgent, StreetWalk>();
        private readonly List<(CustomerAgent Agent, StreetWalk Walk)> _departing = new List<(CustomerAgent, StreetWalk)>();
        private readonly System.Random _streetRandom = new System.Random(11);
        private readonly Dictionary<Customer, int> _partyEnds = new Dictionary<Customer, int>();

        /// <summary>Every customer in the scene and their body, for HUD labels.</summary>
        public IReadOnlyDictionary<Customer, CustomerAgent> Agents => _agents;

        // Whether there's a street to walk in along.
        private bool HasStreet => _streetEnds.Length >= 2 && _streetEnds[0] != null && _streetEnds[1] != null;

        private void Start()
        {
            _service = _root.Services.Service;
            Listen(_service);
            _root.Services.ServiceReplaced += HandleServiceReplaced;
            _root.Services.Days.PhaseChanged += HandlePhaseChanged;
        }

        private void Listen(ServiceSimulation service)
        {
            service.GuestsWalkIn = HasStreet;
            service.Arrived += HandleArrived;
            service.Appeared += HandleAppeared;
        }

        // Furniture only moves between services, so the way through it is worked out as doors open.
        private void HandlePhaseChanged(DayPhase phase)
        {
            if (phase != DayPhase.Service)
                return;
            _layout = _root.Services.NewLayout();
            _serving.Clear();
            var chairs = new List<Footprint>();
            foreach (var station in _root.Services.Stations.All)
            {
                if (!(station is ServingStation serving))
                    continue;
                foreach (var seat in serving.SeatIndexes)
                {
                    _serving[seat] = serving;
                    var position = serving.SeatPosition(seat);
                    chairs.Add(new Footprint(Floor(position), 0f, new SimVector2(_seatedBodyHalfWidth, _seatedBodyHalfWidth)));
                }
            }
            _routes = _root.Services.NewLayout(chairs);
        }

        private void OnDestroy()
        {
            if (_service != null)
            {
                _service.Arrived -= HandleArrived;
                _service.Appeared -= HandleAppeared;
                _root.Services.ServiceReplaced -= HandleServiceReplaced;
                _root.Services.Days.PhaseChanged -= HandlePhaseChanged;
            }
        }

        private void HandleServiceReplaced(ServiceSimulation old, ServiceSimulation next)
        {
            old.Arrived -= HandleArrived;
            old.Appeared -= HandleAppeared;
            Listen(next);
            _service = next;
            _partyEnds.Clear();
        }

        // At the door. Someone who walked in up the street already has a body.
        private void HandleArrived(Customer customer)
        {
            if (_agents.ContainsKey(customer))
                return;
            var agent = Instantiate(_customerPrefab, _door.position, _door.rotation);
            agent.name = $"Customer_{customer.Id}";
            agent.Initialize(customer);
            agent.Slide = _layout.Slide;
            _agents[customer] = agent;
        }

        // Setting off up the street (decision 2026-11-11): from off screen at one end of the
        // pavement, a group one behind another, along it to the doorstep.
        private void HandleAppeared(Customer customer)
        {
            var leader = customer.Party[0];
            var place = 0;
            for (var i = 0; i < customer.Party.Count; i++)
                if (customer.Party[i] == customer)
                    place = i;
            // A group comes from one end, picked at random for its first member.
            if (!_partyEnds.TryGetValue(leader, out var side))
            {
                side = _streetRandom.Next(2);
                _partyEnds[leader] = side;
            }
            var end = _streetEnds[side].position;
            var lane = LanePoint(Doorstep());
            var away = (end - lane).normalized;
            var start = end + away * (_partyGap * place);

            var agent = Instantiate(_customerPrefab, start, Quaternion.LookRotation(-away, Vector3.up));
            agent.name = $"Customer_{customer.Id}";
            agent.Initialize(customer);
            _agents[customer] = agent;
            _outside.Add(agent);
            var walk = new StreetWalk { Since = Time.time };
            walk.Points.Add(lane);
            walk.Points.Add(Doorstep());
            _streetWalks[agent] = walk;
        }

        private void Update()
        {
            if (_service == null || _layout == null)
                return;

            var step = _root.Services.ServiceConfig.WalkSpeed * Time.deltaTime;
            _gone.Clear();
            foreach (var pair in _agents)
            {
                var customer = pair.Key;
                var agent = pair.Value;
                var stride = step * agent.SpeedFactor; // their own pace
                agent.Intent = SimVector2.Zero;
                var outside = _outside.Contains(agent);
                switch (customer.State)
                {
                    case CustomerState.Approaching:
                        // Up the street to the doorstep; there they're at the door, and wait for the
                        // rest of their group. Taking too long (something in the way), they're there.
                        if (customer.AtDoor)
                            break;
                        var walk = _streetWalks[agent];
                        if (Time.time - walk.Since > _approachTimeout)
                            agent.transform.position = Doorstep();
                        if (Time.time - walk.Since > _approachTimeout || FollowStreet(agent, walk, stride))
                        {
                            _streetWalks.Remove(agent);
                            agent.Face(_door.forward);
                            _service.ReachDoor(customer);
                        }
                        break;
                    case CustomerState.WalkingToSeat when outside:
                        // In through the door from the doorstep; inside, round the furniture as anyone.
                        if (agent.WalkTowards(_door.position, stride, ArriveDistance))
                        {
                            _outside.Remove(agent);
                            agent.Slide = _layout.Slide;
                        }
                        break;
                    case CustomerState.Queueing when outside || HasStreet:
                        // The queue is outside, along the front wall (decision 2026-11-11).
                        if (!outside)
                        {
                            // Anyone inside (in the doorway when it filled up) goes back out first.
                            if (Walk(agent, _door.position, stride) == WalkResult.NoWay)
                                agent.WalkTowards(_door.position, stride, ArriveDistance);
                            if ((Floor(agent.transform.position) - Floor(_door.position)).Length() <= DoorReach)
                            {
                                _outside.Add(agent);
                                agent.Slide = null;
                            }
                        }
                        else if (agent.WalkTowards(StreetQueueSpot(customer), stride, ArriveDistance))
                        {
                            agent.Face(_door.position - agent.transform.position); // looking to the door
                        }
                        break;
                    case CustomerState.Leaving when outside:
                        // Turned away (or out of patience) before coming in: off they go.
                        _service.Left(customer);
                        break;
                    case CustomerState.WalkingToSeat:
                        var toSeat = Walk(agent, SeatSpot(customer.Seat), stride);
                        if (toSeat == WalkResult.Arrived)
                            _service.SitDown(customer);
                        else if (toSeat == WalkResult.NoWay)
                            _service.GiveUp(customer);
                        break;
                    case CustomerState.Queueing:
                        Walk(agent, QueueSpot(customer), stride);
                        break;
                    case CustomerState.Waiting:
                    case CustomerState.Eating:
                    case CustomerState.DoneEating:
                        // Sitting; pushed off their seat, they go along with it like a hire does, and
                        // go back a moment after nobody's pressing against them. Walking back while
                        // pressed would be an equal push against whoever's squeezing by: neither moves.
                        if (IsPressed(agent))
                            break;
                        if (Walk(agent, SeatSpot(customer.Seat), stride) != WalkResult.Walking && StationAt(customer.Seat) != null)
                            agent.Face(StationAt(customer.Seat).transform.position - agent.transform.position);
                        break;
                    case CustomerState.Leaving:
                        // Each guest on their way out counts towards a scuff on the floor (Phase 5).
                        if (_walkedOut.Add(customer))
                            _root.Services.Messes?.GuestWalked(Vector3.Lerp(agent.transform.position, _door.position, 0.35f));
                        // Even with the door blocked they always get out, so a service can finish.
                        if (Walk(agent, _door.position, stride) == WalkResult.NoWay)
                            agent.WalkTowards(_door.position, stride, ArriveDistance);
                        if ((Floor(agent.transform.position) - Floor(_door.position)).Length() <= DoorReach)
                            _service.Left(customer);
                        break;
                    case CustomerState.Gone:
                        _gone.Add(customer);
                        break;
                }
            }

            foreach (var customer in _gone)
            {
                var agent = _agents[customer];
                _walks.Remove(agent);
                _backToSeatAt.Remove(agent);
                _progress.Remove(agent);
                _streetWalks.Remove(agent);
                _agents.Remove(customer);
                if (HasStreet)
                    Depart(agent, _outside.Remove(agent));
                else
                    Destroy(agent.gameObject);
            }

            // Out of the restaurant's hands: walking off along the street, gone at its end.
            for (var i = _departing.Count - 1; i >= 0; i--)
            {
                var (agent, walk) = _departing[i];
                if (agent == null || FollowStreet(agent, walk, step * agent.SpeedFactor))
                {
                    if (agent != null)
                        Destroy(agent.gameObject);
                    _departing.RemoveAt(i);
                }
            }
        }

        // Off along the street to the far end from where they stand (or a random one in the middle).
        private void Depart(CustomerAgent agent, bool alreadyOutside)
        {
            agent.Slide = null;
            agent.Intent = SimVector2.Zero;
            var walk = new StreetWalk { Since = Time.time };
            if (!alreadyOutside)
                walk.Points.Add(Doorstep());
            var lane = LanePoint(alreadyOutside ? agent.transform.position : Doorstep());
            walk.Points.Add(lane);
            walk.Points.Add(_streetEnds[_streetRandom.Next(2)].position);
            _departing.Add((agent, walk));
        }

        // A step along a street walk; true once at its last point.
        private static bool FollowStreet(CustomerAgent agent, StreetWalk walk, float step)
        {
            while (walk.Next < walk.Points.Count)
            {
                var before = agent.transform.position;
                var arrived = agent.WalkTowards(walk.Points[walk.Next], step, ArriveDistance);
                var moved = agent.transform.position - before;
                agent.Intent = new SimVector2(moved.x, moved.z);
                if (!arrived)
                    return false;
                walk.Next++;
                step -= moved.magnitude;
                if (step <= 1e-4f)
                    return walk.Next >= walk.Points.Count;
            }
            return true;
        }

        // Just outside the door, where people step in and out of the street.
        private Vector3 Doorstep() => _door.position - _door.forward * _doorstep;

        // The nearest point on the way along the pavement (the line between the street's ends).
        private Vector3 LanePoint(Vector3 position)
        {
            var a = _streetEnds[0].position;
            var b = _streetEnds[1].position;
            var along = b - a;
            var t = Mathf.Clamp01(Vector3.Dot(position - a, along) / Mathf.Max(along.sqrMagnitude, 1e-4f));
            var point = a + along * t;
            point.y = position.y;
            return point;
        }

        // A place in the queue outside: along the front wall from beside the doorstep, on the side
        // with more wall, so the line never stands in the doorway or the way out to the market.
        private Vector3 StreetQueueSpot(Customer customer)
        {
            var place = 0;
            foreach (var queued in _service.Queue)
            {
                if (queued == customer)
                    break;
                place++;
            }
            var side = _door.right;
            if (WallLeft(-side) > WallLeft(side))
                side = -side;
            return Doorstep() + side * (_queueStart + _queueSpacing * place);
        }

        // How far the room goes from the door in a direction along the wall, in metres.
        private float WallLeft(Vector3 direction)
        {
            var room = _root.Services.FloorPlan;
            var here = _door.position;
            var x = direction.x > 1e-3f ? (room.RoomMax.X - here.x) / direction.x
                : direction.x < -1e-3f ? (room.RoomMin.X - here.x) / direction.x : float.MaxValue;
            var z = direction.z > 1e-3f ? (room.RoomMax.Y - here.z) / direction.z
                : direction.z < -1e-3f ? (room.RoomMin.Y - here.z) / direction.z : float.MaxValue;
            return Mathf.Min(x, z);
        }

        // One step of the way to a target, around the furniture and giving way to people. Someone
        // who can't get past for a while walks through, so a crowded doorway can't freeze a service.
        private WalkResult Walk(CustomerAgent agent, Vector3 target, float step)
        {
            var here = Floor(agent.transform.position);
            var goal = Floor(target);
            if ((goal - here).Length() <= ArriveDistance)
                return WalkResult.Arrived;

            if (!_walks.TryGetValue(agent, out var walk) || (walk.Goal - goal).Length() > ArriveDistance)
            {
                // The furniture decides whether there's a way at all. Given one, it goes around the
                // chairs if there's room, so nobody walks into a friend already sitting.
                var way = _layout.FindPath(here, goal);
                walk = new CustomerWalk { Goal = goal, Points = way != null ? _routes.FindPath(here, goal) ?? way : null };
                _walks[agent] = walk;
            }
            if (walk.Points == null)
                return WalkResult.NoWay;

            var point = walk.Points[walk.Next];
            CollectOthers(agent);

            // Blocked by people, they stop giving way and squeeze past for a moment, with the push
            // rules moving whoever's in the way. Pinned by furniture as well, they walk through, so a
            // crowded corner can't freeze a service.
            var progress = ProgressOf(agent, here);
            var squeezing = Time.time < progress.SqueezingUntil;
            var next = Time.time < progress.WalkingThroughUntil
                ? here + SimVector2.Normalize(point - here) * Mathf.Min(step, (point - here).Length())
                : Steering.Step(here, point, step, squeezing ? (IReadOnlyList<SimVector2>)System.Array.Empty<SimVector2>() : _others, _layout.IsFree);

            agent.Intent = next - here;
            agent.Face(new Vector3(next.X - here.X, 0f, next.Y - here.Y));
            agent.transform.position = new Vector3(next.X, agent.transform.position.y, next.Y);

            if ((point - next).Length() <= ArriveDistance)
            {
                walk.Next++;
                if (walk.Next >= walk.Points.Count)
                {
                    _walks.Remove(agent);
                    return WalkResult.Arrived;
                }
                return WalkResult.Walking;
            }

            // Off course, round someone or pushed: find the way again, next step.
            if (!squeezing && !_layout.IsClear(next, point))
                _walks.Remove(agent);
            return WalkResult.Walking;
        }

        // Whether they're getting anywhere, measured over a stretch of time rather than frame by frame:
        // someone blocked still shuffles sideways, and the route gets rebuilt as they do, so neither
        // moving a little nor having a route says they're making progress.
        private Progress ProgressOf(CustomerAgent agent, SimVector2 here)
        {
            if (!_progress.TryGetValue(agent, out var progress))
            {
                progress = new Progress { From = here, Since = Time.time };
                _progress[agent] = progress;
                return progress;
            }

            if (Time.time - progress.Since < _stuckSeconds)
                return progress;

            var covered = (here - progress.From).Length();
            if (Time.time < progress.SqueezingUntil)
            {
                // Pushing someone along is slow, so only not moving at all counts: that's furniture.
                if (covered < _pinnedDistance)
                    progress.WalkingThroughUntil = Time.time + _squeezeSeconds;
            }
            else if (covered < _stuckDistance)
            {
                progress.SqueezingUntil = Time.time + _squeezeSeconds;
            }
            progress.From = here;
            progress.Since = Time.time;
            return progress;
        }

        private bool IsPressed(CustomerAgent agent)
        {
            CollectOthers(agent);
            var here = Floor(agent.transform.position);
            foreach (var other in _others)
            {
                if ((other - here).Length() < Steering.BodyDistance + _contactMargin)
                {
                    _backToSeatAt[agent] = Time.time + _returnDelay;
                    return true;
                }
            }
            return _backToSeatAt.TryGetValue(agent, out var at) && Time.time < at;
        }

        private void CollectOthers(CustomerAgent agent)
        {
            _others.Clear();
            foreach (var other in _agents.Values)
            {
                if (other != agent)
                    _others.Add(Floor(other.transform.position));
            }
            foreach (var staff in _staff.Agents)
                _others.Add(Floor(staff.transform.position));
            foreach (var player in PlayerInput.all)
                _others.Add(Floor(player.transform.position));
        }

        private static SimVector2 Floor(Vector3 position) => new SimVector2(position.x, position.z);

        private ServingStation StationAt(int seat) =>
            _serving.TryGetValue(seat, out var station) ? station : null;

        private Vector3 SeatSpot(int seat)
        {
            var station = StationAt(seat);
            return station != null ? station.SeatPosition(seat) : _door.position;
        }

        private Vector3 QueueSpot(Customer customer)
        {
            var place = 0;
            foreach (var queued in _service.Queue)
            {
                if (queued == customer)
                    break;
                place++;
            }
            if (_queueSpots.Length == 0)
                return _door.position;

            // The spots move with the door. Near a corner they'd be outside, so they swap sides.
            var spot = _queueSpots[Mathf.Min(place, _queueSpots.Length - 1)];
            var room = _root.Services.FloorPlan;
            var position = spot.position;
            var inside = position.x >= room.RoomMin.X && position.x <= room.RoomMax.X && position.z >= room.RoomMin.Y && position.z <= room.RoomMax.Y;
            if (inside)
                return position;
            var local = spot.localPosition;
            return _door.TransformPoint(new Vector3(-local.x, local.y, local.z));
        }
    }
}
