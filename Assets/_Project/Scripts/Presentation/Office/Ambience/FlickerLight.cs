using UnityEngine;

namespace Game.Presentation.Office.Ambience
{
    /// <summary>
    /// A light with a mood (decision 2026-11-09, the office at night): a fire's restless glow, a
    /// television's jumpy blue, or a failing fluorescent tube that mostly works and now and then
    /// stutters. A faulty tube also switches its glowing part, so the fixture goes dark with it.
    /// </summary>
    public sealed class FlickerLight : MonoBehaviour
    {
        public enum Mood { Fire, Television, Faulty }

        [SerializeField] private Light _light;
        [SerializeField] private Mood _mood = Mood.Fire;
        [Tooltip("The glowing part that goes dark when a faulty tube does; optional.")]
        [SerializeField] private GameObject _glow;
        [Tooltip("A television's seconds per shot, or a faulty tube's seconds between stutters.")]
        [SerializeField, Min(0.05f)] private float _rate = 0.2f;
        [SerializeField] private float _seed;

        private float _intensity;

        public void Set(Light light, Mood mood, GameObject glow, float rate, float seed) =>
            (_light, _mood, _glow, _rate, _seed) = (light, mood, glow, rate, seed);

        private void Awake() => _intensity = _light != null ? _light.intensity : 1f;

        private void Update()
        {
            if (_light == null)
                return;
            switch (_mood)
            {
                case Mood.Fire:
                    _light.intensity = _intensity * AmbientMotion.Fire(Time.time, _seed);
                    break;
                case Mood.Television:
                    _light.intensity = _intensity * AmbientMotion.Television(Time.time, _rate, _seed);
                    break;
                case Mood.Faulty:
                    var on = AmbientMotion.FaultyOn(Time.time, _rate, _seed);
                    _light.enabled = on;
                    if (_glow != null && _glow.activeSelf != on)
                        _glow.SetActive(on);
                    break;
            }
        }
    }
}
