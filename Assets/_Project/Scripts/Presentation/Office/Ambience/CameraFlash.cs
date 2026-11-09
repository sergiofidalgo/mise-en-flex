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

        private float _peak;

        public void Set(Light light, GameObject spark, float period, float seed) =>
            (_light, _spark, _period, _seed) = (light, spark, period, seed);

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
                if (on)
                    _spark.transform.localScale = Vector3.one * (0.15f + 0.35f * brightness);
            }
        }
    }
}
