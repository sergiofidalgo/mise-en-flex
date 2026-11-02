using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Stations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// Gives each player a prompt card and keeps it above the station they're facing, showing what
    /// each button does there (badge + short verb) in that player's colour and device glyphs. Two
    /// players at the same station get their cards side by side.
    /// </summary>
    public sealed class PromptHud : MonoBehaviour
    {
        private const string GamepadScheme = "Gamepad";

        [SerializeField] private RectTransform _canvas;
        [SerializeField] private PromptCard _cardPrefab;
        [SerializeField] private CoopSettings _settings;

        [Tooltip("Metres above a station's base where its card sits (just above the counter top).")]
        [SerializeField, Min(0f)] private float _anchorHeight = 1.25f;
        [Tooltip("Canvas pixels between two players' cards on the same station.")]
        [SerializeField, Min(0f)] private float _sideBySideGap = 12f;

        private readonly Dictionary<PlayerInput, PromptCard> _cards = new Dictionary<PlayerInput, PromptCard>();
        private readonly List<PromptLine> _lines = new List<PromptLine>();
        private readonly Dictionary<Interactable, int> _sharing = new Dictionary<Interactable, int>();

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null)
                return;

            _sharing.Clear();
            foreach (var player in PlayerInput.all)
            {
                var card = CardFor(player);
                if (!player.TryGetComponent(out PlayerInteractor interactor) || !(interactor.Target is IStationPrompt prompt))
                {
                    card.Hide();
                    continue;
                }

                _lines.Clear();
                prompt.DescribeActions(interactor, _lines);
                var screen = camera.WorldToScreenPoint(interactor.Target.transform.position + Vector3.up * _anchorHeight);
                if (_lines.Count == 0 || screen.z <= 0f)
                {
                    card.Hide();
                    continue;
                }

                card.Show(_lines, prompt.PromptAsDiamond, player.currentControlScheme == GamepadScheme,
                    _settings.GetPlayerColor(player.playerIndex));
                LayoutRebuilder.ForceRebuildLayoutImmediate(card.Rect);
                Place(card, interactor.Target, screen);
            }
        }

        // Bottom-centre of the card at the station; a second player on the same station sits to the right.
        private void Place(PromptCard card, Interactable station, Vector3 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out var local);
            _sharing.TryGetValue(station, out var already);
            _sharing[station] = already + 1;

            var width = card.Rect.rect.width;
            var offset = already == 0 ? 0f : (width + _sideBySideGap) * already;
            card.Rect.anchoredPosition = local + new Vector2(offset - width / 2f, 0f);
        }

        private PromptCard CardFor(PlayerInput player)
        {
            if (_cards.TryGetValue(player, out var card) && card != null)
                return card;

            card = Instantiate(_cardPrefab, _canvas);
            card.name = $"PromptCard_P{player.playerIndex + 1}";
            card.Hide();
            _cards[player] = card;
            return card;
        }
    }
}
