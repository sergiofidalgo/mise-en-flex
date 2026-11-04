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
    /// use the same stations as players (decision 2026-11-04), standing on the kitchen side.
    /// </summary>
    public sealed class StaffDirector : MonoBehaviour
    {
        // Within this distance they've reached where they were going.
        private const float ArriveDistance = 0.05f;

        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private StaffAgent _agentPrefab;
        [SerializeField] private Transform _spawnPoint;

        [Tooltip("Where staff stand relative to a station, on the kitchen side of the counter.")]
        [SerializeField] private Vector3 _standOffset = new Vector3(0f, 0f, 0.85f);

        private readonly List<StaffAgent> _agents = new List<StaffAgent>();
        private PassStation[] _passes;
        private SinkStation _sink;
        private PlateStackStation _stack;
        private int _nextSeed;

        private void Start()
        {
            _passes = FindObjectsByType<PassStation>(FindObjectsSortMode.None).OrderBy(p => p.SeatIndex).ToArray();
            _sink = FindFirstObjectByType<SinkStation>();
            _stack = FindFirstObjectByType<PlateStackStation>();
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
                _agents[i].Job.PutDownCarried();
                Destroy(_agents[i].gameObject);
                _agents.RemoveAt(i);
            }

            foreach (var member in services.Staff.Hired)
            {
                member.StartDay();
                if (_agents.Any(a => a.Member == member))
                    continue;

                var passes = _passes.Select(p => p.DirtyPlates).ToArray();
                var job = new Dishwasher(member, _sink.Sink, _stack.Stack, passes, services.StaffConfig.ToSettings(), services.StaffConfig.Seed + _nextSeed++);
                var agent = Instantiate(_agentPrefab, _spawnPoint.position, _spawnPoint.rotation);
                agent.name = $"Staff_{member.Name}";
                agent.Initialize(member, job);
                _agents.Add(agent);
            }
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

                var target = Target(job);
                if (target != null)
                {
                    var spot = target.transform.position + _standOffset;
                    if (agent.WalkTowards(spot, step, ArriveDistance))
                    {
                        agent.Face(target.transform.position - agent.transform.position);
                        job.Arrived();
                    }
                }

                if (job.Carried != null)
                    services.ItemViews.Place(job.Carried, agent.CarryAnchor, services.Tuning.ItemMove, agent.CarryAnchor.position);
            }
        }

        private Interactable Target(Dishwasher job)
        {
            switch (job.Task)
            {
                case DishwasherTask.ToPass:
                    return _passes[job.TargetPass];
                case DishwasherTask.ToSink:
                    return _sink;
                case DishwasherTask.ToStack:
                    return _stack;
                default:
                    return null;
            }
        }
    }
}
