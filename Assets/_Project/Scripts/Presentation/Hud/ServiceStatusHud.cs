using System.Text;
using Game.Gameplay;
using Game.Simulation.Days;
using Game.Simulation.Service;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The service's progress on screen: how to join, the Plan and Prep cards, the clock with
    /// last orders during service, and the Review afterwards.
    /// </summary>
    public sealed class ServiceStatusHud : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private DayLoop _dayLoop;

        [Header("Layout")]
        [Tooltip("Canvas pixels below the top of the screen for the clock.")]
        [SerializeField, Min(0f)] private float _clockTopMargin = 16f;
        [Tooltip("Canvas pixels from the top of the screen to the centre of the Prep card.")]
        [SerializeField, Min(0f)] private float _prepCardTopMargin = 150f;
        [SerializeField, Min(8f)] private float _clockFontSize = 26f;
        [SerializeField, Min(8f)] private float _cardFontSize = 26f;
        [SerializeField] private Vector2 _clockPadding = new Vector2(16f, 6f);
        [SerializeField] private Vector2 _cardPadding = new Vector2(36f, 24f);

        [Header("Text")]
        [SerializeField] private string _joinText = "Press A to join\n(keyboard: X)";
        [SerializeField] private string _planText = "Nothing to plan yet: hiring arrives soon";
        [SerializeField] private string _openText = "Prep: preheat and get ready, then open up";
        [SerializeField] private string _readyText = "Press Menu (keyboard: Esc) when ready";
        [SerializeField] private string _lastOrdersText = "Last orders";
        [SerializeField] private string _closedText = "Closed: finish up";
        [SerializeField] private Color _noticeColor = new Color(0.96f, 0.76f, 0.25f);
        [SerializeField] private Color _goodColor = new Color(0.45f, 0.85f, 0.40f);
        [SerializeField] private Color _badColor = new Color(0.95f, 0.30f, 0.26f);

        private readonly StringBuilder _text = new StringBuilder();
        private WorldLabel _clock;
        private WorldLabel _card;
        private string _summary;
        private int _summaryDay;

        private void Start()
        {
            _clock = WorldLabel.Create(_style, _canvas, "ServiceClock", _clockPadding, new Vector2(0.5f, 1f));
            _clock.FontSize = _clockFontSize;
            _card = WorldLabel.Create(_style, _canvas, "ServiceCard", _cardPadding, new Vector2(0.5f, 0.5f));
            _card.FontSize = _cardFontSize;
        }

        private void LateUpdate()
        {
            if (_clock == null || _root.Services == null)
                return;

            var days = _root.Services.Days;
            var service = _root.Services.Service;
            var top = _canvas.rect.height / 2f;

            if (days.Phase == DayPhase.Prep || days.Phase == DayPhase.Service)
                _clock.ShowAt(ClockText(service), new Vector2(0f, top - _clockTopMargin));
            else
                _clock.Hide();

            if (PlayerInput.all.Count == 0)
            {
                _card.ShowAt(_joinText, Vector2.zero);
                return;
            }

            switch (days.Phase)
            {
                case DayPhase.Plan:
                    _card.ShowAt(WithReady(PlanText(days)), Vector2.zero);
                    break;
                case DayPhase.Prep:
                    // Up out of the way: this is a phase players work through.
                    _card.ShowAt(WithReady(_openText), new Vector2(0f, top - _prepCardTopMargin));
                    break;
                case DayPhase.Review:
                    if (_summaryDay != days.Day)
                    {
                        _summaryDay = days.Day;
                        _summary = Describe(days);
                    }
                    _card.ShowAt(WithReady(_summary), Vector2.zero);
                    break;
                default:
                    _card.Hide();
                    break;
            }
        }

        private string PlanText(DayCycle days)
        {
            var currency = _root.Services.ServiceConfig.Currency;
            return $"<b>Day {days.Day}</b>\nBalance {FormatMoney(days.Balance, currency)}\n\n{_planText}";
        }

        // The button prompt, plus who's ready when there's more than one player.
        private string WithReady(string text)
        {
            var players = PlayerInput.all.Count;
            var ready = _dayLoop.Ready.ReadyCount;
            return players > 1
                ? $"{text}\n{_readyText}  ({ready}/{players} ready)"
                : $"{text}\n{_readyText}";
        }

        private static string FormatMoney(float money, string currency) =>
            (money < 0f ? "-" : string.Empty) + $"{currency}{Mathf.Abs(money):0}";

        private string ClockText(ServiceSimulation service)
        {
            var minutes = _root.Services.ServiceConfig.OpensAt * 60f + service.Elapsed / 60f;
            var clock = $"{(int)(minutes / 60f) % 24:00}:{(int)minutes % 60:00}";
            return service.Phase switch
            {
                ServicePhase.LastOrders => $"{clock}  {HudStyle.Colored(_lastOrdersText, _noticeColor)}",
                ServicePhase.Closed => $"{clock}  {HudStyle.Colored(_closedText, _noticeColor)}",
                _ => clock,
            };
        }

        private string Describe(DayCycle days)
        {
            var summary = days.LastService;
            var currency = _root.Services.ServiceConfig.Currency;
            _text.Clear();
            _text.AppendLine($"<b>Day {days.Day}: service over</b>");
            _text.AppendLine();

            var money = FormatMoney(summary.Money, currency);
            _text.AppendLine($"<b>{HudStyle.Colored(money, summary.Money < 0f ? _badColor : _goodColor)}</b>");
            _text.AppendLine($"{currency}{summary.Payments:0} meals  +  {currency}{summary.Tips:0} tips  -  {currency}{summary.WalkOutLosses:0} walk-outs");
            _text.AppendLine();

            if (summary.Rating.HasValue)
                _text.AppendLine(HudStyle.Colored($"{summary.Rating.Value:0.0} stars", _style.RatingColor(summary.Rating.Value / 5f)) + $" from {summary.ReviewCount} reviews");
            _text.AppendLine($"Served {summary.Served}  ({summary.AsOrdered} as ordered)");
            _text.AppendLine($"Walked out {summary.WalkOuts}  ·  Turned away {summary.TurnedAway}");

            if (summary.Best != null || summary.TopComplaint != null)
                _text.AppendLine();
            if (summary.Best != null && !string.IsNullOrEmpty(summary.Best.Line))
                _text.AppendLine($"Best: \"{summary.Best.Line}\"");
            if (summary.TopComplaint != null && !string.IsNullOrEmpty(summary.TopComplaint.Line))
                _text.AppendLine($"Heard {summary.TopComplaintCount}x: \"{summary.TopComplaint.Line}\"");

            _text.AppendLine();
            _text.Append($"Balance {FormatMoney(days.Balance, currency)}");
            return _text.ToString();
        }
    }
}
