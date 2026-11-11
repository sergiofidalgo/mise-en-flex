using System.Collections.Generic;
using Game.Gameplay;
using Game.Simulation.Days;
using Game.Simulation.Street;
using UnityEngine;

namespace Game.Presentation.Street
{
    /// <summary>
    /// The kitchen's extractor stack, seen from the street: it steams as hard as the kitchen is
    /// cooking (nothing with the hobs off, a wisp with them hot and empty, a plume in a rush), and
    /// pours dark smoke while a pan is on fire. The kitchen tells the street what's going on in it.
    /// Cosmetic only.
    /// </summary>
    public sealed class ExtractorVentView : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private ParticleSystem _steam;
        [Tooltip("Dark smoke, only while something's on fire.")]
        [SerializeField] private ParticleSystem _smoke;

        [Tooltip("Particles a second with the kitchen flat out.")]
        [SerializeField, Min(0f)] private float _steamRate = 5f;
        [SerializeField, Min(0f)] private float _smokeRate = 9f;
        [Tooltip("A hot hob with nothing on it counts for this share of one that's cooking.")]
        [SerializeField, Range(0f, 1f)] private float _idleShare = 0.3f;
        [Tooltip("How many hobs cooking at full heat give the full plume.")]
        [SerializeField, Min(0.1f)] private float _fullAt = 2f;
        [Tooltip("Seconds the plume takes to follow the kitchen: an extractor doesn't stop dead.")]
        [SerializeField, Min(0.01f)] private float _lag = 2f;

        private readonly List<(float Heat, bool Cooking)> _hobs = new List<(float, bool)>();
        private float _strength;

        private void Update()
        {
            var services = _root != null ? _root.Services : null;
            if (services == null)
                return;

            var burning = false;
            _hobs.Clear();
            // Nothing cooks between shifts.
            var phase = services.Days.Phase;
            if (phase == DayPhase.Prep || phase == DayPhase.Service)
            {
                foreach (var hob in services.Cooking.Hobs)
                {
                    _hobs.Add((hob.Dial, hob.Pan != null && hob.Pan.Contents.Count > 0));
                    burning |= services.Fires.IsBurning(hob);
                }
            }
            var target = StreetHours.Extraction(_hobs, _idleShare, _fullAt);
            _strength = Mathf.Lerp(_strength, target, 1f - Mathf.Exp(-Time.deltaTime / _lag));

            if (_steam != null)
            {
                var emission = _steam.emission;
                emission.rateOverTime = burning ? 0f : _strength * _steamRate;
            }
            if (_smoke != null)
            {
                var emission = _smoke.emission;
                emission.rateOverTime = burning ? _smokeRate : 0f;
            }
        }
    }
}
