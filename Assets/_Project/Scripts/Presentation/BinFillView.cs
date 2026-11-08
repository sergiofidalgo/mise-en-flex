using Game.Gameplay.Stations;
using UnityEngine;
using UnityEngine.InputSystem;

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

        [Header("Full sign")]
        [Tooltip("Height of the 🚫 sign above the bin's slot (m).")]
        [SerializeField, Min(0f)] private float _signHeight = 1.25f;
        [Tooltip("The sign's width in the world (m).")]
        [SerializeField, Min(0.05f)] private float _signSize = 0.42f;
        [Tooltip("How long the sign takes to pop in or out (s).")]
        [SerializeField, Min(0.01f)] private float _signPop = 0.18f;
        [Tooltip("How far it bobs up and down (m), and how many times a second.")]
        [SerializeField, Min(0f)] private float _bobHeight = 0.03f;
        [SerializeField, Min(0f)] private float _bobRate = 1.2f;
        [Tooltip("How far it rocks (degrees) when someone holding something faces the full bin, and how fast.")]
        [SerializeField, Min(0f)] private float _shakeAngle = 14f;
        [SerializeField, Min(0f)] private float _shakeRate = 22f;

        private static Sprite _noEntry;

        private Renderer _trashRenderer;
        private Transform _sign;
        private float _signShown;

        /// <summary>How full the bin is, from 0 (empty) to 1 (to the rim).</summary>
        public float Fill => Mathf.Clamp01(_bin.Sink.Fill);

        private void Awake()
        {
            _trashRenderer = _trash.GetComponent<Renderer>();
            _trash.localPosition = new Vector3(0f, _level.x, 0f);
            _sign = BuildSign();
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
            _signShown = Mathf.MoveTowards(_signShown, _bin.Sink.IsFull ? 1f : 0f, Time.deltaTime / _signPop);
            var shown = _signShown > 0f;
            if (_sign.gameObject.activeSelf != shown)
                _sign.gameObject.SetActive(shown);
            if (!shown)
                return;

            // Pops in with a little overshoot, so it catches the eye the moment the bag fills.
            _sign.localScale = Vector3.one * (_signSize * Overshoot(_signShown));
            _sign.position = _bin.transform.position
                + Vector3.up * (_signHeight + _bobHeight * Mathf.Sin(Time.time * _bobRate * 2f * Mathf.PI));

            // Faces the camera flat on, so it reads as a sign from the overhead angle.
            var camera = Camera.main;
            var rock = SomeoneTrying() ? _shakeAngle * Mathf.Sin(Time.time * _shakeRate) : 0f;
            if (camera != null)
                _sign.rotation = camera.transform.rotation * Quaternion.Euler(0f, 0f, rock);
        }

        // 0..1 to a scale that swells a little past 1 and settles back (ease-out-back).
        private static float Overshoot(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private bool SomeoneTrying()
        {
            foreach (var player in PlayerInput.all)
            {
                if (player.TryGetComponent(out PlayerInteractor interactor)
                    && interactor.Target == _bin && !interactor.Hands.IsEmpty)
                    return true;
            }
            return false;
        }

        private Transform BuildSign()
        {
            var go = new GameObject("FullSign");
            go.transform.SetParent(transform, false);
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = NoEntrySprite();
            sprite.sortingOrder = 10;
            go.SetActive(false);
            return go.transform;
        }

        // A no-entry sign drawn in code (a white disc, a red ring and a slash from top left to
        // bottom right like 🚫, a dark rim so it reads on any floor), so it needs no texture asset.
        // TODO(Phase 9): swap for a drawn icon if the art pass wants one.
        private static Sprite NoEntrySprite()
        {
            if (_noEntry != null)
                return _noEntry;

            const int size = 128;
            const float outer = 0.5f;      // the sign's radius, in texture widths
            const float rimWidth = 0.035f;
            const float ringWidth = 0.11f;
            const float slashHalf = 0.055f;
            const float softEdge = 1.5f / size;
            var red = new Color(0.86f, 0.16f, 0.14f, 1f);
            var rim = new Color(0.10f, 0.08f, 0.08f, 1f);
            var white = new Color(1f, 0.98f, 0.95f, 1f);

            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var px = (x + 0.5f) / size - 0.5f;
                    var py = (y + 0.5f) / size - 0.5f;
                    var r = Mathf.Sqrt(px * px + py * py);
                    var fromSlash = Mathf.Abs(px + py) / Mathf.Sqrt(2f); // distance from the line y = -x

                    var colour = r > outer - rimWidth ? rim
                        : r > outer - rimWidth - ringWidth || fromSlash < slashHalf ? red
                        : white;
                    colour.a = Mathf.Clamp01((outer - r) / softEdge);
                    pixels[y * size + x] = colour;
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "TEX_BinFullSign",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            _noEntry = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _noEntry;
        }
    }
}
