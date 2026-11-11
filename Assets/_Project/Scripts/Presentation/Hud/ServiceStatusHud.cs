using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Simulation.Days;
using Game.Simulation.Service;
using Game.Simulation.Staff;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The service's progress on screen: how to join, the morning's delivery and the lost-controller
    /// pause. The day, phase, clock and tonight's guests are on the day card (DayCardHud); who's
    /// ready in Prep is the ready bar's (PrepReadyBarHud, the user's design 2026-11-11); Plan and
    /// Review are the forkcast dashboard's (ReviewDashboard, the user's call 2026-11-10).
    /// </summary>
    public sealed class ServiceStatusHud : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private DayLoop _dayLoop;
        [SerializeField] private CoopSettings _settings;

        [Header("Layout")]
        // The ready bar's height (PrepReadyBarHud), which the delivery line sits above.
        private const float ReadyBarHeight = 62f;

        [Tooltip("Canvas pixels above the ready bar for the morning's delivery.")]
        [SerializeField, Min(0f)] private float _stripBottomMargin = 14f;
        [SerializeField, Min(8f)] private float _stripFontSize = 28f;
        [SerializeField] private Vector2 _stripPadding = new Vector2(14f, 6f);
        [Tooltip("Real seconds the morning's delivery stays on the Prep strip.")]
        [SerializeField, Min(0f)] private float _deliverySeconds = 6f;
        [SerializeField, Min(8f)] private float _cardFontSize = 32f;
        [SerializeField] private Vector2 _cardPadding = new Vector2(36f, 24f);

        [Header("Text")]
        [SerializeField] private string _joinText = "Press A to join";
        [SerializeField] private string _deliveryText = "Delivered to storage:";
        [SerializeField] private string _pausedText = "Paused: {players}, reconnect your controller";
        [SerializeField] private string _carryOnText = "Press Menu to carry on without them";
        [SerializeField] private Color _waitingColor = new Color(0.60f, 0.62f, 0.66f);
        [SerializeField] private Color _goodColor = new Color(0.45f, 0.85f, 0.40f);
        [SerializeField] private Color _badColor = new Color(0.95f, 0.30f, 0.26f);

        private WorldLabel _card;
        private WorldLabel _strip;
        private float _prepStartedAt = -1f;

        private void Start()
        {
            _card = WorldLabel.Create(_style, _canvas, "ServiceCard", _cardPadding, new Vector2(0.5f, 0.5f));
            _card.FontSize = _cardFontSize;
            _strip = WorldLabel.Create(_style, _canvas, "PrepStrip", _stripPadding, new Vector2(0.5f, 0f));
            _strip.FontSize = _stripFontSize;
        }

        private void LateUpdate()
        {
            if (_card == null || _root.Services == null)
                return;

            var days = _root.Services.Days;

            if (days.Phase != DayPhase.Prep)
            {
                _prepStartedAt = -1f;
                _strip.Hide();
            }
            else if (_prepStartedAt < 0f)
            {
                _prepStartedAt = Time.unscaledTime;
            }

            // forkcast is up (Review and Plan): it says everything, joining included.
            if (_root.Services.Forkcast.IsOpen)
            {
                _card.Hide();
                return;
            }

            if (PlayerInput.all.Count == 0)
            {
                _card.ShowAt(Tr.Get(_joinText), Vector2.zero);
                return;
            }

            // Rearranging the kitchen needs to see all of it.
            if (PlayerInput.all.Any(p => p.TryGetComponent(out PlayerInteractor interactor) && interactor.IsManaging))
            {
                _card.Hide();
                _strip.Hide();
                return;
            }

            // A lost controller during service: everything waits, and this says why and what to do.
            if (days.Phase == DayPhase.Service && _dayLoop.ControllerPause.Paused)
            {
                _card.ShowAt(PausedText(), Vector2.zero);
                return;
            }

            switch (days.Phase)
            {
                case DayPhase.Prep:
                    // Players work through this phase and need to see the kitchen: no card.
                    _card.Hide();
                    ShowDelivery();
                    break;
                default:
                    _card.Hide();
                    break;
            }
        }

        // The morning's delivery, for a few seconds, just above the ready bar.
        private void ShowDelivery()
        {
            var services = _root.Services;
            if (_prepStartedAt >= 0f && services.LastDelivery.Count > 0 && Time.unscaledTime - _prepStartedAt < _deliverySeconds)
            {
                var parts = services.LastDelivery.Select(d => Tr.Format("{count} {what}", ("count", d.Count), ("what", services.Catalog.DisplayName(d.TypeId))));
                var bottom = -_canvas.rect.height / 2f + _stripBottomMargin;
                _strip.ShowAt($"{Tr.Get(_deliveryText)} {string.Join(", ", parts)}", new Vector2(0f, bottom + ReadyBarHeight));
            }
            else
            {
                _strip.Hide();
            }
        }

        // Words, not numbers, like the candidates' descriptions. New staff haven't settled in yet.
        internal static string Mood(StaffMember member)
        {
            if (member.Morale < 0f)
                return Tr.Get("new");
            if (member.Morale >= 0.75f)
                return HudStyle.Colored(Tr.Get("happy"), new Color(0.45f, 0.85f, 0.40f));
            if (member.Morale >= 0.45f)
                return Tr.Get("doing fine");
            if (member.Morale >= 0.25f)
                return HudStyle.Colored(Tr.Get("unhappy"), new Color(0.96f, 0.76f, 0.25f));
            return HudStyle.Colored(Tr.Get("fed up"), new Color(0.95f, 0.30f, 0.26f));
        }

        internal static string RoleName(StaffRole role) => role switch
        {
            StaffRole.PrepAssistant => Tr.Get("helper"),
            StaffRole.Waiter => Tr.Get("waiter"),
            _ => Tr.Get("cleaner"),
        };

        internal static string WorkDone(StaffMember member) => member.Role switch
        {
            StaffRole.PrepAssistant => Tr.Format("{name} prepared {jobs}, dropped {mistakes}", ("name", member.Name), ("jobs", member.JobsToday), ("mistakes", member.MistakesToday)),
            StaffRole.Waiter => Tr.Format("{name} carried {jobs} plates, dropped {mistakes}", ("name", member.Name), ("jobs", member.JobsToday), ("mistakes", member.MistakesToday)),
            _ => Tr.Format("{name} washed {jobs}, broke {mistakes}", ("name", member.Name), ("jobs", member.JobsToday), ("mistakes", member.MistakesToday)),
        };

        private string PausedText()
        {
            var waiting = _dayLoop.ControllerPause.WaitingFor;
            var names = string.Join(", ", waiting.Select(index => HudStyle.Colored($"P{index + 1}", _settings.GetPlayerColor(index))));
            var text = $"<b>{Tr.Format(_pausedText, ("players", names))}</b>";
            // Only someone still playing can carry on, so with nobody left the second line would lie.
            return waiting.Count < PlayerInput.all.Count ? $"{text}\n{Tr.Get(_carryOnText)}" : text;
        }

        // In the player's language and currency (Locale).
        private static string FormatMoney(float money) => Game.Data.Locale.Money(money, Game.Data.MoneyStyle.Whole);
    }
}
