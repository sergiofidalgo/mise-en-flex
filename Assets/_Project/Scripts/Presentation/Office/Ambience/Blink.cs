using UnityEngine;

namespace Game.Presentation.Office.Ambience
{
    /// <summary>
    /// Switches a light on and off: aviation lights on the tall towers and the crane, string lights
    /// on a pergola, the bulbs round a sign. The target is a child, so this keeps running while it's off.
    /// </summary>
    public sealed class Blink : MonoBehaviour
    {
        [SerializeField] private GameObject _light;
        [SerializeField, Min(0.01f)] private float _period = 1.5f;
        [SerializeField, Range(0f, 1f)] private float _onFraction = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _phase;

        private bool _on = true;

        public void Set(GameObject light, float period, float onFraction, float phase) =>
            (_light, _period, _onFraction, _phase) = (light, period, onFraction, phase);

        private void Update()
        {
            var on = AmbientMotion.BlinkOn(Time.time, _period, _onFraction, _phase);
            if (on == _on || _light == null)
                return;
            _on = on;
            _light.SetActive(on);
        }
    }
}
