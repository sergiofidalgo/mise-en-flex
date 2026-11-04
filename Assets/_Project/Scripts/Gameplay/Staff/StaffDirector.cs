using System.Collections.Generic;
using System.Linq;
using Game.Gameplay.Stations;
using Game.Simulation.Days;
using Game.Simulation.Items;
using Game.Simulation.Movement;
using Game.Simulation.Staff;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Staff
{
    /// <summary>
    /// Puts the hired staff in the kitchen each morning and runs them while the kitchen is live:
    /// walking them around the furniture to where their job says, showing what they carry and
    /// reporting arrivals. Staff use the same stations as players (decision 2026-11-04). Pushed away
    /// from what they're doing, they stop, walk back when there's space, and carry on.
    /// </summary>
    public sealed class StaffDirector : MonoBehaviour
    {
        // Within this distance they've reached where they were going.
        private const float ArriveDistance = 0.05f;

        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private StaffAgent _agentPrefab;
        [SerializeField] private Transform _spawnPoint;

        [Tooltip("How far from a station's centre staff stand.")]
        [SerializeField, Min(0f)] private float _standDistance = 0.85f;
        [Tooltip("Half a station's width, for walking around it.")]
        [SerializeField, Min(0f)] private float _stationHalfSize = 0.5f;
        [Tooltip("How much room a walking staff member takes.")]
        [SerializeField, Min(0f)] private float _walkerRadius = 0.3f;
        [Tooltip("Metres from a spot that counts as there, when someone's already standing on it.")]
        [SerializeField, Min(0f)] private float _shareSpotDistance = 0.8f;
        [Tooltip("Metres off their spot that counts as being pushed away from what they're doing.")]
        [SerializeField, Min(0f)] private float _pushedAwayDistance = 0.15f;
        [Tooltip("Metres between staff arriving, so nobody starts inside anyone else.")]
        [SerializeField, Min(0f)] private float _arrivalSpacing = 0.9f;
        [Tooltip("Half the room's width and depth: staff stay inside.")]
        [SerializeField] private Vector2 _roomHalfSize = new Vector2(4.6f, 3.6f);

        private readonly List<StaffAgent> _agents = new List<StaffAgent>();
        private readonly Dictionary<object, Interactable> _stations = new Dictionary<object, Interactable>();
        private readonly Dictionary<StaffAgent, Route> _routes = new Dictionary<StaffAgent, Route>();
        private readonly List<System.Numerics.Vector2> _others = new List<System.Numerics.Vector2>();
        private StationLayout _layout;

        /// <summary>Everyone currently working in the kitchen.</summary>
        public IReadOnlyList<StaffAgent> Agents => _agents;

        private Interactable[] _allStations;
        private PassStation[] _passes;
        private SinkStation _sink;
        private PlateStackStation _stack;
        private PrepStation _board;
        private RackStation _trayRack;
        private CounterStation[] _counters;
        private StorageStation[] _storage;
        private int _nextSeed;

        private void Start()
        {
            _allStations = FindObjectsByType<Interactable>(FindObjectsSortMode.None);
            _passes = _allStations.OfType<PassStation>().OrderBy(p => p.SeatIndex).ToArray();
            _sink = _allStations.OfType<SinkStation>().First();
            _stack = _allStations.OfType<PlateStackStation>().First();
            _board = _allStations.OfType<PrepStation>().First();
            _trayRack = _allStations.OfType<RackStation>().First(r => r.Kind == RackStation.RackKind.Trays);
            _counters = _allStations.OfType<CounterStation>().ToArray();
            _storage = _allStations.OfType<StorageStation>().ToArray();
            _layout = new StationLayout(
                _allStations.Select(station => Floor(station.transform.position)),
                new System.Numerics.Vector2(_roomHalfSize.x, _roomHalfSize.y), _stationHalfSize, _walkerRadius, _standDistance);

            foreach (var pass in _passes)
                _stations[pass.DirtyPlates] = pass;
            _stations[_sink.Sink] = _sink;
            _stations[_stack.Stack] = _stack;
            _stations[_board.Counter] = _board;
            _stations[_trayRack.Rack] = _trayRack;
            foreach (var counter in _counters)
                _stations[counter.Slot] = counter;
            foreach (var unit in _storage)
                _stations[unit.Unit] = unit;
            var bin = _allStations.OfType<BinStation>().First();
            _stations[bin.Sink] = bin;

            _root.Services.Days.PhaseChanged += HandlePhaseChanged;
        }

        private void OnDestroy()
        {
            if (_root != null && _root.Services != null)
                _root.Services.Days.PhaseChanged -= HandlePhaseChanged;
        }

        private void HandlePhaseChanged(DayPhase phase)
        {
            if (phase == DayPhase.Prep)
                StartShift();
        }

        // Matches the people in the kitchen to the roster: the let go leave, the newly hired arrive.
        private void StartShift()
        {
            var services = _root.Services;
            for (var i = _agents.Count - 1; i >= 0; i--)
            {
                if (services.Staff.Hired.Contains(_agents[i].Member))
                    continue;
                _agents[i].Job.Release();
                services.ItemViews.ReleaseMissing(services.Cooking);
                _routes.Remove(_agents[i]);
                Destroy(_agents[i].gameObject);
                _agents.RemoveAt(i);
            }

            foreach (var member in services.Staff.Hired)
            {
                member.StartDay();
                if (_agents.Any(a => a.Member == member))
                    continue;

                var job = CreateJob(member);
                job.ItemGone += HandleItemGone;
                job.FoodChanged += HandleFoodChanged;
                var agent = Instantiate(_agentPrefab, FreeArrivalSpot(), _spawnPoint.rotation);
                agent.IsFree = _layout.IsFree;
                agent.name = $"Staff_{member.Name}";
                agent.Initialize(member, job);
                _agents.Add(agent);
            }
        }

        private bool IsInterrupted(StaffAgent agent, IStaffJob job) =>
            job.Target != null && _routes.TryGetValue(agent, out var route) && route.Target == job.Target && route.Interrupted;

        // The arrival point, or a step along from it if someone's already there.
        private Vector3 FreeArrivalSpot()
        {
            for (var i = 0; i < 8; i++)
            {
                var spot = _spawnPoint.position + Vector3.left * (_arrivalSpacing * i);
                if (_layout.IsFree(Floor(spot)) && _agents.All(a => (a.transform.position - spot).magnitude >= _arrivalSpacing))
                    return spot;
            }
            return _spawnPoint.position;
        }

        private IStaffJob CreateJob(StaffMember member)
        {
            var services = _root.Services;
            var settings = services.StaffConfig.ToSettings();
            var seed = services.StaffConfig.Seed + _nextSeed++;
            if (member.Role == StaffRole.PrepAssistant)
            {
                return new PrepAssistant(
                    member,
                    _counters.Select(c => c.Slot).ToArray(),
                    _storage.Select(s => s.Unit).ToArray(),
                    _board.Counter,
                    _trayRack.Rack,
                    _allStations.OfType<BinStation>().First().Sink,
                    services.Cooking,
                    settings,
                    seed);
            }
            return new Dishwasher(member, _sink.Sink, _stack.Stack, _passes.Select(p => p.DirtyPlates).ToArray(), settings, seed);
        }

        private void HandleItemGone(Item item) => _root.Services.ItemViews.Release(item);

        private void HandleFoodChanged()
        {
            var services = _root.Services;
            services.ItemViews.ReleaseMissing(services.Cooking);
            if (_board.Counter.Current != null)
                services.ItemViews.Place(_board.Counter.Current, _board.AnchorFor(_board.Counter.Current), services.Tuning.ItemMove, _board.ItemAnchor.position);
        }

        private void Update()
        {
            var services = _root.Services;
            var phase = services.Days.Phase;
            if (phase != DayPhase.Prep && phase != DayPhase.Service)
                return;

            var gameSeconds = Time.deltaTime * services.Tuning.TimeScale;
            var step = services.StaffConfig.WalkSpeed * Time.deltaTime;
            foreach (var agent in _agents)
            {
                var job = agent.Job;
                agent.Intent = System.Numerics.Vector2.Zero;

                // Pushed away from the board, the sink...: the work waits until they're back.
                if (!IsInterrupted(agent, job))
                    job.Tick(gameSeconds);

                if (job.Target != null && _stations.TryGetValue(job.Target, out var station))
                {
                    if (!_routes.TryGetValue(agent, out var route) || route.Target != job.Target)
                    {
                        route = new Route { Target = job.Target, Points = PlanRoute(agent, station) };
                        _routes[agent] = route;
                    }

                    if (WalkRoute(agent, route, station, step))
                    {
                        agent.Face(station.transform.position - agent.transform.position);
                        job.Arrived();

                        // What they just put down there.
                        var onStation = station.HeldItem;
                        if (onStation != null)
                            services.ItemViews.Place(onStation, station.AnchorFor(onStation), services.Tuning.ItemMove, agent.CarryAnchor.position);
                    }
                }

                if (job.Carried != null)
                    services.ItemViews.Place(job.Carried, agent.CarryAnchor, services.Tuning.ItemMove, agent.CarryAnchor.position);
            }
        }

        // Walks along the route to the job's target, giving way to other people, and works a new one
        // out when the target changes or they've been moved off it. True once they've arrived.
        private bool WalkRoute(StaffAgent agent, Route route, Interactable station, float step)
        {
            var here = Floor(agent.transform.position);

            // Pushed off the spot they were working from: they stop and go back.
            if (route.Next >= route.Points.Count)
            {
                if (route.Points.Count == 0 || (route.Points[route.Points.Count - 1] - here).Length() <= _pushedAwayDistance)
                    return true;
                route.Interrupted = true;
                route.Points = PlanRoute(agent, station);
                route.Next = 0;
                if (route.Points.Count == 0)
                    return false;
            }

            var point = route.Points[route.Next];
            CollectOthers(agent);

            // Someone's standing where they need to be. Starting a job, close enough will do.
            // Going back to one they were pushed from, they wait for the space.
            if (route.Next == route.Points.Count - 1 && (point - here).Length() <= _shareSpotDistance && SomeoneAt(point))
            {
                if (route.Interrupted)
                    return false;
                route.Points[route.Next] = here;
                route.Next = route.Points.Count;
                return true;
            }

            var next = Steering.Step(here, point, step, _others, _layout.IsFree);
            agent.Intent = next - here;
            agent.Face(new Vector3(next.X - here.X, 0f, next.Y - here.Y));
            agent.transform.position = new Vector3(next.X, agent.transform.position.y, next.Y);

            if ((point - next).Length() <= ArriveDistance)
            {
                route.Next++;
                if (route.Next < route.Points.Count)
                    return false;
                route.Interrupted = false;
                return true;
            }

            // Off course, round someone or pushed: find the way again.
            if (!_layout.IsClear(next, point))
            {
                route.Points = PlanRoute(agent, station);
                route.Next = 0;
            }
            return false;
        }

        private void CollectOthers(StaffAgent agent)
        {
            _others.Clear();
            foreach (var other in _agents)
            {
                if (other != agent)
                    _others.Add(Floor(other.transform.position));
            }
            foreach (var player in PlayerInput.all)
                _others.Add(Floor(player.transform.position));
        }

        private bool SomeoneAt(System.Numerics.Vector2 point)
        {
            foreach (var other in _others)
            {
                if ((other - point).Length() < Steering.BodyDistance)
                    return true;
            }
            return false;
        }

        private List<System.Numerics.Vector2> PlanRoute(StaffAgent agent, Interactable station)
        {
            var here = Floor(agent.transform.position);
            var spot = _layout.StandingSpot(Floor(station.transform.position));
            var route = spot.HasValue ? _layout.FindPath(here, spot.Value) : null;
            if (route != null)
                return route;

            Debug.LogWarning($"[StaffDirector] {agent.name} has no way to reach {station.name}.");
            return new List<System.Numerics.Vector2>();
        }

        private static System.Numerics.Vector2 Floor(Vector3 position) => new System.Numerics.Vector2(position.x, position.z);

        private sealed class Route
        {
            public object Target;
            public List<System.Numerics.Vector2> Points;
            public int Next;

            // Pushed away from what they were doing: the job waits until they're back.
            public bool Interrupted;
        }
    }
}
