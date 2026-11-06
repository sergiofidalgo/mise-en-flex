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

        private readonly Dictionary<Customer, CustomerAgent> _agents = new Dictionary<Customer, CustomerAgent>();
        private readonly Dictionary<CustomerAgent, CustomerWalk> _walks = new Dictionary<CustomerAgent, CustomerWalk>();
        private readonly Dictionary<CustomerAgent, Progress> _progress = new Dictionary<CustomerAgent, Progress>();
        private readonly Dictionary<CustomerAgent, float> _backToSeatAt = new Dictionary<CustomerAgent, float>();
        private readonly List<SimVector2> _others = new List<SimVector2>();
        private readonly List<Customer> _gone = new List<Customer>();
        private ServiceSimulation _service;
        private StationLayout _layout;

        // The same, keeping clear of every chair: for planning a way, not for where they can step.
        private StationLayout _routes;
        private readonly Dictionary<int, ServingStation> _serving = new Dictionary<int, ServingStation>();

        /// <summary>Every customer in the scene and their body, for HUD labels.</summary>
        public IReadOnlyDictionary<Customer, CustomerAgent> Agents => _agents;

        private void Start()
        {
            _service = _root.Services.Service;
            _service.Arrived += HandleArrived;
            _root.Services.ServiceReplaced += HandleServiceReplaced;
            _root.Services.Days.PhaseChanged += HandlePhaseChanged;
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
                _root.Services.ServiceReplaced -= HandleServiceReplaced;
                _root.Services.Days.PhaseChanged -= HandlePhaseChanged;
            }
        }

        private void HandleServiceReplaced(ServiceSimulation old, ServiceSimulation next)
        {
            old.Arrived -= HandleArrived;
            next.Arrived += HandleArrived;
            _service = next;
        }

        private void HandleArrived(Customer customer)
        {
            var agent = Instantiate(_customerPrefab, _door.position, _door.rotation);
            agent.name = $"Customer_{customer.Id}";
            agent.Initialize(customer);
            agent.Slide = _layout.Slide;
            _agents[customer] = agent;
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
                agent.Intent = SimVector2.Zero;
                switch (customer.State)
                {
                    case CustomerState.WalkingToSeat:
                        var toSeat = Walk(agent, SeatSpot(customer.Seat), step);
                        if (toSeat == WalkResult.Arrived)
                            _service.SitDown(customer);
                        else if (toSeat == WalkResult.NoWay)
                            _service.GiveUp(customer);
                        break;
                    case CustomerState.Queueing:
                        Walk(agent, QueueSpot(customer), step);
                        break;
                    case CustomerState.Waiting:
                    case CustomerState.Eating:
                    case CustomerState.DoneEating:
                        // Sitting; pushed off their seat, they go along with it like a hire does, and
                        // go back a moment after nobody's pressing against them. Walking back while
                        // pressed would be an equal push against whoever's squeezing by: neither moves.
                        if (IsPressed(agent))
                            break;
                        if (Walk(agent, SeatSpot(customer.Seat), step) != WalkResult.Walking && StationAt(customer.Seat) != null)
                            agent.Face(StationAt(customer.Seat).transform.position - agent.transform.position);
                        break;
                    case CustomerState.Leaving:
                        // Even with the door blocked they always get out, so a service can finish.
                        if (Walk(agent, _door.position, step) == WalkResult.NoWay)
                            agent.WalkTowards(_door.position, step, ArriveDistance);
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
                _walks.Remove(_agents[customer]);
                _backToSeatAt.Remove(_agents[customer]);
                _progress.Remove(_agents[customer]);
                Destroy(_agents[customer].gameObject);
                _agents.Remove(customer);
            }
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
