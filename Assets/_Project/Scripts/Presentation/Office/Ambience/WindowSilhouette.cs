using UnityEngine;

namespace Game.Presentation.Office.Ambience
{
    /// <summary>
    /// Someone walking past a lit window (user's note, 2026-11-09: "shadows, like people passing
    /// behind"). Every so often a silhouette crosses the glass, the wall hiding it as it comes in at
    /// one side and leaves at the other, then nobody for a while; the next one may go the other way.
    /// Works in the window's own space (a quad, −0.5 to 0.5 across), cropping the silhouette at the
    /// frame with its texture's tiling, so it never shows outside the glass.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class WindowSilhouette : MonoBehaviour
    {
        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

        [Tooltip("The silhouette's width as a share of the window's.")]
        [SerializeField, Range(0.1f, 1f)] private float _width = 0.45f;
        [Tooltip("Seconds to cross the window.")]
        [SerializeField, Min(0.5f)] private float _crossing = 3.5f;
        [Tooltip("Seconds between one crossing and the next, at least and at most.")]
        [SerializeField] private Vector2 _pause = new Vector2(4f, 12f);
        [SerializeField] private int _seed;

        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private System.Random _random;
        private float _start;
        private float _next;
        private bool _leftToRight;
        private Vector3 _scale;
        private Vector3 _position;

        public void Set(float width, float crossing, Vector2 pause, int seed) =>
            (_width, _crossing, _pause, _seed) = (width, crossing, pause, seed);

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
            _random = new System.Random(_seed);
            _scale = transform.localScale;
            _position = transform.localPosition;
            _next = (float)_random.NextDouble() * _pause.y;
            _renderer.enabled = false;
        }

        private void Update()
        {
            if (Time.time >= _next)
            {
                _start = Time.time;
                _leftToRight = _random.NextDouble() < 0.5;
                _next = _start + _crossing + Mathf.Lerp(_pause.x, _pause.y, (float)_random.NextDouble());
            }
            var t = (Time.time - _start) / _crossing;
            if (t < 0f || t > 1f)
            {
                _renderer.enabled = false;
                return;
            }

            // The silhouette's centre goes from fully off one side to fully off the other.
            var half = _width / 2f;
            var centre = Mathf.Lerp(-0.5f - half, 0.5f + half, _leftToRight ? t : 1f - t);
            var left = Mathf.Max(centre - half, -0.5f);
            var right = Mathf.Min(centre + half, 0.5f);
            if (right <= left)
            {
                _renderer.enabled = false;
                return;
            }
            _renderer.enabled = true;
            // Show only the part within the glass: narrow the quad and crop the texture to match.
            var visible = right - left;
            transform.localPosition = new Vector3((left + right) / 2f, _position.y, _position.z);
            transform.localScale = new Vector3(visible, _scale.y, _scale.z);
            var tiling = visible / _width;
            var offset = (left - (centre - half)) / _width;
            _renderer.GetPropertyBlock(_block);
            _block.SetVector(BaseMapSt, new Vector4(tiling, 1f, offset, 0f));
            _renderer.SetPropertyBlock(_block);
        }
    }
}
