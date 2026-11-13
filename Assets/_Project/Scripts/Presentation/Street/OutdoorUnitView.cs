using System.Collections.Generic;
using Game.Gameplay;
using Game.Simulation.Days;
using Game.Simulation.Street;
using UnityEngine;

namespace Game.Presentation.Street
{
    /// <summary>
    /// The air conditioning's outdoor unit by the side wall: its fan idles all day and turns faster
    /// the harder the kitchen is cooking, easing up and down as a motor does. The kitchen tells the
    /// street what's going on in it. Cosmetic only.
    /// </summary>
    public sealed class OutdoorUnitView : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [Tooltip("The fan, turning about its own local z.")]
        [SerializeField] private Transform _fan;

        [Tooltip("Degrees a second with nothing cooking.")]
        [SerializeField, Min(0f)] private float _idle = 150f;
        [Tooltip("Degrees a second with the kitchen flat out.")]
        [SerializeField, Min(0f)] private float _flatOut = 1100f;
        [Tooltip("A hot hob with nothing on it counts for this share of one that's cooking.")]
        [SerializeField, Range(0f, 1f)] private float _idleShare = 0.3f;
        [Tooltip("How many hobs cooking at full heat run it flat out.")]
        [SerializeField, Min(0.1f)] private float _fullAt = 2f;
        [Tooltip("Seconds the fan takes to follow the kitchen.")]
        [SerializeField, Min(0.01f)] private float _lag = 2.5f;

        private readonly List<(float Heat, bool Cooking)> _hobs = new List<(float, bool)>();
        private float _work;

        private void Update()
        {
            var services = _root != null ? _root.Services : null;
            if (services == null || _fan == null)
                return;

            _hobs.Clear();
            // Nothing cooks between shifts.
            var phase = services.Days.Phase;
            if (phase == DayPhase.Prep || phase == DayPhase.Service)
            {
                foreach (var hob in services.Cooking.Hobs)
                    _hobs.Add((hob.Dial, hob.Pan != null && hob.Pan.Contents.Count > 0));
            }
            var target = StreetHours.Extraction(_hobs, _idleShare, _fullAt);
            _work = Mathf.Lerp(_work, target, 1f - Mathf.Exp(-Time.deltaTime / _lag));
            _fan.Rotate(0f, 0f, Mathf.Lerp(_idle, _flatOut, _work) * Time.deltaTime, Space.Self);
        }
    }
}
