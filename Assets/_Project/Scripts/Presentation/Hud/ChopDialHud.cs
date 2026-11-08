using System.Collections.Generic;
using Game.Gameplay.Stations;
using Game.Simulation.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// Shows a cutting dial over each prep counter while a chef holding the knife faces it with
    /// something on the board to cut.
    /// </summary>
    public sealed class ChopDialHud : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private ChopDialView _dialPrefab;

        [Tooltip("Metres above the station's base the dial is anchored to (the counter top).")]
        [SerializeField, Min(0f)] private float _anchorHeight = 0.9f;
        [Tooltip("Canvas pixels the dial keeps from the screen's edges.")]
        [SerializeField, Min(0f)] private float _screenMargin = 12f;
        [Tooltip("Canvas pixels below that point, so the dial sits in front of the counter, under the prompt.")]
        [SerializeField] private float _drop = 80f;

        private readonly Dictionary<PrepStation, ChopDialView> _views = new Dictionary<PrepStation, ChopDialView>();
        private readonly Dictionary<PrepStation, float> _lastChop = new Dictionary<PrepStation, float>();
        private readonly List<PrepStation> _faced = new List<PrepStation>();
        private readonly List<PrepStation> _gone = new List<PrepStation>();

        // Boards come and go (bought, sold), so a dial is made for one the first time it's needed.
        private ChopDialView ViewFor(PrepStation station)
        {
            if (_views.TryGetValue(station, out var view))
                return view;
            station.Chopped += _ => _lastChop[station] = Time.unscaledTime;
            view = Instantiate(_dialPrefab, _canvas);
            view.name = $"ChopDial_{station.name}";
            _views[station] = view;
            return view;
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null)
                return;

            _faced.Clear();
            foreach (var player in PlayerInput.all)
            {
                // Not while managing: then the target is whatever the layout cursor is over (a board
                // being placed or moved), not a board the chef is at, and the dial stuck to the
                // cursor (the user's bug report, 2026-11-08).
                if (player.TryGetComponent(out PlayerInteractor interactor) && !interactor.IsManaging
                    && interactor.Target is PrepStation board && interactor.Tool is Knife && !_faced.Contains(board))
                    _faced.Add(board);
            }

            _gone.Clear();
            foreach (var pair in _views)
            {
                if (pair.Key == null)
                    _gone.Add(pair.Key);
                else if (!_faced.Contains(pair.Key))
                    pair.Value.Hide();
            }
            foreach (var station in _gone)
            {
                Destroy(_views[station].gameObject);
                _views.Remove(station);
                _lastChop.Remove(station);
            }

            foreach (var station in _faced)
            {
                var view = ViewFor(station);
                var counter = station.Counter;
                if (counter?.Dial == null)
                {
                    view.Hide();
                    continue;
                }

                var screen = camera.WorldToScreenPoint(station.transform.position + Vector3.up * _anchorHeight);
                if (screen.z <= 0f)
                {
                    view.Hide();
                    continue;
                }

                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out var local);
                view.Rect.anchoredPosition = local + Vector2.down * _drop;
                HudBounds.KeepInside(view.Rect, _canvas, _screenMargin);
                var since = _lastChop.TryGetValue(station, out var at) ? Time.unscaledTime - at : float.MaxValue;
                view.Show(counter.Dial, counter.Cuts, counter.CutsNeeded, Time.unscaledTimeAsDouble, since);
            }
        }
    }
}
