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
    /// each button does there (badge + short verb) in that player's colour, always as Xbox buttons
    /// (decision 2026-11-04), whatever the player is using. Two
    /// players at the same station get their cards side by side.
    /// </summary>
    public sealed class PromptHud : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private PromptCard _cardPrefab;
        [SerializeField] private CoopSettings _settings;

        [Tooltip("Metres above a station's base where its card sits (just above the counter top).")]
        [SerializeField, Min(0f)] private float _anchorHeight = 1.25f;
        [Tooltip("Metres above the top of a tall station (a fridge, a shelf) where its card sits, so it doesn't cover what's inside.")]
        [SerializeField, Min(0f)] private float _aboveTallStation = 0.2f;
        [Tooltip("Canvas pixels between two players' cards on the same station.")]
        [SerializeField, Min(0f)] private float _sideBySideGap = 12f;
        [Tooltip("Canvas pixels kept clear at the top of the screen, for the clock and the money.")]
        [SerializeField, Min(0f)] private float _clearAtTheTop = 70f;
        [Tooltip("Canvas pixels kept clear at the other edges of the screen.")]
        [SerializeField, Min(0f)] private float _clearAtTheEdges = 12f;
        [Tooltip("Canvas pixels from a station's centre to a card beside it, when there's no room above.")]
        [SerializeField, Min(0f)] private float _besideStation = 90f;

        private readonly Dictionary<PlayerInput, PromptCard> _cards = new Dictionary<PlayerInput, PromptCard>();
        private readonly List<PromptLine> _lines = new List<PromptLine>();
        private readonly Dictionary<Interactable, int> _sharing = new Dictionary<Interactable, int>();
        private readonly Dictionary<Interactable, float> _tops = new Dictionary<Interactable, float>();

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null)
                return;

            _sharing.Clear();
            foreach (var player in PlayerInput.all)
            {
                var card = CardFor(player);
                if (!player.TryGetComponent(out PlayerInteractor interactor) || !interactor.CanAct || interactor.Target == null)
                {
                    card.Hide();
                    continue;
                }

                _lines.Clear();
                var prompt = interactor.Target as IStationPrompt;
                var diamond = interactor.IsTrayMenuOpen;
                if (diamond)
                {
                    interactor.DescribeTrayMenu(_lines);
                }
                else
                {
                    prompt?.DescribeActions(interactor, _lines);
                    diamond = prompt != null && prompt.PromptAsDiamond(interactor);
                    if (interactor.CanOpenTrayMenu)
                        _lines.Add(new PromptLine(PromptControl.Triggers, Tr.Get("choose from tray")));
                }

                var screen = camera.WorldToScreenPoint(interactor.Target.transform.position + Vector3.up * AnchorHeight(interactor.Target));
                if (_lines.Count == 0 || screen.z <= 0f)
                {
                    card.Hide();
                    continue;
                }

                card.Show(_lines, diamond, gamepad: true,
                    _settings.GetPlayerColor(player.playerIndex));
                LayoutRebuilder.ForceRebuildLayoutImmediate(card.Rect);
                Place(card, interactor.Target, screen);
            }
        }

        // Just above a counter, or above the top of a tall station. Measured once per station.
        private float AnchorHeight(Interactable station)
        {
            if (!_tops.TryGetValue(station, out var top))
            {
                top = 0f;
                foreach (var renderer in station.GetComponentsInChildren<MeshRenderer>())
                    top = Mathf.Max(top, renderer.bounds.max.y - station.transform.position.y);
                _tops[station] = top;
            }
            return Mathf.Max(_anchorHeight, top + _aboveTallStation);
        }

        // Bottom-centre of the card at the station; a second player on the same station sits to the right.
        private void Place(PromptCard card, Interactable station, Vector3 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out var local);
            _sharing.TryGetValue(station, out var already);
            _sharing[station] = already + 1;

            var width = card.Rect.rect.width;
            var offset = already == 0 ? 0f : (width + _sideBySideGap) * already;
            var height = card.Rect.rect.height;
            var position = local + new Vector2(offset - width / 2f, 0f);

            // No room above the station: beside it, so the card doesn't sit over the food.
            var highest = _canvas.rect.height / 2f - _clearAtTheTop - height;
            if (position.y > highest)
            {
                position.y = highest;
                position.x = local.x + offset + _besideStation;
            }

            var right = _canvas.rect.width / 2f - _clearAtTheEdges - width;
            if (position.x > right)
                position.x = local.x - _besideStation - width; // the other side
            position.x = Mathf.Clamp(position.x, -_canvas.rect.width / 2f + _clearAtTheEdges, right);
            position.y = Mathf.Max(position.y, -_canvas.rect.height / 2f + _clearAtTheEdges);
            card.Rect.anchoredPosition = position;
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
