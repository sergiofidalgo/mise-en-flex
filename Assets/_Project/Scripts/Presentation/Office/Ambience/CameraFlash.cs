using UnityEngine;

namespace Game.Presentation.Office.Ambience
{
    /// <summary>
    /// A camera or phone going off now and then (user's note, 2026-11-09: "camera flashes,
    /// customers shooting selfies in front of a restaurant"): a light that pops and a bright
    /// spark at the lens, timed by <see cref="AmbientMotion.Flash"/>.
    /// </summary>
    public sealed class CameraFlash : MonoBehaviour
    {
        [SerializeField] private Light _light;
        [Tooltip("The spark at the lens, shown while the flash is bright.")]
        [SerializeField] private GameObject _spark;
        [SerializeField, Min(1f)] private float _period = 6f;
        [SerializeField] private float _seed;
        [Tooltip("The spark's size at the flash's brightest (m).")]
        [SerializeField, Min(0.05f)] private float _sparkSize = 0.5f;
        [Tooltip("Turn the spark to the camera: for a flat flare.")]
        [SerializeField] private bool _faceCamera;

        private float _peak;
        private Camera _camera;

        public void Set(Light light, GameObject spark, float period, float seed, float sparkSize = 0.5f, bool faceCamera = false) =>
            (_light, _spark, _period, _seed, _sparkSize, _faceCamera) = (light, spark, period, seed, sparkSize, faceCamera);

        private void Awake()
        {
            _peak = _light != null ? _light.intensity : 1f;
            Apply(0f);
        }

        private void Update() => Apply(AmbientMotion.Flash(Time.time, _period, _seed));

        private void Apply(float brightness)
        {
            var on = brightness > 0.05f;
            if (_light != null)
            {
                _light.enabled = on;
                _light.intensity = _peak * brightness;
            }
            if (_spark != null)
            {
                if (_spark.activeSelf != on)
                    _spark.SetActive(on);
                if (!on)
                    return;
                // Sized in the world, whatever it hangs from.
                var parent = _spark.transform.parent;
                // (An arm may be mirrored, so its scale can be negative: only its size matters here.)
                var size = parent != null ? Mathf.Abs(parent.lossyScale.x) : 1f;
                var scale = size > 1e-4f ? 1f / size : 1f;
                _spark.transform.localScale = Vector3.one * (_sparkSize * (0.3f + 0.7f * brightness) * scale);
                if (!_faceCamera)
                    return;
                if (_camera == null)
                    _camera = Camera.main;
                if (_camera != null)
                    _spark.transform.rotation = _camera.transform.rotation;
            }
        }
    }
}
