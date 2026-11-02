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
        [Tooltip("Canvas pixels below that point, so the dial sits in front of the counter, under the prompt.")]
        [SerializeField] private float _drop = 80f;

        private readonly Dictionary<PrepStation, ChopDialView> _views = new Dictionary<PrepStation, ChopDialView>();
        private readonly Dictionary<PrepStation, float> _lastChop = new Dictionary<PrepStation, float>();
        private PrepStation[] _stations;

        private void Start()
        {
            _stations = FindObjectsByType<PrepStation>(FindObjectsSortMode.None);
            foreach (var station in _stations)
            {
                var captured = station;
                station.Chopped += _ => _lastChop[captured] = Time.unscaledTime;
                var view = Instantiate(_dialPrefab, _canvas);
                view.name = $"ChopDial_{station.name}";
                view.Hide();
                _views[station] = view;
            }
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null || _stations == null)
                return;

            foreach (var station in _stations)
            {
                var view = _views[station];
                var counter = station.Counter;
                if (counter?.Dial == null || !KnifeChefFacing(station))
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
                var since = _lastChop.TryGetValue(station, out var at) ? Time.unscaledTime - at : float.MaxValue;
                view.Show(counter.Dial, counter.Cuts, counter.CutsNeeded, Time.unscaledTimeAsDouble, since);
            }
        }

        private static bool KnifeChefFacing(PrepStation station)
        {
            foreach (var player in PlayerInput.all)
            {
                if (player.TryGetComponent(out PlayerInteractor interactor) && interactor.Target == station && interactor.Tool is Knife)
                    return true;
            }
            return false;
        }
    }
}
