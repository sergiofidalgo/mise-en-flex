using System.Linq;
using System.Text;
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
    /// The service's progress on screen: how to join, the Plan and Prep cards, the clock with
    /// last orders during service, and the Review afterwards.
    /// </summary>
    public sealed class ServiceStatusHud : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private DayLoop _dayLoop;
        [SerializeField] private CoopSettings _settings;

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
        [SerializeField] private string _candidatesText = "Looking for work:";
        [SerializeField] private string _deliveryText = "In the storeroom:";
        [SerializeField] private string _openText = "Prep: preheat and get ready, then open up";
        [SerializeField] private string _readyText = "Press Menu (keyboard: Esc) when ready";
        [SerializeField] private string _playerReadyText = "READY";
        [SerializeField] private string _playerWaitingText = "not ready";
        [SerializeField] private Color _waitingColor = new Color(0.60f, 0.62f, 0.66f);
        [SerializeField] private string _lastOrdersText = "Last orders";
        [SerializeField] private string _closedText = "Closed: finish up";
        [SerializeField] private Color _noticeColor = new Color(0.96f, 0.76f, 0.25f);
        [SerializeField] private Color _goodColor = new Color(0.45f, 0.85f, 0.40f);
        [SerializeField] private Color _badColor = new Color(0.95f, 0.30f, 0.26f);

        private static readonly PromptControl[] HireButtons = { PromptControl.South, PromptControl.West, PromptControl.North };

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
                    _card.ShowAt(WithReady(PrepText()), new Vector2(0f, top - _prepCardTopMargin));
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

        private string PrepText()
        {
            var services = _root.Services;
            if (services.LastDelivery.Count == 0)
                return _openText;
            var parts = services.LastDelivery.Select(d => $"{d.Count} {services.Catalog.DisplayName(d.TypeId)}");
            return $"{_openText}\n{_deliveryText} {string.Join(", ", parts)}";
        }

        // Lists who works here and who could; the buttons are HiringMenu's.
        private string PlanText(DayCycle days)
        {
            var currency = _root.Services.ServiceConfig.Currency;
            var staff = _root.Services.Staff;
            var plan = new StringBuilder();
            plan.AppendLine($"<b>Day {days.Day}: plan</b>");
            plan.AppendLine($"Balance {FormatMoney(days.Balance, currency)}");
            plan.AppendLine();

            foreach (var member in staff.Hired)
                plan.AppendLine($"{member.Name}, {RoleName(member.Role)}: {member.Description}, {currency}{member.DailyWage:0}/day   {Button(PromptControl.East)} let go");
            if (staff.Hired.Count > 0)
                plan.AppendLine();

            plan.AppendLine(_candidatesText);
            for (var i = 0; i < staff.Candidates.Count && i < HireButtons.Length; i++)
            {
                var candidate = staff.Candidates[i];
                var line = $"{Button(HireButtons[i])}  {candidate.Name}, {RoleName(candidate.Role)}: {candidate.Description}, {currency}{candidate.DailyWage:0}/day";
                plan.AppendLine(staff.CanHire(candidate, days.Balance) ? line : HudStyle.Colored(line, _waitingColor));
            }
            return plan.ToString().TrimEnd();
        }

        // A button as both devices see it, e.g. "[A / X]".
        private static string Button(PromptControl control) =>
            $"[{HudStyle.Glyph(control, true)} / {HudStyle.Glyph(control, false)}]";

        private static string RoleName(StaffRole role) => role.ToString().ToLowerInvariant();

        // The button prompt, plus who's ready when there's more than one player. (With one player,
        // being ready moves the day on at once, so there's nothing to show.)
        private string WithReady(string text)
        {
            if (PlayerInput.all.Count <= 1)
                return $"{text}\n{_readyText}";

            _text.Clear();
            _text.Append(text).Append('\n').Append(_readyText).Append('\n');
            foreach (var player in PlayerInput.all.OrderBy(p => p.playerIndex))
            {
                var name = $"P{player.playerIndex + 1}";
                _text.Append(_dayLoop.Ready.IsReady(player.playerIndex)
                    ? $"<b>{HudStyle.Colored($"{name} {_playerReadyText}", _settings.GetPlayerColor(player.playerIndex))}</b>   "
                    : HudStyle.Colored($"{name} {_playerWaitingText}", _waitingColor) + "   ");
            }
            return _text.ToString().TrimEnd();
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
            if (days.LastClose.Spoiled > 0 || days.LastClose.LeftOut > 0)
            {
                _text.AppendLine($"Overnight: {days.LastClose.Spoiled} spoiled, {days.LastClose.LeftOut} left out");
                _text.AppendLine();
            }
            foreach (var member in _root.Services.Staff.Hired)
                _text.AppendLine($"{member.Name} washed {member.JobsToday}, broke {member.MistakesToday}");
            if (days.LastWages > 0f)
                _text.AppendLine($"Wages -{currency}{days.LastWages:0}");
            _text.Append($"Balance {FormatMoney(days.Balance, currency)}");
            return _text.ToString();
        }
    }
}
