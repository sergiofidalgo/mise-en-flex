using System;
using System.Collections.Generic;
using Game.Gameplay;
using Game.Gameplay.Debt;
using Game.Gameplay.Stations;
using Game.Simulation.Items;
using Game.Simulation.Quality;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// What the stations themselves say: a muted knock when a press can't do anything, the bin taking
    /// food and giving up its bag, a knife leaving or returning to the rack, a dish landing at a table
    /// with a phrase that says how it went, and furniture being set down or carried off by the boss.
    /// </summary>
    public sealed class StationAudio : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private KitchenAudio _audio;
        [Tooltip("Seconds after the kitchen loads before layout changes make a sound, so setting up stays quiet.")]
        [SerializeField, Min(0f)] private float _settleSeconds = 2f;
        [Tooltip("Pitch of the furniture thump when the boss's men carry something off: lower and heavier.")]
        [SerializeField, Range(0.5f, 1f)] private float _repossessPitch = 0.75f;

        private readonly Dictionary<Interactable, Action> _unsubscribe = new Dictionary<Interactable, Action>();
        private readonly Dictionary<KnifeRackStation, int> _knives = new Dictionary<KnifeRackStation, int>();
        private KitchenServices _services;
        private LayoutEditor _layout;
        private BossVisitDirector _boss;

        private void Start()
        {
            _services = _root.Services;
            foreach (var station in _services.Stations.All)
                HandleAdded(station);
            _services.Stations.Added += HandleAdded;
            _services.Stations.Removed += HandleRemoved;

            _layout = FindFirstObjectByType<LayoutEditor>();
            if (_layout != null)
                _layout.LayoutChanged += HandleLayoutChanged;
            _boss = FindFirstObjectByType<BossVisitDirector>();
            if (_boss != null)
                _boss.Packed += HandlePacked;
        }

        private void OnDestroy()
        {
            if (_services == null)
                return;
            _services.Stations.Added -= HandleAdded;
            _services.Stations.Removed -= HandleRemoved;
            foreach (var unsubscribe in _unsubscribe.Values)
                unsubscribe();
            if (_layout != null)
                _layout.LayoutChanged -= HandleLayoutChanged;
            if (_boss != null)
                _boss.Packed -= HandlePacked;
        }

        private void Update()
        {
            // The rack has no event: a knife taken or put back shows as its count changing.
            foreach (var station in _services.Stations.All)
            {
                if (!(station is KnifeRackStation rack))
                    continue;
                var count = rack.KnifeCount;
                if (_knives.TryGetValue(rack, out var last) && last != count)
                    _audio.Play(_audio.Sounds.Knife, rack.transform.position, count < last ? 1.06f : 0.94f);
                _knives[rack] = count;
            }
        }

        private void HandleAdded(Interactable station)
        {
            if (station == null || _unsubscribe.ContainsKey(station))
                return;

            Action denied = () => _audio.Play(_audio.Sounds.Deny, station.transform.position);
            station.Denied += denied;
            Action unsubscribe = () => station.Denied -= denied;

            if (station is BinStation bin)
            {
                Action<Item> discarded = _ => _audio.Play(_audio.Sounds.BinThunk, bin.transform.position);
                Action emptied = () => _audio.Play(_audio.Sounds.BinBag, bin.transform.position);
                bin.Sink.Discarded += discarded;
                bin.Sink.Emptied += emptied;
                unsubscribe += () =>
                {
                    bin.Sink.Discarded -= discarded;
                    bin.Sink.Emptied -= emptied;
                };
            }

            if (station is ServingStation serving)
            {
                serving.Served += HandleServed;
                unsubscribe += () => serving.Served -= HandleServed;
            }

            _unsubscribe[station] = unsubscribe;
        }

        private void HandleRemoved(Interactable station)
        {
            if (station != null && _unsubscribe.TryGetValue(station, out var unsubscribe))
            {
                unsubscribe();
                _unsubscribe.Remove(station);
            }
            if (station is KnifeRackStation rack)
                _knives.Remove(rack);
        }

        private void HandleServed(ServingStation station, DishScore score)
        {
            var sounds = _audio.Sounds;
            var grade = SoundMath.ServeGrade(score.Stars, sounds.ServeStars);
            var cue = grade == 2 ? sounds.ServeGood : grade == 1 ? sounds.ServeOk : sounds.ServePoor;
            _audio.Play(cue, station.transform.position);
        }

        private void HandleLayoutChanged()
        {
            if (Time.timeSinceLevelLoad >= _settleSeconds)
                _audio.PlayCentred(_audio.Sounds.Furniture);
        }

        private void HandlePacked(Interactable station) =>
            _audio.Play(_audio.Sounds.Furniture, station != null ? station.transform.position : transform.position, _repossessPitch);
    }
}
