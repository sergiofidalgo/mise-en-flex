using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows how full the bin is (the user's notes, 2026-11-08): empty, the bag inside is dark and
    /// deep; each thing thrown away raises a mound of rubbish up the bag, until it's full to the
    /// rim, and once it's getting full, flies come buzzing round it, more the fuller it is. Once
    /// it's full they become a big, fast swarm that pulses (2026-11-11: urgent, not a detail), and
    /// a 🚫 sign pops up over it and stays until it's emptied; it rocks when a player
    /// with something in their hands faces it, since that's when they'd try. For now a fresh bag
    /// goes in each morning; later players take it out (docs/ideas.md).
    /// </summary>
    public sealed class BinFillView : MonoBehaviour
    {
        [SerializeField] private BinStation _bin;
        [Tooltip("The rubbish mound, raised up the bag as the bin fills.")]
        [SerializeField] private Transform _trash;

        [Tooltip("The mound's height in the bin when it's nearly empty, and when it's full (m).")]
        [SerializeField] private Vector2 _level = new Vector2(0.08f, 0.6f);
        [Tooltip("Flies buzzing round the bin once it's getting full.")]
        [SerializeField] private ParticleSystem _flies;
        [Tooltip("How full the bin is when the first flies come, and how many arrive a second when it's full.")]
        [SerializeField, Range(0f, 1f)] private float _fliesFrom = 0.65f;
        [SerializeField, Min(0f)] private float _flyRate = 1.5f;

        [Header("Full: a swarm (the user's notes, 2026-11-11: urgent, but not intrusive)")]
        [Tooltip("Once full, flies a second, so it reads as urgent from across the kitchen.")]
        [SerializeField, Min(0f)] private float _swarmRate = 5f;
        [Tooltip("Size of a fly while it's getting full, and once it's full (m).")]
        [SerializeField] private Vector2 _flySize = new Vector2(0.045f, 0.07f);
        [Tooltip("How wide they circle while it's getting full, and once it's full (m).")]
        [SerializeField] private Vector2 _swarmRadius = new Vector2(0.14f, 0.2f);
        [Tooltip("How much faster they circle and how much more they dart about once it's full.")]
        [SerializeField, Min(1f)] private float _swarmFrenzy = 1.5f;
        [Tooltip("Once full, a burst of flies this often (s), so the swarm pulses rather than just hangs there.")]
        [SerializeField, Min(0.1f)] private float _burstEvery = 2.2f;
        [SerializeField, Min(0)] private int _burstCount = 3;
        [Tooltip("Most flies alive at once.")]
        [SerializeField, Min(1)] private int _maxFlies = 24;
        [Tooltip("How fast the mound rises to its new level, in metres per second.")]
        [SerializeField, Min(0f)] private float _riseSpeed = 0.5f;

        [Tooltip("The 🚫 over the bin once it's full.")]
        [SerializeField] private FullSign _fullSign = new FullSign();

        private Renderer _trashRenderer;
        private float _orbitCalm;
        private float _buzzCalm;
        private float _untilBurst;

        /// <summary>How full the bin is, from 0 (empty) to 1 (to the rim).</summary>
        public float Fill => Mathf.Clamp01(_bin.Sink.Fill);

        private void Awake()
        {
            _trashRenderer = _trash.GetComponent<Renderer>();
            _trash.localPosition = new Vector3(0f, _level.x, 0f);
            _fullSign.Build(_bin, _trash.parent); // over the bin's model, which sits snug in its slot

            // The scene's flies are built small and calm; how they grow is tuned here.
            var main = _flies.main;
            main.maxParticles = _maxFlies;
            _orbitCalm = _flies.velocityOverLifetime.orbitalYMultiplier;
            _buzzCalm = _flies.noise.strengthMultiplier;
        }

        private void OnEnable()
        {
            _bin.Sink.Emptied += HandleEmptied;
        }

        private void OnDisable()
        {
            _bin.Sink.Emptied -= HandleEmptied;
        }

        // A fresh bag: the mound is gone at once rather than sinking slowly.
        private void HandleEmptied()
        {
            _trash.localPosition = new Vector3(0f, _level.x, 0f);
        }

        private void Update()
        {
            var fill = Fill;
            _trashRenderer.enabled = fill > 0f;
            var target = new Vector3(0f, Mathf.Lerp(_level.x, _level.y, fill), 0f);
            _trash.localPosition = Vector3.MoveTowards(_trash.localPosition, target, _riseSpeed * Time.deltaTime);

            UpdateFlies(fill, _bin.Sink.IsFull);
        }

        // Getting full: a few flies, more as it fills. Full: a big, fast, wide swarm that pulses,
        // a jump the moment the last thing goes in, so "do something about the bin" reads at once.
        private void UpdateFlies(float fill, bool full)
        {
            var emission = _flies.emission;
            var growing = Mathf.InverseLerp(_fliesFrom, 1f, fill);
            emission.rateOverTime = full ? _swarmRate : fill < _fliesFrom ? 0f : _flyRate * growing + 0.5f;

            // New flies take the size of the moment; the ones already out keep theirs.
            var main = _flies.main;
            var size = full ? _flySize.y : Mathf.Lerp(_flySize.x, (_flySize.x + _flySize.y) / 2f, growing);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.8f, size);

            var shape = _flies.shape;
            shape.radius = full ? _swarmRadius.y : _swarmRadius.x;
            var frenzy = full ? _swarmFrenzy : 1f;
            var circle = _flies.velocityOverLifetime;
            circle.orbitalYMultiplier = _orbitCalm * frenzy;
            var buzz = _flies.noise;
            buzz.strengthMultiplier = _buzzCalm * frenzy;

            if (!full)
            {
                _untilBurst = 0f; // the first burst comes the moment it fills
                return;
            }
            _untilBurst -= Time.deltaTime;
            if (_untilBurst <= 0f)
            {
                _flies.Emit(_burstCount);
                _untilBurst = _burstEvery;
            }
        }

        private void LateUpdate()
        {
            _fullSign.Tick(_bin.Sink.IsFull);
        }
    }
}
