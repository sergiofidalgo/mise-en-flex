using Game.Gameplay;
using Game.Simulation.Street;
using UnityEngine;

namespace Game.Presentation.Street
{
    /// <summary>
    /// The street's lamps and the houses' windows come on at dusk by the service's clock (the art
    /// bible's "streetlamps switch on at 19:00"): a dinner service that runs past seven sees them
    /// light up one after another, a lunch never does. Only lamps and windows change; the daylight
    /// itself stays as it is until a darker evening is decided on.
    /// </summary>
    public sealed class StreetLampsView : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [Tooltip("The lamps' lights, dark by day.")]
        [SerializeField] private Light[] _lights = new Light[0];
        [Tooltip("Each lamp's bulb, in the same order.")]
        [SerializeField] private Renderer[] _bulbs = new Renderer[0];
        [SerializeField] private Material _bulbOn;
        [SerializeField] private Material _bulbOff;
        [Tooltip("Windows that light up after dark. Not every window: some houses stay dark.")]
        [SerializeField] private Renderer[] _windows = new Renderer[0];
        [SerializeField] private Material _windowOn;
        [SerializeField] private Material _windowOff;

        [Tooltip("The hour on the clock the lamps come on, and the hour they go off.")]
        [SerializeField, Range(0f, 24f)] private float _duskHour = 19f;
        [SerializeField, Range(0f, 24f)] private float _dawnHour = 7f;
        [Tooltip("Seconds over which they come on one after another, not all at once.")]
        [SerializeField, Min(0f)] private float _spread = 6f;
        [Tooltip("Seconds a lamp takes to warm up.")]
        [SerializeField, Min(0.01f)] private float _warmUp = 0.6f;

        private float[] _full;
        private bool _dark;
        private float _since = float.MaxValue;
        private bool _started;

        private void Awake()
        {
            _full = new float[_lights.Length];
            for (var i = 0; i < _lights.Length; i++)
                _full[i] = _lights[i] != null ? _lights[i].intensity : 0f;
        }

        private void Update()
        {
            var dark = Dark();
            if (!_started)
            {
                // As the scene opens they're simply on or off: nothing to watch come on.
                _started = true;
                _dark = dark;
                _since = float.MaxValue;
            }
            else if (dark != _dark)
            {
                _dark = dark;
                _since = 0f;
            }
            if (_since < float.MaxValue)
                _since += Time.deltaTime;

            for (var i = 0; i < _lights.Length; i++)
            {
                var on = Shown(i, _lights.Length);
                if (_lights[i] != null)
                {
                    _lights[i].intensity = _full[i] * on;
                    _lights[i].enabled = on > 0f;
                }
                if (i < _bulbs.Length && _bulbs[i] != null)
                    _bulbs[i].sharedMaterial = on > 0.5f ? _bulbOn : _bulbOff;
            }
            for (var i = 0; i < _windows.Length; i++)
                if (_windows[i] != null)
                    _windows[i].sharedMaterial = Shown(i * 7 + 3, _windows.Length) > 0.5f ? _windowOn : _windowOff;
        }

        // How lit thing number `index` of `count` is, 0–1: each has its own moment within the
        // spread, in a shuffled order, and warms up (or dies down) from there.
        private float Shown(int index, int count)
        {
            var slot = count > 1 ? (index * 5 % count) / (float)(count - 1) : 0f;
            var warmed = Mathf.Clamp01((_since - slot * _spread) / _warmUp);
            return _dark ? warmed : 1f - warmed;
        }

        private bool Dark() => StreetHours.Dark(DaylightView.ClockHour(_root != null ? _root.Services : null), _duskHour, _dawnHour);
    }
}
