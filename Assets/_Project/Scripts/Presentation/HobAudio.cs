using System.Collections.Generic;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Simulation.Cooking;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// The hobs: the knob clicks as it turns, gas catches with a whump and goes out with a puff, and a
    /// pan fire roars up, crackles while it burns and hisses out under the extinguisher.
    /// </summary>
    public sealed class HobAudio : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private KitchenAudio _audio;
        [Tooltip("How fast the fire's roar fades in and out, per second.")]
        [SerializeField, Min(0.1f)] private float _fireFadeRate = 2f;

        private readonly Dictionary<Hob, int> _notches = new Dictionary<Hob, int>();
        private KitchenServices _services;
        private AudioSource _fire;

        private void Start()
        {
            _services = _root.Services;
            _services.Fires.Ignited += HandleIgnited;
            _services.Fires.PutOut += HandlePutOut;

            _fire = SfxPlayer.NewSource(gameObject);
            _fire.clip = _audio.Sounds.FireLoop;
            _fire.loop = true;
            _fire.volume = 0f;
        }

        private void OnDestroy()
        {
            if (_services == null)
                return;
            _services.Fires.Ignited -= HandleIgnited;
            _services.Fires.PutOut -= HandlePutOut;
        }

        private void Update()
        {
            var sounds = _audio.Sounds;
            HobStation burning = null;
            foreach (var station in _services.Stations.All)
            {
                if (!(station is HobStation hobStation) || hobStation.Hob == null)
                    continue;
                if (hobStation.IsOnFire && burning == null)
                    burning = hobStation;
                Listen(hobStation, sounds);
            }

            var goal = burning != null ? sounds.FireLoopVolume : 0f;
            _fire.volume = Mathf.MoveTowards(_fire.volume, goal, _fireFadeRate * Time.unscaledDeltaTime);
            if (burning != null)
                _fire.panStereo = _audio.StereoAt(burning.transform.position);
            if (_fire.volume > 0f && !_fire.isPlaying && _fire.clip != null)
                _fire.Play();
            else if (_fire.volume <= 0f && _fire.isPlaying)
                _fire.Stop();
        }

        // Clicks only on a change, never for where a knob already was when the kitchen loaded.
        private void Listen(HobStation station, Game.Data.SoundBank sounds)
        {
            var hob = station.Hob;
            int notch;
            if (hob.Kind == HeatKind.Gas)
                notch = SoundMath.GasNotch(hob.Dial, sounds.GasNotches);
            else if (hob.Kind == HeatKind.Induction)
                notch = hob.Step;
            else
                return;

            if (!_notches.TryGetValue(hob, out var last))
            {
                _notches[hob] = notch;
                return;
            }
            if (notch == last)
                return;
            _notches[hob] = notch;

            var at = station.transform.position;
            if (hob.Kind == HeatKind.Gas && last == 0)
                _audio.Play(sounds.GasIgnite, at);
            else if (hob.Kind == HeatKind.Gas && notch == 0)
                _audio.Play(sounds.GasOff, at);
            else
                // Higher as it goes up, so the direction can be heard.
                _audio.Play(sounds.HeatClick, at, notch > last ? 1.08f : 0.92f);
        }

        private void HandleIgnited(Hob hob, IReadOnlyList<Ingredient> food) => _audio.Play(_audio.Sounds.FireWhoosh, PositionOf(hob));

        private void HandlePutOut(Hob hob) => _audio.Play(_audio.Sounds.FireOut, PositionOf(hob));

        private Vector3 PositionOf(Hob hob)
        {
            foreach (var station in _services.Stations.All)
            {
                if (station is HobStation hobStation && hobStation.Hob == hob)
                    return hobStation.transform.position;
            }
            return transform.position;
        }
    }
}
