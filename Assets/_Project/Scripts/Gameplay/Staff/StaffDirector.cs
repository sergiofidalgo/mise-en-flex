using System.Collections.Generic;
using System.Linq;
using Game.Gameplay.Stations;
using Game.Simulation.Days;
using Game.Simulation.Items;
using Game.Simulation.Staff;
using UnityEngine;

namespace Game.Gameplay.Staff
{
    /// <summary>
    /// Puts the hired staff in the kitchen each morning and runs them while the kitchen is live:
    /// walking them to where their job says, showing what they carry and reporting arrivals. Staff
    /// use the same stations as players (decision 2026-11-04). They take the straight line there:
    /// no pathfinding yet, so they can walk through counters.
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
        [Tooltip("How far from another station's centre a standing spot has to be, along either axis.")]
        [SerializeField, Min(0f)] private float _clearance = 0.75f;
        [Tooltip("Half the room's width and depth: standing spots stay inside.")]
        [SerializeField] private Vector2 _roomHalfSize = new Vector2(4.6f, 3.6f);

        private readonly List<StaffAgent> _agents = new List<StaffAgent>();
        private readonly Dictionary<object, Interactable> _stations = new Dictionary<object, Interactable>();
        private readonly Dictionary<Interactable, Vector3> _spots = new Dictionary<Interactable, Vector3>();
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
                var agent = Instantiate(_agentPrefab, _spawnPoint.position, _spawnPoint.rotation);
                agent.name = $"Staff_{member.Name}";
                agent.Initialize(member, job);
                _agents.Add(agent);
            }
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
                job.Tick(gameSeconds);

                if (job.Target != null && _stations.TryGetValue(job.Target, out var station))
                {
                    if (agent.WalkTowards(SpotFor(station), step, ArriveDistance))
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

        // The side of the station nearest the middle of the room with space to stand.
        private Vector3 SpotFor(Interactable station)
        {
            if (_spots.TryGetValue(station, out var spot))
                return spot;

            var centre = station.transform.position;
            var sides = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right }
                .Select(side => centre + side * _standDistance)
                .OrderBy(point => new Vector2(point.x, point.z).sqrMagnitude);

            spot = centre;
            foreach (var point in sides)
            {
                if (Mathf.Abs(point.x) <= _roomHalfSize.x && Mathf.Abs(point.z) <= _roomHalfSize.y && IsClear(point, station))
                {
                    spot = point;
                    break;
                }
            }
            _spots[station] = spot;
            return spot;
        }

        private bool IsClear(Vector3 point, Interactable except)
        {
            foreach (var other in _allStations)
            {
                if (other == except)
                    continue;
                var offset = other.transform.position - point;
                if (Mathf.Abs(offset.x) < _clearance && Mathf.Abs(offset.z) < _clearance)
                    return false;
            }
            return true;
        }
    }
}
