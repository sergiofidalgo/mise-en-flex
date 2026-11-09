using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Office.Ambience
{
    /// <summary>
    /// The city's windows, alive: every so often a light goes on or off somewhere, and a few
    /// windows flicker blue with a television. One clock for all of them, cheap at any count.
    /// Televisions get their own material copies at runtime, so Play mode never edits the assets.
    /// </summary>
    public sealed class WindowFlicker : MonoBehaviour
    {
        [SerializeField] private Renderer[] _windows = System.Array.Empty<Renderer>();
        [Tooltip("Lit windows, one per kind of room behind them (WindowArt); a window that lights up takes one at random.")]
        [SerializeField] private Material[] _lits = System.Array.Empty<Material>();
        [SerializeField] private Material _dark;
        [Tooltip("Windows with a television on; each gets its own flicker.")]
        [SerializeField] private Renderer[] _televisions = System.Array.Empty<Renderer>();
        [Tooltip("Seconds between one window changing and the next.")]
        [SerializeField, Min(0.02f)] private float _interval = 0.4f;
        [Tooltip("Seconds between a television's changes of brightness.")]
        [SerializeField, Min(0.02f)] private float _televisionStep = 0.12f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Color TelevisionBright = new Color(0.62f, 0.78f, 1f);
        private static readonly Color TelevisionDim = new Color(0.22f, 0.3f, 0.5f);

        private readonly List<Material> _screens = new List<Material>();
        private System.Random _random;
        private float _nextWindow;
        private float _nextTelevision;

        public void Set(Renderer[] windows, Material[] lits, Material dark, Renderer[] televisions) =>
            (_windows, _lits, _dark, _televisions) = (windows, lits, dark, televisions);

        private void Awake()
        {
            _random = new System.Random(2611);
            foreach (var television in _televisions)
            {
                if (television == null)
                    continue;
                var screen = new Material(television.sharedMaterial);
                television.sharedMaterial = screen;
                _screens.Add(screen);
            }
        }

        private void OnDestroy()
        {
            foreach (var screen in _screens)
                Destroy(screen);
        }

        private void Update()
        {
            if (Time.time >= _nextWindow && _windows.Length > 0)
            {
                _nextWindow = Time.time + _interval * (0.5f + (float)_random.NextDouble());
                var window = _windows[_random.Next(_windows.Length)];
                if (window != null && _lits.Length > 0)
                    window.sharedMaterial = window.sharedMaterial == _dark ? _lits[_random.Next(_lits.Length)] : _dark;
            }
            if (Time.time >= _nextTelevision)
            {
                _nextTelevision = Time.time + _televisionStep;
                foreach (var screen in _screens)
                    screen.SetColor(BaseColor, Color.Lerp(TelevisionDim, TelevisionBright, (float)_random.NextDouble()));
            }
        }
    }
}
