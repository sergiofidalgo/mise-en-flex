using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Simulation.Items;
using Game.Simulation.Prep;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Plays the kitchen's one-off sounds. Listens to the bell and the cutting boards itself; other
    /// views call <see cref="Play"/>. Nothing in the simulation or gameplay knows sound exists.
    /// </summary>
    public sealed class KitchenAudio : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private SoundBank _sounds;
        [Tooltip("How many one-off sounds can overlap. If they all ring at once, the oldest gets cut off.")]
        [SerializeField, Min(1)] private int _voices = 12;

        private sealed class CueHistory
        {
            public float? LastPlayed;
            public int LastTake = -1;
        }

        private readonly Dictionary<SoundCue, CueHistory> _history = new Dictionary<SoundCue, CueHistory>();
        private readonly Dictionary<PrepStation, System.Action<ChopGrade>> _boards = new Dictionary<PrepStation, System.Action<ChopGrade>>();
        private AudioSource[] _pool;
        private int _nextToSteal;
        private KitchenServices _services;

        public SoundBank Sounds => _sounds;

        private void Awake()
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

        private void Start()
        {
            _services = _root.Services;
            _services.Bell.Rang += HandleBell;
            foreach (var station in _services.Stations.All)
                HandleStationAdded(station);
            _services.Stations.Added += HandleStationAdded;
            _services.Stations.Removed += HandleStationRemoved;
        }

        private void OnDestroy()
        {
            if (_services == null)
                return;

            _services.Bell.Rang -= HandleBell;
            _services.Stations.Added -= HandleStationAdded;
            _services.Stations.Removed -= HandleStationRemoved;
            foreach (var board in _boards)
                board.Key.Chopped -= board.Value;
        }

        /// <summary>Plays one take of the cue, unless it played too recently.</summary>
        /// <param name="position">Where in the world it comes from, for stereo.</param>
        public void Play(SoundCue cue, Vector3 position)
        {
            if (cue == null || cue.Clips.Length == 0)
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
            source.pitch = 1f + Random.Range(-cue.PitchVariation, cue.PitchVariation);
            source.panStereo = StereoAt(position);
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

        private void HandleBell(ItemSlot slot)
        {
            var counter = _services.Stations.All.OfType<CounterStation>().FirstOrDefault(c => c.Slot == slot);
            Play(_sounds.Bell, counter != null ? counter.transform.position : transform.position);
        }

        private void HandleStationAdded(Interactable station)
        {
            if (!(station is PrepStation board) || _boards.ContainsKey(board))
                return;

            System.Action<ChopGrade> handler = grade => Play(ChopCue(grade), board.transform.position);
            _boards[board] = handler;
            board.Chopped += handler;
        }

        private void HandleStationRemoved(Interactable station)
        {
            if (station is PrepStation board && _boards.TryGetValue(board, out var handler))
            {
                board.Chopped -= handler;
                _boards.Remove(board);
            }
        }

        // Rhythm you can hear: a clean knock on the beat, duller either side of it, a skid when it's way off.
        private SoundCue ChopCue(ChopGrade grade)
        {
            switch (grade)
            {
                case ChopGrade.Good:
                    return _sounds.ChopClean;
                case ChopGrade.Early:
                case ChopGrade.Late:
                    return _sounds.ChopOff;
                default:
                    return _sounds.ChopMiss;
            }
        }
    }
}
