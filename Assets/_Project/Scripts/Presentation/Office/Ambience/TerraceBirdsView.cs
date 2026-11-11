using System.Collections.Generic;
using Game.Simulation.Office;
using UnityEngine;
using UnityEngine.InputSystem;
using SimVector2 = System.Numerics.Vector2;
using SimVector3 = System.Numerics.Vector3;

namespace Game.Presentation.Office.Ambience
{
    /// <summary>
    /// The pigeons on the boss's terrace fly off when a chef steps close, over the rail and out of
    /// sight, and glide back to their perches a while later (user's request, 2026-11-10; the
    /// coming back is a proposal). The flights are worked out by <see cref="TerraceBirds"/>; this
    /// moves the birds, stops their pecking and flaps their wings while they fly.
    /// </summary>
    public sealed class TerraceBirdsView : MonoBehaviour
    {
        [Tooltip("Each perched bird's root. Its child \"Bird\" pecks, and that has folded-away wings (\"Wing_L\", \"Wing_R\") that flap in flight.")]
        [SerializeField] private Transform[] _birds = new Transform[0];
        [SerializeField] private int _seed = 2611_10;

        [Header("Taking off")]
        [Tooltip("Metres: a chef this close startles a bird.")]
        [SerializeField, Min(0f)] private float _startleDistance = 2.5f;
        [Tooltip("Seconds: the longest a startled bird waits before it goes, each its own, so a group doesn't leave in step.")]
        [SerializeField, Min(0f)] private float _startleDelayMax = 0.2f;
        [Tooltip("Metres: a bird this close to a startled one goes too.")]
        [SerializeField, Min(0f)] private float _flockDistance = 3f;

        [Header("Flying off")]
        [Tooltip("Seconds from the perch to out of sight.")]
        [SerializeField, Min(0.1f)] private float _flyOffSeconds = 2.5f;
        [Tooltip("Metres across the ground it flies.")]
        [SerializeField, Min(0f)] private float _flyOffDistance = 25f;
        [Tooltip("Metres: the arc's rise. With the dip below, it peaks about 5.5 m above the perch.")]
        [SerializeField, Min(0f)] private float _flyOffRise = 7.5f;
        [Tooltip("Metres below the perch it ends, dropping away towards the roofs. Below zero, it ends that far above: for birds on the ground, which have nowhere to drop to.")]
        [SerializeField] private float _flyOffDip = 8f;
        [Tooltip("The way out, across the ground (X, Z): over the rail. Flights lean this way rather than into the office.")]
        [SerializeField] private Vector2 _outward = new Vector2(0f, 1f);
        [Tooltip("How much flights lean the way out, against straight away from the chef.")]
        [SerializeField, Min(0f)] private float _outwardWeight = 1f;
        [Tooltip("Share of the flight, at its end, over which it shrinks away to nothing, so it doesn't vanish all at once.")]
        [SerializeField, Range(0.01f, 1f)] private float _shrinkOver = 0.2f;

        [Header("Coming back (a proposal)")]
        [Tooltip("Seconds: the shortest it stays away. Below zero, it never comes back.")]
        [SerializeField] private float _awayMin = 20f;
        [Tooltip("Seconds: the longest it stays away.")]
        [SerializeField, Min(0f)] private float _awayMax = 40f;
        [Tooltip("Seconds the glide back to its perch takes.")]
        [SerializeField, Min(0.1f)] private float _returnSeconds = 2.5f;
        [Tooltip("Metres: it only lands with no chef this close to its perch. Wider than the startle distance, so it doesn't land and flee at once.")]
        [SerializeField, Min(0f)] private float _landClearDistance = 4f;

        private readonly List<SimVector2> _chefs = new List<SimVector2>();
        private TerraceBirds _flock;
        private BirdSettings _settings;
        private Transform[] _bodies;
        private Oscillate[] _pecks;
        private GameObject[][] _wings;
        private Quaternion[] _bodyRotations;
        private Vector3[] _scales;

        private void Awake()
        {
            _settings = new BirdSettings();
            ApplySettings();
            var perches = new List<SimVector3>();
            var count = _birds.Length;
            _bodies = new Transform[count];
            _pecks = new Oscillate[count];
            _wings = new GameObject[count][];
            _bodyRotations = new Quaternion[count];
            _scales = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                var bird = _birds[i];
                perches.Add(new SimVector3(bird.position.x, bird.position.y, bird.position.z));
                _scales[i] = bird.localScale;
                _bodies[i] = bird.Find("Bird");
                _pecks[i] = _bodies[i] != null ? _bodies[i].GetComponent<Oscillate>() : null;
                _bodyRotations[i] = _bodies[i] != null ? _bodies[i].localRotation : Quaternion.identity;
                var wings = new List<GameObject>();
                foreach (var name in new[] { "Wing_L", "Wing_R" })
                {
                    var wing = _bodies[i] != null ? _bodies[i].Find(name) : null;
                    if (wing != null)
                        wings.Add(wing.gameObject);
                }
                _wings[i] = wings.ToArray();
            }
            _flock = new TerraceBirds(perches, _settings, _seed);
        }

        // Tunable while playing, in the inspector.
        private void OnValidate()
        {
            if (_settings != null)
                ApplySettings();
        }

        private void ApplySettings()
        {
            _settings.StartleDistance = _startleDistance;
            _settings.StartleDelayMax = _startleDelayMax;
            _settings.FlockDistance = _flockDistance;
            _settings.FlyOffSeconds = _flyOffSeconds;
            _settings.FlyOffDistance = _flyOffDistance;
            _settings.FlyOffRise = _flyOffRise;
            _settings.FlyOffDip = _flyOffDip;
            _settings.Outward = new SimVector2(_outward.x, _outward.y);
            _settings.OutwardWeight = _outwardWeight;
            _settings.AwayMin = _awayMin;
            _settings.AwayMax = Mathf.Max(_awayMin, _awayMax);
            _settings.ReturnSeconds = _returnSeconds;
            _settings.LandClearDistance = _landClearDistance;
        }

        private void Update()
        {
            _chefs.Clear();
            foreach (var player in PlayerInput.all)
                _chefs.Add(new SimVector2(player.transform.position.x, player.transform.position.z));
            _flock.Tick(Time.deltaTime, _chefs);

            for (var i = 0; i < _birds.Length; i++)
            {
                var sim = _flock.Birds[i];
                var bird = _birds[i];
                var flying = sim.Flying;
                bird.position = new Vector3(sim.Position.X, sim.Position.Y, sim.Position.Z);

                // In flight it faces the way it's going: out on the way off, home on the way back.
                if (flying)
                {
                    var heading = new Vector3(sim.Direction.X, 0f, sim.Direction.Y);
                    if (sim.State == TerraceBird.Phase.Returning)
                        heading = -heading;
                    if (heading.sqrMagnitude > 1e-6f)
                        bird.rotation = Quaternion.LookRotation(heading);
                }

                // Shrinks away at the end of its flight, and grows back as it starts the return.
                var shrink = Mathf.Clamp01((1f - sim.Along) / _shrinkOver);
                bird.localScale = sim.Visible ? _scales[i] * shrink : Vector3.zero;

                // No pecking in the air: the body held level, the wings out and flapping.
                if (_pecks[i] != null && _pecks[i].enabled == flying)
                {
                    _pecks[i].enabled = !flying;
                    if (flying)
                        _bodies[i].localRotation = _bodyRotations[i];
                }
                foreach (var wing in _wings[i])
                    if (wing.activeSelf != flying)
                        wing.SetActive(flying);
            }
        }
    }
}
