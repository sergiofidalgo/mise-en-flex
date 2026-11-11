using System.Collections.Generic;
using Game.Gameplay;
using Game.Simulation.Street;
using UnityEngine;
using UnityEngine.InputSystem;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Presentation.Street
{
    /// <summary>
    /// People walking past the restaurant (the user's request, 2026-11-11: the street should have
    /// the city's life, "people roaming around"). They're the customers' own archetypes, each
    /// rolled anew every time one comes round, walking the pavement either side of the lane guests
    /// arrive by. They have no bodies to bump into: they step aside for chefs, guests and staff
    /// instead (<see cref="StreetWalkers"/>), so nobody carrying a plate is ever held up by the
    /// scenery. Some stop at the menu board. Cosmetic only.
    /// </summary>
    public sealed class StreetWalkersView : MonoBehaviour
    {
        [Tooltip("A person with a CharacterLookPicker and no agent: PF_Passerby.")]
        [SerializeField] private GameObject _prefab;
        [SerializeField, Range(0, 16)] private int _count = 6;
        [SerializeField] private int _seed = 2611_11;

        [Header("The pavement")]
        [Tooltip("Where they come in and leave (X), beyond the camera's view.")]
        [SerializeField] private float _west = -18f;
        [SerializeField] private float _east = 14f;
        [Tooltip("Each lane's Z. Even lanes walk east, odd ones west.")]
        [SerializeField] private float[] _lanes = { -8.35f, -9.85f };
        [Tooltip("The furthest they step, towards the kerb and towards the wall (Z).")]
        [SerializeField] private float _minZ = -10f;
        [SerializeField] private float _maxZ = -8f;

        [Header("Walking")]
        [SerializeField, Min(0.1f)] private float _speedMin = 1f;
        [SerializeField, Min(0.1f)] private float _speedMax = 1.7f;
        [Tooltip("Seconds one stays away before coming round again as someone new. Longer for a quieter street.")]
        [SerializeField, Min(0f)] private float _awayMin = 2f;
        [SerializeField, Min(0f)] private float _awayMax = 16f;
        [Tooltip("Metres: someone this close makes them step aside.")]
        [SerializeField, Min(0f)] private float _avoidDistance = 1.3f;
        [SerializeField, Min(0f)] private float _sideStep = 0.7f;
        [SerializeField, Min(0.1f)] private float _sideStepSpeed = 1.6f;
        [SerializeField, Min(1f)] private float _turnSpeed = 360f;

        [Header("The menu board")]
        [Tooltip("The board they stop at; none, and nobody stops.")]
        [SerializeField] private Transform _board;
        [Tooltip("Where its reader stands.")]
        [SerializeField] private Transform _boardSpot;
        [SerializeField] private int _boardLane;
        [SerializeField, Range(0f, 1f)] private float _readChance = 0.3f;
        [SerializeField, Min(0f)] private float _readMin = 3f;
        [SerializeField, Min(0f)] private float _readMax = 7f;

        // Guests and staff are looked up a few times a second, not every frame.
        private const float PeopleEvery = 0.25f;

        private readonly List<SimVector2> _people = new List<SimVector2>();
        private WalkingAgent[] _agents = System.Array.Empty<WalkingAgent>();
        private float _nextLook;
        private WalkerSettings _settings;
        private StreetWalkers _street;
        private GameObject[] _bodies;
        private CharacterLookPicker[] _pickers;
        private int[] _arrivals;

        private void Awake()
        {
            _settings = new WalkerSettings();
            ApplySettings();
            if (_prefab == null || _lanes.Length == 0)
            {
                enabled = false;
                return;
            }
            _street = new StreetWalkers(_count, _settings, _seed);
            _bodies = new GameObject[_count];
            _pickers = new CharacterLookPicker[_count];
            _arrivals = new int[_count];
            for (var i = 0; i < _count; i++)
            {
                _bodies[i] = Instantiate(_prefab, transform);
                _bodies[i].name = $"Passerby_{i + 1}";
                _pickers[i] = _bodies[i].GetComponent<CharacterLookPicker>();
                _arrivals[i] = -1;
                _bodies[i].SetActive(false);
            }
        }

        // Tunable while playing, in the inspector.
        private void OnValidate()
        {
            if (_settings != null)
                ApplySettings();
        }

        private void ApplySettings()
        {
            _settings.West = _west;
            _settings.East = _east;
            _settings.Lanes = _lanes;
            _settings.MinZ = _minZ;
            _settings.MaxZ = _maxZ;
            _settings.SpeedMin = _speedMin;
            _settings.SpeedMax = Mathf.Max(_speedMin, _speedMax);
            _settings.AwayMin = _awayMin;
            _settings.AwayMax = Mathf.Max(_awayMin, _awayMax);
            _settings.AvoidDistance = _avoidDistance;
            _settings.SideStep = _sideStep;
            _settings.SideStepSpeed = _sideStepSpeed;
            _settings.HasBoard = _board != null && _boardSpot != null;
            if (_settings.HasBoard)
            {
                _settings.Board = new SimVector2(_board.position.x, _board.position.z);
                _settings.BoardSpot = new SimVector2(_boardSpot.position.x, _boardSpot.position.z);
            }
            _settings.BoardLane = _boardLane;
            _settings.ReadChance = _readChance;
            _settings.ReadMin = _readMin;
            _settings.ReadMax = Mathf.Max(_readMin, _readMax);
        }

        private void Update()
        {
            if (Time.time >= _nextLook)
            {
                _nextLook = Time.time + PeopleEvery;
                _agents = FindObjectsByType<WalkingAgent>(FindObjectsSortMode.None);
            }
            _people.Clear();
            foreach (var player in PlayerInput.all)
                _people.Add(new SimVector2(player.transform.position.x, player.transform.position.z));
            foreach (var agent in _agents)
                if (agent != null)
                    _people.Add(new SimVector2(agent.transform.position.x, agent.transform.position.z));
            _street.Tick(Time.deltaTime, _people);

            for (var i = 0; i < _count; i++)
            {
                var walker = _street.Walkers[i];
                var body = _bodies[i];
                if (body.activeSelf != walker.Visible)
                    body.SetActive(walker.Visible);
                if (!walker.Visible)
                    continue;

                var at = new Vector3(walker.Position.X, transform.position.y, walker.Position.Y);
                var facing = Quaternion.LookRotation(new Vector3(walker.Facing.X, 0f, walker.Facing.Y));
                if (_arrivals[i] != walker.Arrivals)
                {
                    // Someone new: another archetype, and (as their look wakes) another roll of it.
                    _arrivals[i] = walker.Arrivals;
                    if (_pickers[i] != null && _pickers[i].Count > 0)
                        _pickers[i].Show(Random.Range(0, _pickers[i].Count));
                    body.transform.SetPositionAndRotation(at, facing);
                    continue;
                }
                body.transform.SetPositionAndRotation(at, Quaternion.RotateTowards(body.transform.rotation, facing, _turnSpeed * Time.deltaTime));
            }
        }
    }
}
