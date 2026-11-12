using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// A few flies circling over the wheelie bin (user's call, 2026-11-11): the signifier that it
    /// smells, so a player who sees guests grumble by the door can tell why. Each fly loops round a
    /// point over the lid on its own radius and pace, bobbing and wandering with smooth noise. The
    /// loops stay inside the bin's half-depth, so flies never pass through the wall it stands against.
    /// </summary>
    public sealed class BinFlies : MonoBehaviour
    {
        [Tooltip("The flies' look; a plain dark material if empty.")]
        [SerializeField] private Material _material;
        [SerializeField, Range(0, 16)] private int _count = 3;
        [Tooltip("Metres above the bin's base that the flies circle around (the lid is at about 1.35).")]
        [SerializeField, Min(0f)] private float _height = 1.5f;
        [Tooltip("Smallest and largest loop, metres from the middle. Keep under half the bin's depth (0.6) so no fly enters the wall.")]
        [SerializeField] private Vector2 _radius = new Vector2(0.12f, 0.35f);
        [Tooltip("How far each fly bobs up and down, metres.")]
        [SerializeField, Min(0f)] private float _bob = 0.08f;
        [Tooltip("Loops a second, slowest and fastest.")]
        [SerializeField] private Vector2 _loopsPerSecond = new Vector2(0.3f, 0.7f);
        [Tooltip("How far noise pulls a fly off its loop, metres.")]
        [SerializeField, Min(0f)] private float _wander = 0.06f;
        [Tooltip("A fly's size, metres. Big enough to read from the overhead camera.")]
        [SerializeField, Min(0.005f)] private float _size = 0.03f;

        private Transform[] _flies = new Transform[0];
        private float[] _seeds = new float[0];

        private void Start() => Build();

        private void Update() => Place(Time.time);

        /// <summary>
        /// Puts the flies where they'd be at <paramref name="time"/>, making them first if needed:
        /// for captures in edit mode, where Start and Update don't run.
        /// </summary>
        public void Pose(float time)
        {
            if (_flies.Length != _count)
                Build();
            Place(time);
        }

        private void Build()
        {
            foreach (var old in _flies)
                if (old != null)
                    DestroyImmediate(old.gameObject);
            var material = _material != null ? _material : DefaultMaterial();
            _flies = new Transform[_count];
            _seeds = new float[_count];
            for (var i = 0; i < _count; i++)
            {
                var fly = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fly.name = "Fly";
                DestroyImmediate(fly.GetComponent<Collider>()); // nothing bumps into a fly
                var view = fly.GetComponent<Renderer>();
                view.sharedMaterial = material;
                view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                fly.transform.SetParent(transform, false);
                fly.transform.localScale = new Vector3(_size, _size * 0.8f, _size);
                _flies[i] = fly.transform;
                // Fixed per fly and per bin, so the swarm doesn't change between visits.
                _seeds[i] = (i + 1) * 7.31f + transform.position.x * 0.37f + transform.position.z * 0.11f;
            }
        }

        private void Place(float time)
        {
            for (var i = 0; i < _flies.Length; i++)
            {
                var seed = _seeds[i];
                var radius = Mathf.Lerp(_radius.x, _radius.y, Frac(seed * 0.618f));
                var speed = Mathf.Lerp(_loopsPerSecond.x, _loopsPerSecond.y, Frac(seed * 0.414f));
                var direction = Frac(seed * 0.273f) < 0.5f ? -1f : 1f;
                var angle = direction * (time * speed * Mathf.PI * 2f + seed);
                var wanderX = (Mathf.PerlinNoise(seed, time * 1.7f) - 0.5f) * 2f * _wander;
                var wanderZ = (Mathf.PerlinNoise(time * 1.7f, seed) - 0.5f) * 2f * _wander;
                var bob = Mathf.Sin(time * 3.1f + seed * 2f) * _bob;
                // The wander can't carry a fly past the largest loop, which keeps it off the wall.
                var flat = Vector2.ClampMagnitude(new Vector2(Mathf.Cos(angle) * radius + wanderX, Mathf.Sin(angle) * radius + wanderZ), _radius.y);
                _flies[i].localPosition = new Vector3(flat.x, _height + bob, flat.y);
            }
        }

        private static float Frac(float value) => value - Mathf.Floor(value);

        private static Material DefaultMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = "BinFly (runtime)" };
            material.color = new Color(0.08f, 0.08f, 0.09f);
            return material;
        }
    }
}
