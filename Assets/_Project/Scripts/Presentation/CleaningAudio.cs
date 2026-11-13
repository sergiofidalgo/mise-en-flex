using System.Collections.Generic;
using Game.Gameplay;
using Game.Gameplay.Cleaning;
using Game.Gameplay.Stations;
using Game.Simulation.Cleaning;
using Game.Simulation.Days;
using Game.Simulation.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation
{
    /// <summary>
    /// Keeping the kitchen clean: the cloth, mop and extinguisher stroke by stroke, a sparkle when a
    /// spot comes clean, water at the sink while anyone washes up (chef or dishwasher), a splash as a
    /// plate goes in, and a soft splat when a new spill lands during the day.
    /// </summary>
    public sealed class CleaningAudio : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private KitchenAudio _audio;
        [Tooltip("Seconds after the kitchen loads before spills make a sound, so a restored mess stays quiet.")]
        [SerializeField, Min(0f)] private float _settleSeconds = 2f;

        private sealed class SinkState
        {
            public Plate Plate;
            public float Progress;
            public float NextStroke;
        }

        private readonly Dictionary<ChefCleaning, float> _nextStroke = new Dictionary<ChefCleaning, float>();
        private readonly Dictionary<SinkStation, SinkState> _sinks = new Dictionary<SinkStation, SinkState>();
        private KitchenServices _services;

        private void Start()
        {
            _services = _root.Services;
            _services.Dirt.Added += HandleDirtAdded;
        }

        private void OnDestroy()
        {
            if (_services != null)
                _services.Dirt.Added -= HandleDirtAdded;
            foreach (var chef in _nextStroke.Keys)
            {
                if (chef != null)
                    chef.Finished -= HandleFinished;
            }
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            foreach (var player in PlayerInput.all)
            {
                if (!player.TryGetComponent(out ChefCleaning chef))
                    continue;
                if (!_nextStroke.ContainsKey(chef))
                {
                    _nextStroke[chef] = 0f;
                    chef.Finished += HandleFinished;
                }
                if (!chef.IsCleaning)
                {
                    _nextStroke[chef] = 0f;
                    continue;
                }
                if (now < _nextStroke[chef])
                    continue;
                _nextStroke[chef] = now + StrokeSeconds(chef.Tool);
                _audio.Play(StrokeCue(chef.Tool), chef.Where);
            }

            foreach (var station in _services.Stations.All)
            {
                if (station is SinkStation sink)
                    ListenToSink(sink, now);
            }
        }

        // Washing is heard from the plate's progress moving, as SinkWashView sees it.
        private void ListenToSink(SinkStation station, float now)
        {
            var sink = station.Sink;
            if (sink == null)
                return;
            if (!_sinks.TryGetValue(station, out var state))
            {
                _sinks[station] = new SinkState { Plate = sink.Washing, Progress = sink.WashProgress };
                return;
            }

            var at = station.transform.position;
            if (sink.Washing != null && sink.Washing != state.Plate)
                _audio.Play(_audio.Sounds.Splash, at);
            var washing = station.Game != null || !Mathf.Approximately(sink.WashProgress, state.Progress);
            state.Plate = sink.Washing;
            state.Progress = sink.WashProgress;
            if (washing && now >= state.NextStroke)
            {
                state.NextStroke = now + _audio.Sounds.WashStrokeSeconds;
                _audio.Play(_audio.Sounds.Wash, at);
            }
        }

        private float StrokeSeconds(CleaningTool tool)
        {
            var seconds = _audio.Sounds.StrokeSeconds;
            return tool == CleaningTool.Mop ? seconds.y : tool == CleaningTool.Extinguisher ? seconds.z : seconds.x;
        }

        private Game.Data.SoundCue StrokeCue(CleaningTool tool) =>
            tool == CleaningTool.Mop ? _audio.Sounds.Mop : tool == CleaningTool.Extinguisher ? _audio.Sounds.Spray : _audio.Sounds.Scrub;

        // A fire put out has its own hiss (HobAudio); this is for dirt wiped clean.
        private void HandleFinished(ChefCleaning chef, Vector3 where)
        {
            if (chef.Fire == null)
                _audio.Play(_audio.Sounds.CleanDone, where);
        }

        private void HandleDirtAdded(DirtSpot spot)
        {
            var phase = _services.Days.Phase;
            if (Time.timeSinceLevelLoad < _settleSeconds || (phase != DayPhase.Prep && phase != DayPhase.Service))
                return;
            _audio.Play(_audio.Sounds.Splat, SpotPosition(spot));
        }

        // On the floor it's in room metres; on a table or counter it's in that host's own axes.
        private static Vector3 SpotPosition(DirtSpot spot) =>
            spot.Host is Component host ? host.transform.TransformPoint(new Vector3(spot.X, 0f, spot.Z)) : new Vector3(spot.X, 0f, spot.Z);
    }
}
