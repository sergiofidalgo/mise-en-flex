using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows how full the bin is (the user's notes, 2026-11-08): empty, the bag inside is dark and
    /// deep; each thing thrown away raises a mound of rubbish up the bag, until it's full to the
    /// rim, and once it's getting full, flies come buzzing round it, more the fuller it is. Once
    /// it's full, a 🚫 sign pops up over it and stays until it's emptied; it rocks when a player
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
        [SerializeField, Range(0f, 1f)] private float _fliesFrom = 0.5f;
        [SerializeField, Min(0f)] private float _flyRate = 3f;
        [Tooltip("How fast the mound rises to its new level, in metres per second.")]
        [SerializeField, Min(0f)] private float _riseSpeed = 0.5f;

        [Tooltip("The 🚫 over the bin once it's full.")]
        [SerializeField] private FullSign _fullSign = new FullSign();

        private Renderer _trashRenderer;

        /// <summary>How full the bin is, from 0 (empty) to 1 (to the rim).</summary>
        public float Fill => Mathf.Clamp01(_bin.Sink.Fill);

        private void Awake()
        {
            _trashRenderer = _trash.GetComponent<Renderer>();
            _trash.localPosition = new Vector3(0f, _level.x, 0f);
            _fullSign.Build(_bin, _trash.parent); // over the bin's model, which sits snug in its slot
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

            var emission = _flies.emission;
            emission.rateOverTime = fill < _fliesFrom ? 0f : _flyRate * Mathf.InverseLerp(_fliesFrom, 1f, fill) + 0.5f;
        }

        private void LateUpdate()
        {
            _fullSign.Tick(_bin.Sink.IsFull);
        }
    }
}
