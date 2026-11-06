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
        [Tooltip("Canvas pixels from the centre of the screen: the money is on the other side.")]
        [SerializeField, Min(0f)] private float _besideMoney = 6f;
        [Tooltip("Canvas pixels above the bottom of the screen for the small Prep strip.")]
        [SerializeField, Min(0f)] private float _stripBottomMargin = 14f;
        [SerializeField, Min(8f)] private float _stripFontSize = 18f;
        [SerializeField] private Vector2 _stripPadding = new Vector2(14f, 6f);
        [Tooltip("Real seconds the morning's delivery stays on the Prep strip.")]
        [SerializeField, Min(0f)] private float _deliverySeconds = 6f;
        [SerializeField, Min(8f)] private float _clockFontSize = 26f;
        [SerializeField, Min(8f)] private float _cardFontSize = 26f;
        [SerializeField] private Vector2 _clockPadding = new Vector2(16f, 6f);
        [SerializeField] private Vector2 _cardPadding = new Vector2(36f, 24f);

        [Header("Text")]
        [SerializeField] private string _joinText = "Press A to join";
        [SerializeField] private string _candidatesText = "Looking for work:";
        [SerializeField] private string _manageText = "Press Select to rearrange the kitchen";
        [SerializeField] private string _deliveryText = "Delivered to storage:";
        [SerializeField] private string _prepText = "Prep   [Select] rearrange   [Menu] open up";
        [SerializeField] private string _readyText = "Press Menu when ready";
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
        private WorldLabel _strip;
        private float _prepStartedAt = -1f;
        private string _summary;
        private int _summaryDay;

        private void Start()
        {
            _clock = WorldLabel.Create(_style, _canvas, "ServiceClock", _clockPadding, new Vector2(1f, 1f));
            _clock.FontSize = _clockFontSize;
            _card = WorldLabel.Create(_style, _canvas, "ServiceCard", _cardPadding, new Vector2(0.5f, 0.5f));
            _card.FontSize = _cardFontSize;
            _strip = WorldLabel.Create(_style, _canvas, "PrepStrip", _stripPadding, new Vector2(0.5f, 0f));
            _strip.FontSize = _stripFontSize;
        }

        private void LateUpdate()
        {
            if (_clock == null || _root.Services == null)
                return;

            var days = _root.Services.Days;
            var service = _root.Services.Service;
            var top = _canvas.rect.height / 2f;

            if (days.Phase == DayPhase.Prep || days.Phase == DayPhase.Service)
                _clock.ShowAt(ClockText(service), new Vector2(-_besideMoney, top - _clockTopMargin));
            else
                _clock.Hide();

            if (days.Phase != DayPhase.Prep)
            {
                _prepStartedAt = -1f;
                _strip.Hide();
            }
            else if (_prepStartedAt < 0f)
            {
                _prepStartedAt = Time.unscaledTime;
            }

            if (PlayerInput.all.Count == 0)
            {
                _card.ShowAt(_joinText, Vector2.zero);
                return;
            }

            // Rearranging the kitchen needs to see all of it.
            if (PlayerInput.all.Any(p => p.TryGetComponent(out PlayerInteractor interactor) && interactor.IsManaging))
            {
                _card.Hide();
                _strip.Hide();
                return;
            }

            switch (days.Phase)
            {
                case DayPhase.Plan:
                    _card.ShowAt(WithReady(PlanText(days)), Vector2.zero);
                    break;
                case DayPhase.Prep:
                    // Players work through this phase and need to see the kitchen: a strip, not a card.
                    _card.Hide();
                    _strip.ShowAt(PrepText(), new Vector2(0f, -_canvas.rect.height / 2f + _stripBottomMargin));
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
            var text = _prepText;
            if (PlayerInput.all.Count > 1)
                text += $"\n{ReadyLine()}";

            // Just long enough to read: the stock shows on the storage itself.
            if (services.LastDelivery.Count > 0 && Time.unscaledTime - _prepStartedAt < _deliverySeconds)
            {
                var parts = services.LastDelivery.Select(d => $"{d.Count} {services.Catalog.DisplayName(d.TypeId)}");
                text += $"\n{_deliveryText} {string.Join(", ", parts)}";
            }
            return text;
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
                plan.AppendLine($"{member.Name}, {RoleName(member.Role)}: {member.Description}, {Mood(member)}, {currency}{member.DailyWage:0}/day   {Button(PromptControl.East)} let go");
            if (staff.Hired.Count > 0)
                plan.AppendLine();

            plan.AppendLine(_candidatesText);
            for (var i = 0; i < staff.Candidates.Count && i < HireButtons.Length; i++)
            {
                var candidate = staff.Candidates[i];
                var line = $"{Button(HireButtons[i])}  {candidate.Name}, {RoleName(candidate.Role)}: {candidate.Description}, {currency}{candidate.DailyWage:0}/day";
                plan.AppendLine(staff.CanHire(candidate, days.Balance) ? line : HudStyle.Colored(line, _waitingColor));
            }
            plan.AppendLine();
            plan.AppendLine(_manageText);
            return plan.ToString().TrimEnd();
        }

        // A button as an Xbox controller shows it: players are only ever shown those.
        private static string Button(PromptControl control) =>
            $"[{HudStyle.Glyph(control, true)}]";

        // Words, not numbers, like the candidates' descriptions. New staff haven't settled in yet.
        private static string Mood(StaffMember member)
        {
            if (member.Morale < 0f)
                return "new";
            if (member.Morale >= 0.75f)
                return HudStyle.Colored("happy", new Color(0.45f, 0.85f, 0.40f));
            if (member.Morale >= 0.45f)
                return "doing fine";
            if (member.Morale >= 0.25f)
                return HudStyle.Colored("unhappy", new Color(0.96f, 0.76f, 0.25f));
            return HudStyle.Colored("fed up", new Color(0.95f, 0.30f, 0.26f));
        }

        private static string RoleName(StaffRole role) => role switch
        {
            StaffRole.PrepAssistant => "helper",
            StaffRole.Waiter => "waiter",
            _ => "cleaner",
        };

        private static string WorkDone(StaffMember member) => member.Role switch
        {
            StaffRole.PrepAssistant => $"{member.Name} prepared {member.JobsToday}, dropped {member.MistakesToday}",
            StaffRole.Waiter => $"{member.Name} carried {member.JobsToday} plates, dropped {member.MistakesToday}",
            _ => $"{member.Name} washed {member.JobsToday}, broke {member.MistakesToday}",
        };

        // The button prompt, plus who's ready when there's more than one player. (With one player,
        // being ready moves the day on at once, so there's nothing to show.)
        private string WithReady(string text) =>
            PlayerInput.all.Count <= 1 ? $"{text}\n{_readyText}" : $"{text}\n{_readyText}\n{ReadyLine()}";

        private string ReadyLine()
        {
            _text.Clear();
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
                _text.AppendLine($"{WorkDone(member)}, {Mood(member)}");
            foreach (var member in _root.Services.Staff.LastQuit)
                _text.AppendLine(HudStyle.Colored($"{WorkDone(member)}, and quit", _badColor));
            if (days.LastWages > 0f)
                _text.AppendLine($"Wages -{currency}{days.LastWages:0}");
            _text.Append($"Balance {FormatMoney(days.Balance, currency)}");
            return _text.ToString();
        }
    }
}
