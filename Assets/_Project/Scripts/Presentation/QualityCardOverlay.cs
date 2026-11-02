using System.Collections.Generic;
using System.Text;
using Game.Gameplay.Stations;
using Game.Simulation.Quality;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Pops a quality card above the pass when a dish is served: stars and a line per item with
    /// its doneness and how it scored on doneness, crust, serving temperature and freshness.
    /// Stands in for customers' reactions until Phase 2. Greybox UI (IMGUI).
    /// </summary>
    public sealed class QualityCardOverlay : MonoBehaviour
    {
        // Height above the pass where the card is anchored.
        private const float AnchorHeight = 1.4f;

        [Tooltip("Seconds a card stays up.")]
        [SerializeField, Min(0.5f)] private float _duration = 5f;
        [SerializeField, Min(8)] private int _fontSize = 14;
        [SerializeField, Min(100f)] private float _width = 340f;

        private readonly List<(PassStation Pass, DishScore Score, float Until)> _cards = new List<(PassStation, DishScore, float)>();
        private readonly StringBuilder _text = new StringBuilder();
        private PassStation[] _passes;
        private GUIStyle _style;

        private void Start()
        {
            _passes = FindObjectsByType<PassStation>(FindObjectsSortMode.None);
            foreach (var pass in _passes)
                pass.Served += HandleServed;
        }

        private void OnDestroy()
        {
            if (_passes == null)
                return;
            foreach (var pass in _passes)
                pass.Served -= HandleServed;
        }

        private void HandleServed(PassStation pass, DishScore score)
        {
            _cards.RemoveAll(card => card.Pass == pass);
            _cards.Add((pass, score, Time.time + _duration));
        }

        private void OnGUI()
        {
            _cards.RemoveAll(card => Time.time > card.Until);
            var camera = Camera.main;
            if (_cards.Count == 0 || camera == null)
                return;

            _style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, fontSize = _fontSize, wordWrap = true };

            foreach (var card in _cards)
            {
                var screen = camera.WorldToScreenPoint(card.Pass.transform.position + Vector3.up * AnchorHeight);
                if (screen.z <= 0f)
                    continue;

                var content = new GUIContent(Describe(card.Score));
                var height = _style.CalcHeight(content, _width);
                var rect = new Rect(screen.x - _width / 2f, Screen.height - screen.y - height, _width, height);
                GUI.Box(rect, content, _style);
            }
        }

        private string Describe(DishScore score)
        {
            _text.Clear();
            _text.AppendLine($"<b>{score.Stars:0.#} / 5 stars</b>   ({score.Overall:P0})");
            foreach (var item in score.Items)
            {
                _text.Append($"<b>{item.Ingredient.TypeId}</b>");
                if (item.DonenessName != null)
                    _text.Append($" — {item.DonenessName}");
                _text.AppendLine();

                _text.Append("   ");
                Part("doneness", item.Doneness);
                Part("crust", item.Crust);
                Part("hot", item.Temperature);
                Part("fresh", item.Freshness);
                _text.AppendLine();
            }
            return _text.ToString().TrimEnd();
        }

        private void Part(string name, float? value)
        {
            if (!value.HasValue)
                return;
            var colour = value.Value >= 0.8f ? "#9fe39f" : value.Value >= 0.5f ? "#f0d080" : "#f08070";
            _text.Append($"{name} <color={colour}>{value.Value:P0}</color>   ");
        }
    }
}
