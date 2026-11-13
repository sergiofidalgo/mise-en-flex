using System.Collections.Generic;
using Game.Data;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Plays one-off sounds from the <see cref="SoundBank"/> through a small pool of 2D sources. One per
    /// scene: <see cref="KitchenAudio"/> in the kitchens, <see cref="OfficeAudio"/> on the boss's floor.
    /// Menus find whichever is there.
    /// </summary>
    public abstract class SfxPlayer : MonoBehaviour
    {
        [SerializeField] private SoundBank _sounds;
        [Tooltip("How many one-off sounds can overlap. If they all ring at once, the oldest gets cut off.")]
        [SerializeField, Min(1)] private int _voices = 12;

        private sealed class CueHistory
        {
            public float? LastPlayed;
            public int LastTake = -1;
        }

        private readonly Dictionary<SoundCue, CueHistory> _history = new Dictionary<SoundCue, CueHistory>();
        private AudioSource[] _pool;
        private int _nextToSteal;

        public SoundBank Sounds => _sounds;

        protected virtual void Awake()
        {
            _pool = new AudioSource[_voices];
            for (var i = 0; i < _pool.Length; i++)
                _pool[i] = NewSource(gameObject);
        }

        /// <summary>A 2D source: see <see cref="SoundMath.StereoPan"/> for why.</summary>
        public static AudioSource NewSource(GameObject owner)
        {
            var source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        /// <summary>Plays one take of the cue, unless it played too recently.</summary>
        /// <param name="position">Where in the world it comes from, for stereo.</param>
        /// <param name="pitchScale">Multiplies its pitch, on top of the cue's own variation.</param>
        public void Play(SoundCue cue, Vector3 position, float pitchScale = 1f) => PlayPanned(cue, StereoAt(position), pitchScale, false);

        /// <summary>For what has no place in the room, like a table's bill being settled.</summary>
        public void PlayCentred(SoundCue cue, float pitchScale = 1f) => PlayPanned(cue, 0f, pitchScale, false);

        /// <summary>
        /// A menu's own sounds. They play even while the pause menu has paused everything else
        /// (AudioListener.pause), or moving through it would be silent.
        /// </summary>
        public void PlayUi(SoundCue cue) => PlayPanned(cue, 0f, 1f, true);

        private void PlayPanned(SoundCue cue, float stereo, float pitchScale, bool ignorePause)
        {
            if (cue == null || cue.Clips.Length == 0 || _pool == null)
                return;

            if (!_history.TryGetValue(cue, out var history))
                _history[cue] = history = new CueHistory();
            var now = Time.unscaledTime;
            if (!SoundMath.CanPlay(now, history.LastPlayed, cue.MinInterval))
                return;

            var take = SoundMath.NextTake(cue.Clips.Length, history.LastTake, Random.value);
            var clip = cue.Clips[take];
            if (clip == null)
                return;
            history.LastPlayed = now;
            history.LastTake = take;

            var source = FreeSource();
            source.clip = clip;
            source.volume = cue.Volume;
            source.pitch = pitchScale * (1f + Random.Range(-cue.PitchVariation, cue.PitchVariation));
            source.panStereo = stereo;
            source.ignoreListenerPause = ignorePause;
            source.Play();
        }

        /// <summary>Where a sound from this place in the world sits in stereo.</summary>
        public float StereoAt(Vector3 position)
        {
            var camera = Camera.main;
            return camera == null ? 0f : SoundMath.StereoPan(camera.WorldToViewportPoint(position).x, _sounds.StereoWidth);
        }

        private AudioSource FreeSource()
        {
            foreach (var source in _pool)
            {
                if (!source.isPlaying)
                    return source;
            }

            var stolen = _pool[_nextToSteal];
            _nextToSteal = (_nextToSteal + 1) % _pool.Length;
            return stolen;
        }
    }
}
