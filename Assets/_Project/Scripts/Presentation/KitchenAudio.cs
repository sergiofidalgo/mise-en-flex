using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Simulation.Items;
using Game.Simulation.Prep;
using Game.Simulation.Service;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Plays the kitchen's one-off sounds. Listens to the bell, the cutting boards and the customers
    /// itself; other views call <see cref="Play"/>. Nothing in the simulation or gameplay knows sound exists.
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
        private readonly Dictionary<FridgeDoorView, (System.Action Opened, System.Action Closed)> _fridges =
            new Dictionary<FridgeDoorView, (System.Action, System.Action)>();
        private AudioSource[] _pool;
        private int _nextToSteal;
        private KitchenServices _services;
        private StreetDoor _streetDoor;

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

            _streetDoor = FindFirstObjectByType<StreetDoor>();
            _services.ServiceReplaced += HandleServiceReplaced;
            HandleServiceReplaced(null, _services.Service);
        }

        private void OnDestroy()
        {
            if (_services == null)
                return;

            HandleServiceReplaced(_services.Service, null);
            _services.ServiceReplaced -= HandleServiceReplaced;
            _services.Bell.Rang -= HandleBell;
            _services.Stations.Added -= HandleStationAdded;
            _services.Stations.Removed -= HandleStationRemoved;
            foreach (var board in _boards)
                board.Key.Chopped -= board.Value;
            foreach (var fridge in _fridges)
            {
                if (fridge.Key == null)
                    continue;
                fridge.Key.Opened -= fridge.Value.Opened;
                fridge.Key.Closed -= fridge.Value.Closed;
            }
        }

        /// <summary>Plays one take of the cue, unless it played too recently.</summary>
        /// <param name="position">Where in the world it comes from, for stereo.</param>
        /// <param name="pitchScale">Multiplies its pitch, on top of the cue's own variation.</param>
        public void Play(SoundCue cue, Vector3 position, float pitchScale = 1f) => PlayPanned(cue, StereoAt(position), pitchScale);

        // For what has no place in the kitchen, like a table's bill being settled.
        private void PlayCentred(SoundCue cue) => PlayPanned(cue, 0f, 1f);

        private void PlayPanned(SoundCue cue, float stereo, float pitchScale)
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
            source.pitch = pitchScale * (1f + Random.Range(-cue.PitchVariation, cue.PitchVariation));
            source.panStereo = stereo;
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

        // Each day has its own service, so the sounds move over to the new one.
        private void HandleServiceReplaced(ServiceSimulation old, ServiceSimulation next)
        {
            if (old != null)
            {
                old.Arrived -= HandleCustomerArrived;
                old.Paid -= HandleCustomerPaid;
            }
            if (next != null)
            {
                next.Arrived += HandleCustomerArrived;
                next.Paid += HandleCustomerPaid;
            }
        }

        private void HandleCustomerArrived(Customer customer) =>
            Play(_sounds.Chime, _streetDoor != null ? _streetDoor.transform.position : transform.position);

        private void HandleCustomerPaid(Customer customer) => PlayCentred(_sounds.Till);

        private void HandleBell(ItemSlot slot)
        {
            var counter = _services.Stations.All.OfType<CounterStation>().FirstOrDefault(c => c.Slot == slot);
            Play(_sounds.Bell, counter != null ? counter.transform.position : transform.position);
        }

        private void HandleStationAdded(Interactable station)
        {
            if (station.TryGetComponent(out FridgeDoorView fridge) && !_fridges.ContainsKey(fridge))
            {
                System.Action opened = () => Play(_sounds.FridgeOpen, fridge.transform.position);
                System.Action closed = () => Play(_sounds.FridgeClose, fridge.transform.position);
                _fridges[fridge] = (opened, closed);
                fridge.Opened += opened;
                fridge.Closed += closed;
            }

            if (!(station is PrepStation board) || _boards.ContainsKey(board))
                return;

            System.Action<ChopGrade> handler = grade => Play(ChopCue(grade), board.transform.position);
            _boards[board] = handler;
            board.Chopped += handler;
        }

        private void HandleStationRemoved(Interactable station)
        {
            if (station.TryGetComponent(out FridgeDoorView fridge) && _fridges.TryGetValue(fridge, out var handlers))
            {
                fridge.Opened -= handlers.Opened;
                fridge.Closed -= handlers.Closed;
                _fridges.Remove(fridge);
            }

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
