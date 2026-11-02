using System.Collections.Generic;
using Game.Gameplay.Stations;
using Game.Simulation.Items;
using Game.Simulation.Prep;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation
{
    /// <summary>
    /// Rhythm feedback while chopping at a prep counter, so players can tell they're on the beat:
    /// a beat light that pulses in time, a progress bar of cuts, and the last chop's grade
    /// (Good / Early / Late / Miss) in colour. Shown when a chef holding the knife faces a counter
    /// with something to cut. Greybox UI (IMGUI).
    /// </summary>
    public sealed class ChopRhythmOverlay : MonoBehaviour
    {
        // Anchored just above the counter top so it sits below the station's button prompt.
        private const float AnchorHeight = 0.95f;

        [SerializeField, Min(8)] private int _fontSize = 16;
        [Tooltip("Seconds the last chop's grade stays visible.")]
        [SerializeField, Min(0f)] private float _gradeDuration = 0.45f;
        [SerializeField] private Color _good = new Color(0.55f, 0.9f, 0.55f);
        [SerializeField] private Color _okay = new Color(0.95f, 0.8f, 0.4f);
        [SerializeField] private Color _miss = new Color(0.95f, 0.45f, 0.4f);

        private readonly Dictionary<PrepStation, (ChopGrade Grade, float At)> _lastChop = new Dictionary<PrepStation, (ChopGrade, float)>();
        private PrepStation[] _stations;
        private GUIStyle _label;
        private Texture2D _white;

        private void Start()
        {
            _stations = FindObjectsByType<PrepStation>(FindObjectsSortMode.None);
            foreach (var station in _stations)
            {
                var captured = station;
                station.Chopped += grade => _lastChop[captured] = (grade, Time.unscaledTime);
            }
            _white = Texture2D.whiteTexture;
        }

        private void OnGUI()
        {
            var camera = Camera.main;
            if (camera == null || _stations == null)
                return;

            _label ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = _fontSize, fontStyle = FontStyle.Bold };

            foreach (var station in _stations)
            {
                var counter = station.Counter;
                if (counter == null || !counter.CanChop || !KnifeChefFacing(station))
                    continue;

                var screen = camera.WorldToScreenPoint(station.transform.position + Vector3.up * AnchorHeight);
                if (screen.z <= 0f)
                    continue;
                var centre = new Vector2(screen.x, Screen.height - screen.y);
                Draw(station, counter, centre);
            }
        }

        private void Draw(PrepStation station, PrepCounter counter, Vector2 centre)
        {
            // Beat light: brightest and largest on the beat, fading through the beat.
            var phase = counter.Rhythm?.BeatPhase(Time.unscaledTimeAsDouble);
            var pulse = phase.HasValue ? 1f - (float)phase.Value : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            var size = Mathf.Lerp(14f, 30f, pulse);
            DrawRect(new Rect(centre.x - size / 2f, centre.y + 18f - size / 2f, size, size), new Color(1f, 1f, 1f, 0.35f + 0.65f * pulse));

            // Progress: one square per cut needed, filled as chops land.
            const float cell = 12f;
            const float gap = 4f;
            var total = counter.CutsNeeded;
            var width = total * cell + (total - 1) * gap;
            for (var i = 0; i < total; i++)
            {
                var rect = new Rect(centre.x - width / 2f + i * (cell + gap), centre.y + 40f, cell, cell);
                DrawRect(rect, i < counter.Cuts ? Color.white : new Color(1f, 1f, 1f, 0.25f));
            }

            // Last grade, or how to start.
            string text;
            Color colour;
            if (_lastChop.TryGetValue(station, out var last) && Time.unscaledTime - last.At < _gradeDuration && counter.Cuts > 0)
            {
                text = last.Grade switch
                {
                    ChopGrade.Good => "Good!",
                    ChopGrade.Early => "Early",
                    ChopGrade.Late => "Late",
                    _ => "Miss",
                };
                colour = last.Grade == ChopGrade.Good ? _good : last.Grade == ChopGrade.Miss ? _miss : _okay;
            }
            else
            {
                text = counter.Cuts == 0 ? "chop to the beat" : string.Empty;
                colour = Color.white;
            }

            var previous = GUI.contentColor;
            GUI.contentColor = colour;
            GUI.Label(new Rect(centre.x - 100f, centre.y + 56f, 200f, 24f), text, _label);
            GUI.contentColor = previous;
        }

        private void DrawRect(Rect rect, Color colour)
        {
            var previous = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(rect, _white);
            GUI.color = previous;
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
