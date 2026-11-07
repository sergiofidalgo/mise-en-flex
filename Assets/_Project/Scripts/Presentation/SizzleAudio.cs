using System.Collections.Generic;
using Game.Data;
using Game.Gameplay;
using Game.Simulation.Cooking;
using Game.Simulation.Days;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Pans you can hear: food in a hot pan sizzles, louder and higher as the pan heats, so a pan's
    /// heat can be judged without looking at it. Food hitting the pan, or being flipped, gives a rush
    /// of hiss on top. Only the loudest few pans get a voice, so a full line doesn't become a wall of noise.
    /// </summary>
    public sealed class SizzleAudio : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private KitchenAudio _audio;

        private sealed class Voice
        {
            public AudioSource Source;
            public Pan Pan;
        }

        private readonly List<Voice> _voices = new List<Voice>();
        private readonly List<(Pan Pan, float Level)> _heard = new List<(Pan, float)>();
        private readonly Dictionary<Pan, (Ingredient Food, IngredientSide Side)> _lastSeen = new Dictionary<Pan, (Ingredient, IngredientSide)>();
        private readonly List<Pan> _gonePans = new List<Pan>();

        private void Start()
        {
            for (var i = 0; i < _audio.Sounds.MaxSizzles; i++)
            {
                var source = KitchenAudio.NewSource(gameObject);
                source.clip = _audio.Sounds.SizzleLoop;
                source.loop = true;
                source.volume = 0f;
                _voices.Add(new Voice { Source = source });
            }
        }

        private void Update()
        {
            var sounds = _audio.Sounds;
            ListenToPans(sounds);
            GiveVoicesToNewPans();
            foreach (var voice in _voices)
                UpdateVoice(voice, sounds);
        }

        private void ListenToPans(SoundBank sounds)
        {
            var services = _root.Services;
            // Nothing cooks between shifts, so nothing sizzles either.
            var phase = services.Days.Phase;
            var cooking = phase == DayPhase.Prep || phase == DayPhase.Service;

            _heard.Clear();
            var pans = services.Cooking.Pans;
            foreach (var pan in pans)
            {
                var hasFood = pan.Contents.Count > 0;
                var level = cooking && hasFood ? SoundMath.SizzleLevel(pan.Temperature, sounds.SizzleFrom, sounds.SizzleFull) : 0f;
                // Exactly at the threshold still counts as sizzling, below it doesn't.
                var sizzling = cooking && hasFood && pan.Temperature >= sounds.SizzleFrom;
                PlayHitIfFoodMoved(pan, sizzling, sounds);
                if (sizzling)
                    _heard.Add((pan, Mathf.Max(level, Mathf.Epsilon)));
            }

            _heard.Sort((a, b) => b.Level.CompareTo(a.Level));
            if (_heard.Count > _voices.Count)
                _heard.RemoveRange(_voices.Count, _heard.Count - _voices.Count);

            ForgetGonePans(pans);
        }

        private void PlayHitIfFoodMoved(Pan pan, bool sizzling, SoundBank sounds)
        {
            var food = pan.Contents.Count > 0 ? pan.Contents[0] : null;
            var side = food != null ? food.DownSide : default;
            _lastSeen.TryGetValue(pan, out var last);
            var moved = food != null && (food != last.Food || side != last.Side);
            _lastSeen[pan] = (food, side);

            if (moved && sizzling)
                _audio.Play(sounds.SizzleHit, PositionOf(pan));
        }

        private void ForgetGonePans(IReadOnlyList<Pan> pans)
        {
            if (_lastSeen.Count <= pans.Count)
                return;

            _gonePans.Clear();
            foreach (var pan in _lastSeen.Keys)
            {
                var stillHere = false;
                foreach (var other in pans)
                    stillHere |= other == pan;
                if (!stillHere)
                    _gonePans.Add(pan);
            }
            foreach (var pan in _gonePans)
                _lastSeen.Remove(pan);
        }

        private void GiveVoicesToNewPans()
        {
            foreach (var (pan, _) in _heard)
            {
                if (HasVoice(pan))
                    continue;

                foreach (var voice in _voices)
                {
                    // A voice still fading out from another pan isn't free yet: taking it would make that pan jump.
                    if (voice.Pan != null && (IsHeard(voice.Pan) || voice.Source.volume > 0f))
                        continue;

                    voice.Pan = pan;
                    if (!voice.Source.isPlaying && voice.Source.clip != null)
                    {
                        // Start somewhere random, so two pans don't play the same crackles together.
                        voice.Source.time = Random.value * voice.Source.clip.length * 0.99f;
                        voice.Source.Play();
                    }
                    break;
                }
            }
        }

        private void UpdateVoice(Voice voice, SoundBank sounds)
        {
            var level = -1f;
            foreach (var (pan, heardLevel) in _heard)
            {
                if (pan == voice.Pan)
                    level = heardLevel;
            }

            var goal = level < 0f ? 0f : SoundMath.SizzleVolume(level, sounds.SizzleQuietVolume, sounds.SizzleLoudVolume);
            var source = voice.Source;
            source.volume = Mathf.MoveTowards(source.volume, goal, sounds.SizzleFadeRate * Time.unscaledDeltaTime);

            if (level >= 0f)
            {
                source.pitch = Mathf.Lerp(sounds.SizzleLowPitch, sounds.SizzleHighPitch, level);
                source.panStereo = _audio.StereoAt(PositionOf(voice.Pan));
            }
            else if (source.volume <= 0f)
            {
                voice.Pan = null;
                if (source.isPlaying)
                    source.Stop();
            }
        }

        private bool HasVoice(Pan pan)
        {
            foreach (var voice in _voices)
            {
                if (voice.Pan == pan)
                    return true;
            }
            return false;
        }

        private bool IsHeard(Pan pan)
        {
            foreach (var (heard, _) in _heard)
            {
                if (heard == pan)
                    return true;
            }
            return false;
        }

        private Vector3 PositionOf(Pan pan) =>
            _root.Services.ItemViews.TryGetView(pan, out var view) ? view.transform.position : transform.position;
    }
}
