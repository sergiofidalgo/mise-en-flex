using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows how full the bin is (the user's note, 2026-11-08): empty, the bag inside is dark and
    /// deep; each thing thrown away raises a mound of rubbish up the bag, until it's full to the
    /// rim, and once it's getting full, flies come buzzing round it, more the fuller it is. For now
    /// it only fills: emptying it, by taking the bag out, comes with the restaurant's outside
    /// (docs/ideas.md).
    /// </summary>
    public sealed class BinFillView : MonoBehaviour
    {
        [SerializeField] private BinStation _bin;
        [Tooltip("The rubbish mound, raised up the bag as the bin fills.")]
        [SerializeField] private Transform _trash;

        [Tooltip("How many things thrown away fill the bin to the rim.")]
        [SerializeField, Min(1)] private int _itemsWhenFull = 24;
        [Tooltip("The mound's height in the bin when it's nearly empty, and when it's full (m).")]
        [SerializeField] private Vector2 _level = new Vector2(0.08f, 0.6f);
        [Tooltip("Flies buzzing round the bin once it's getting full.")]
        [SerializeField] private ParticleSystem _flies;
        [Tooltip("How full the bin is when the first flies come, and how many arrive a second when it's full.")]
        [SerializeField, Range(0f, 1f)] private float _fliesFrom = 0.5f;
        [SerializeField, Min(0f)] private float _flyRate = 3f;
        [Tooltip("How fast the mound rises to its new level, in metres per second.")]
        [SerializeField, Min(0f)] private float _riseSpeed = 0.5f;

        private Renderer _trashRenderer;

        /// <summary>How full the bin is, from 0 (empty) to 1 (to the rim).</summary>
        public float Fill => Mathf.Clamp01((float)_bin.ThrownAway / _itemsWhenFull);

        private void Awake()
        {
            _trashRenderer = _trash.GetComponent<Renderer>();
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
    }
}
