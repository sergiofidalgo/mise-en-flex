using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows that the sink is in use (the user's note, 2026-11-08): while someone washes, chef or
    /// dishwasher, water runs from the tap into the basin, splashing where it lands, and soap
    /// bubbles rise round the plate. Washing is seen from the plate's wash progress moving, and the
    /// effect lingers a moment after it stops, so short pauses don't flicker it off and on.
    /// </summary>
    public sealed class SinkWashView : MonoBehaviour
    {
        [SerializeField] private SinkStation _sink;
        [Tooltip("The stream of water from the tap to the basin.")]
        [SerializeField] private Renderer _stream;
        [SerializeField] private ParticleSystem _splash;
        [SerializeField] private ParticleSystem _bubbles;

        [Tooltip("Seconds the water keeps running after washing last moved on.")]
        [SerializeField, Min(0f)] private float _linger = 0.35f;
        [SerializeField, Min(0f)] private float _splashRate = 80f;
        [SerializeField, Min(0f)] private float _bubbleRate = 5f;
        [Tooltip("How much the stream wobbles in width, as a share of it.")]
        [SerializeField, Range(0f, 0.5f)] private float _wobble = 0.15f;

        private Vector3 _streamScale;
        private float _lastProgress;
        private float _lastWashed = float.NegativeInfinity;
        private bool _running;

        private void Awake()
        {
            _streamScale = _stream.transform.localScale;
            Show(false);
        }

        private void Update()
        {
            var sink = _sink.Sink;
            if (sink == null)
                return;

            // Any change counts: progress climbs while washing and drops to zero when a plate's done.
            if (!Mathf.Approximately(sink.WashProgress, _lastProgress) || _sink.Game != null)
                _lastWashed = Time.time;
            _lastProgress = sink.WashProgress;

            var running = Time.time - _lastWashed <= _linger;
            if (running != _running)
                Show(running);
            if (running)
            {
                var wobble = 1f + _wobble * Mathf.Sin(Time.time * 37f) * Mathf.Sin(Time.time * 23f);
                _stream.transform.localScale = new Vector3(_streamScale.x * wobble, _streamScale.y, _streamScale.z * wobble);
            }
        }

        private void Show(bool running)
        {
            _running = running;
            _stream.enabled = running;
            var splash = _splash.emission;
            splash.rateOverTime = running ? _splashRate : 0f;
            var bubbles = _bubbles.emission;
            bubbles.rateOverTime = running ? _bubbleRate : 0f;
        }
    }
}
