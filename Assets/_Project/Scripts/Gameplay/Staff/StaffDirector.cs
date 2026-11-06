using System.Collections.Generic;
using System.Linq;
using Game.Gameplay.Stations;
using Game.Simulation.Days;
using Game.Simulation.Items;
using Game.Simulation.Movement;
using Game.Simulation.Staff;
using Game.Simulation.Storage;
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
        [SerializeField] private LayoutEditor _editor;

        [Tooltip("Metres from a spot that counts as there, when someone's already standing on it.")]
        [SerializeField, Min(0f)] private float _shareSpotDistance = 0.8f;
        [Tooltip("Metres off their spot that counts as being pushed away from what they're doing.")]
        [SerializeField, Min(0f)] private float _pushedAwayDistance = 0.15f;
        [Tooltip("Metres beyond touching that still counts as someone pressing against them.")]
        [SerializeField, Min(0f)] private float _contactMargin = 0.2f;
        [Tooltip("Real seconds after being pushed before they start back to their work.")]
        [SerializeField, Min(0f)] private float _returnDelay = 0.8f;
        [Tooltip("Seconds between looks for a way to a station they can't reach.")]
        [SerializeField, Min(0.1f)] private float _noWayRetrySeconds = 1f;
        [Tooltip("Metres between staff arriving, so nobody starts inside anyone else.")]
        [SerializeField, Min(0f)] private float _arrivalSpacing = 0.9f;

        private readonly List<StaffAgent> _agents = new List<StaffAgent>();
        private readonly Dictionary<object, Interactable> _stations = new Dictionary<object, Interactable>();
        private readonly Dictionary<StaffAgent, Route> _routes = new Dictionary<StaffAgent, Route>();
        private readonly List<System.Numerics.Vector2> _others = new List<System.Numerics.Vector2>();
        private StationLayout _layout;

        /// <summary>Everyone currently working in the kitchen.</summary>
        public IReadOnlyList<StaffAgent> Agents => _agents;

        // Dirty plates a dishwasher can collect from the kitchen: at tables against the wall.
        private readonly List<IDirtyPlateSource> _clearable = new List<IDirtyPlateSource>();
        private SinkStation _sink;
        private PlateStackStation _stack;
        private DirtyPileStation _dirtyPile;
        private PrepStation _board;
        private RackStation _trayRack;
        private readonly List<ItemSlot> _counters = new List<ItemSlot>();
        private readonly List<StorageUnit> _storage = new List<StorageUnit>();
        private int _nextSeed;

        private void Start()
        {
            var all = _root.Services.Stations.All;
            _sink = all.OfType<SinkStation>().First();
            _stack = all.OfType<PlateStackStation>().First();
            _dirtyPile = all.OfType<DirtyPileStation>().First();
            _board = all.OfType<PrepStation>().First();
            _trayRack = all.OfType<RackStation>().First(r => r.Kind == RackStation.RackKind.Trays);
            RebuildLayout();

            _stations[_sink.Sink] = _sink;
            _stations[_stack.Stack] = _stack;
            _stations[_dirtyPile.Pile] = _dirtyPile;
            _stations[_board.Counter] = _board;
            _stations[_trayRack.Rack] = _trayRack;
            foreach (var station in all)
                HandleStationAdded(station);
            var bin = all.OfType<BinStation>().First();
            _stations[bin.Sink] = bin;

            _root.Services.Stations.Added += HandleStationAdded;
            _root.Services.Stations.Removed += HandleStationRemoved;
            _root.Services.Days.PhaseChanged += HandlePhaseChanged;
            if (_editor != null)
                _editor.LayoutChanged += RebuildLayout;
        }

        // Bought furniture joins the places they can work from, and sold furniture leaves them.
        private void HandleStationAdded(Interactable station)
        {
            if (station is CounterStation counter)
            {
                _counters.Add(counter.Slot);
                _stations[counter.Slot] = counter;
            }
            else if (station is StorageStation storage)
            {
                _storage.Add(storage.Unit);
                _stations[storage.Unit] = storage;
            }
        }

        private void HandleStationRemoved(Interactable station)
        {
            if (station is CounterStation counter)
            {
                _counters.Remove(counter.Slot);
                _stations.Remove(counter.Slot);
            }
            else if (station is StorageStation storage)
            {
                _storage.Remove(storage.Unit);
                _stations.Remove(storage.Unit);
            }
        }

        /// <summary>True if a helper is working at this station or on their way to it.</summary>
        public bool IsHeadedTo(Interactable station)
        {
            foreach (var agent in _agents)
            {
                var target = agent.Job.Target;
                if (target != null && _stations.TryGetValue(target, out var there) && there == station)
                    return true;
            }
            return false;
        }

        // The kitchen changed: everyone works out their way again.
        private void RebuildLayout()
        {
            _layout = _root.Services.NewLayout();

            _clearable.Clear();
            foreach (var table in _root.Services.Stations.All.OfType<ServingStation>())
            {
                if (!_root.Services.Building.TouchesAWall(table.Footprint))
                    continue;
                foreach (var source in table.DirtySources)
                {
                    _clearable.Add(source);
                    _stations[source] = table;
                }
            }
            _routes.Clear();
            foreach (var agent in _agents)
                agent.Slide = _layout.Slide;
        }

        private void OnDestroy()
        {
            if (_root != null && _root.Services != null)
            {
                _root.Services.Days.PhaseChanged -= HandlePhaseChanged;
                _root.Services.Stations.Added -= HandleStationAdded;
                _root.Services.Stations.Removed -= HandleStationRemoved;
            }
            if (_editor != null)
                _editor.LayoutChanged -= RebuildLayout;
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
                agent.Slide = _layout.Slide;
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
                    _counters,
                    _storage,
                    _board.Counter,
                    _trayRack.Rack,
                    services.Stations.All.OfType<BinStation>().First().Sink,
                    services.Cooking,
                    settings,
                    seed);
            }
            return new Dishwasher(member, _sink.Sink, _dirtyPile.Pile, _stack.Stack, _clearable, settings, seed);
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

            // The layout has shut them out: they stand and wait, and the players see why.
            if (route.Points == null)
            {
                if (Time.time < route.RetryAt)
                    return false;
                route.Points = PlanRoute(agent, station);
                route.Next = 0;
                route.RetryAt = Time.time + _noWayRetrySeconds;
                if (route.Points == null)
                    return false;
            }

            // Pushed off the spot they were working from: they stop and go back.
            if (route.Next >= route.Points.Count)
            {
                if ((route.Points[route.Points.Count - 1] - here).Length() <= _pushedAwayDistance)
                    return true;
                route.Interrupted = true;
                route.Points = PlanRoute(agent, station);
                route.Next = 0;
                if (route.Points == null)
                    return false;
            }

            var point = route.Points[route.Next];
            CollectOthers(agent);

            // Being pushed: they go along with it, like anyone standing still, and only head back
            // a moment after nobody's pressing against them.
            if (route.Interrupted)
            {
                if (SomeoneAt(here, Steering.BodyDistance + _contactMargin))
                    route.ReturnAt = Time.time + _returnDelay;
                if (Time.time < route.ReturnAt)
                    return false;
            }

            // Someone's standing where they need to be. Starting a job, close enough will do.
            // Going back to one they were pushed from, they wait for the space.
            if (route.Next == route.Points.Count - 1 && (point - here).Length() <= _shareSpotDistance && SomeoneAt(point, Steering.BodyDistance))
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

        private bool SomeoneAt(System.Numerics.Vector2 point, float within)
        {
            foreach (var other in _others)
            {
                if ((other - point).Length() < within)
                    return true;
            }
            return false;
        }

        private List<System.Numerics.Vector2> PlanRoute(StaffAgent agent, Interactable station)
        {
            var here = Floor(agent.transform.position);
            return _layout.FindPathTo(here, station.Footprint);
        }

        private static System.Numerics.Vector2 Floor(Vector3 position) => new System.Numerics.Vector2(position.x, position.z);

        private sealed class Route
        {
            public object Target;
            public List<System.Numerics.Vector2> Points;
            public int Next;

            // Pushed away from what they were doing: the job waits until they're back.
            public bool Interrupted;

            // When they may start back, once nobody's pressing against them.
            public float ReturnAt;

            // With no way there, when to look for one again.
            public float RetryAt;
        }
    }
}
