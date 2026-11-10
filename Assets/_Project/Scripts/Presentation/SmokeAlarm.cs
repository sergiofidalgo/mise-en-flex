using Game.Gameplay;
using Game.Simulation.Cooking;
using Game.Simulation.Days;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// The fire detector: beeps while food is close to burning in a hot pan, the sound of the "act
    /// now" smoke that <see cref="PanView"/> shows. One alarm however many pans are burning, panned
    /// towards the worst, so players can tell which way to run. A warning only: it costs nothing itself.
    /// </summary>
    public sealed class SmokeAlarm : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private KitchenAudio _audio;

        private float _nextBeep;
        private bool _sounding;

        private void Update()
        {
            var worst = WorstPan();
            if (worst == null)
            {
                _sounding = false;
                return;
            }

            // The first beep comes straight away; the alarm only keeps time once it's going.
            var now = Time.unscaledTime;
            if (_sounding && now < _nextBeep)
                return;

            _sounding = true;
            _nextBeep = now + _audio.Sounds.AlarmInterval;
            var position = _root.Services.ItemViews.TryGetView(worst, out var view) ? view.transform.position : transform.position;
            _audio.Play(_audio.Sounds.AlarmBeep, position);
        }

        private Pan WorstPan()
        {
            var services = _root.Services;
            // Nothing cooks between shifts, and burnt food left over shouldn't ring through Review.
            var phase = services.Days.Phase;
            if (phase != DayPhase.Prep && phase != DayPhase.Service)
                return null;

            // A pan on fire is the worst there is: the alarm keeps going until it's out (Phase 5).
            foreach (var hob in services.Cooking.Hobs)
            {
                if (hob.Pan != null && services.Fires.IsBurning(hob))
                    return hob.Pan;
            }

            var sounds = _audio.Sounds;
            Pan worst = null;
            var worstCloseness = 0f;
            foreach (var pan in services.Cooking.Pans)
            {
                foreach (var food in pan.Contents)
                {
                    // The side on the pan is the one burning, as with the smoke: a flip buys time.
                    var closeness = SoundMath.BurnCloseness(food.Browning(food.DownSide), food.Properties.BurntBrowning);
                    if (closeness > worstCloseness && SoundMath.AlarmSounds(closeness, sounds.AlarmFrom, pan.Temperature, sounds.AlarmHotEnough))
                    {
                        worst = pan;
                        worstCloseness = closeness;
                    }
                }
            }
            return worst;
        }
    }
}
