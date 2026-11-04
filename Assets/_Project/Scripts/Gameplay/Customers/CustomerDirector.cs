using System.Collections.Generic;
using Game.Gameplay.Staff;
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

        private sealed class CustomerWalk
        {
            public SimVector2 Goal;
            public List<SimVector2> Points;
            public int Next;
            public float StuckFor;
        }

        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private StaffDirector _staff;
        [SerializeField] private CustomerAgent _customerPrefab;
        [SerializeField] private Transform _door;

        [Tooltip("Where each stool's customer stands, in seat order (seat i faces pass counter i).")]
        [SerializeField] private Transform[] _seats = System.Array.Empty<Transform>();

        [Tooltip("Spots by the door where customers wait for a stool, nearest the door first.")]
        [SerializeField] private Transform[] _queueSpots = System.Array.Empty<Transform>();

        [Tooltip("Real seconds someone can't get past before they walk through.")]
        [SerializeField, Min(0f)] private float _stuckSeconds = 2f;

        private readonly Dictionary<Customer, CustomerAgent> _agents = new Dictionary<Customer, CustomerAgent>();
        private readonly Dictionary<CustomerAgent, CustomerWalk> _walks = new Dictionary<CustomerAgent, CustomerWalk>();
        private readonly List<SimVector2> _others = new List<SimVector2>();
        private readonly List<Customer> _gone = new List<Customer>();
        private ServiceSimulation _service;
        private StationLayout _layout;

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
            if (phase == DayPhase.Service)
                _layout = _root.Services.NewLayout();
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
                        // Sitting; pushed off their seat, they go back to it.
                        Walk(agent, SeatSpot(customer.Seat), step);
                        agent.Face(Vector3.forward);
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
                walk = new CustomerWalk { Goal = goal, Points = _layout.FindPath(here, goal) };
                _walks[agent] = walk;
            }
            if (walk.Points == null)
                return WalkResult.NoWay;

            var point = walk.Points[walk.Next];
            CollectOthers(agent);
            var next = Steering.Step(here, point, step, _others, _layout.IsFree);

            walk.StuckFor = (next - here).Length() < step * 0.1f ? walk.StuckFor + Time.deltaTime : 0f;
            if (walk.StuckFor > _stuckSeconds)
                next = here + SimVector2.Normalize(point - here) * Mathf.Min(step, (point - here).Length());

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
            if (walk.StuckFor <= _stuckSeconds && !_layout.IsClear(next, point))
                _walks.Remove(agent);
            return WalkResult.Walking;
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

        private Vector3 SeatSpot(int seat) =>
            seat >= 0 && seat < _seats.Length ? _seats[seat].position : _door.position;

        private Vector3 QueueSpot(Customer customer)
        {
            var place = 0;
            foreach (var queued in _service.Queue)
            {
                if (queued == customer)
                    break;
                place++;
            }
            return _queueSpots.Length > 0 ? _queueSpots[Mathf.Min(place, _queueSpots.Length - 1)].position : _door.position;
        }
    }
}
