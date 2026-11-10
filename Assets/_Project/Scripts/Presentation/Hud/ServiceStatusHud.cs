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
        [Tooltip("Canvas pixels above the bottom of the screen for the small Prep strip.")]
        [SerializeField, Min(0f)] private float _stripBottomMargin = 14f;
        [SerializeField, Min(8f)] private float _stripFontSize = 28f;
        [SerializeField] private Vector2 _stripPadding = new Vector2(14f, 6f);
        [Tooltip("Real seconds the morning's delivery stays on the Prep strip.")]
        [SerializeField, Min(0f)] private float _deliverySeconds = 6f;
        [SerializeField, Min(8f)] private float _clockFontSize = 32f;
        [SerializeField, Min(8f)] private float _cardFontSize = 32f;
        [SerializeField] private Vector2 _clockPadding = new Vector2(16f, 6f);
        [SerializeField] private Vector2 _cardPadding = new Vector2(36f, 24f);

        [Header("Text")]
        [SerializeField] private string _joinText = "Press A to join";
        [SerializeField] private string _candidatesText = "Looking for work:";
        [SerializeField] private string _manageText = "Press Select to rearrange the kitchen";
        [SerializeField] private string _deliveryText = "Delivered to storage:";
        [SerializeField] private string _prepText = "Prep   [Select] rearrange   hold [Menu] open up";
        [SerializeField] private string _readyText = "Hold Menu when ready";
        [SerializeField] private string _playerReadyText = "READY";
        [SerializeField] private string _playerWaitingText = "not ready";
        [SerializeField] private string _playerLostText = "controller lost";
        [SerializeField] private string _pausedText = "Paused: {players}, reconnect your controller";
        [SerializeField] private string _carryOnText = "Press Menu to carry on without them";
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

        private void Start()
        {
            _clock = WorldLabel.Create(_style, _canvas, "ServiceClock", _clockPadding, new Vector2(0.5f, 1f));
            _clock.FontSize = _clockFontSize;
            _clock.Font = _style.DisplayFont;
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
                _clock.ShowAt(ClockText(service), new Vector2(0f, top - _clockTopMargin));
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

            if (_root.Services.Days.Phase == DayPhase.Review)
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
                case DayPhase.Plan:
                    _card.ShowAt(WithReady(PlanText(days)), Vector2.zero);
                    break;
                case DayPhase.Prep:
                    // Players work through this phase and need to see the kitchen: a strip, not a card.
                    _card.Hide();
                    _strip.ShowAt(PrepText(), new Vector2(0f, -_canvas.rect.height / 2f + _stripBottomMargin));
                    break;
                case DayPhase.Review:
                    // The forkcast dashboard shows the day now (decision 2026-11-08).
                    _card.Hide();
                    break;
                default:
                    _card.Hide();
                    break;
            }
        }

        private string PrepText()
        {
            var services = _root.Services;
            var text = Tr.Get(_prepText);
            if (PlayerInput.all.Count > 1)
                text += $"\n{ReadyLine()}";

            // Just long enough to read: the stock shows on the storage itself.
            if (services.LastDelivery.Count > 0 && Time.unscaledTime - _prepStartedAt < _deliverySeconds)
            {
                var parts = services.LastDelivery.Select(d => Tr.Format("{count} {what}", ("count", d.Count), ("what", services.Catalog.DisplayName(d.TypeId))));
                text += $"\n{Tr.Get(_deliveryText)} {string.Join(", ", parts)}";
            }
            return text;
        }

        // Lists who works here and who could; the buttons are HiringMenu's.
        private string PlanText(DayCycle days)
        {
            var staff = _root.Services.Staff;
            var plan = new StringBuilder();
            plan.AppendLine("<b>" + Tr.Format("Day {day}: plan", ("day", days.Day)) + "</b>");
            plan.AppendLine(Tr.Format("Balance {amount}", ("amount", FormatMoney(days.Balance))));
            plan.AppendLine();

            foreach (var member in staff.Hired)
                plan.AppendLine(Tr.Format("{name}, {role}: {description}, {mood}, {wage}/day", ("name", member.Name), ("role", RoleName(member.Role)), ("description", Tr.Get(member.Description)), ("mood", Mood(member)), ("wage", FormatMoney(member.DailyWage)))
                    + $"   {Button(PromptControl.East)} " + Tr.Get("let go"));
            if (staff.Hired.Count > 0)
                plan.AppendLine();

            plan.AppendLine(Tr.Get(_candidatesText));
            for (var i = 0; i < staff.Candidates.Count && i < HireButtons.Length; i++)
            {
                var candidate = staff.Candidates[i];
                var line = $"{Button(HireButtons[i])}  " + Tr.Format("{name}, {role}: {description}, {wage}/day", ("name", candidate.Name), ("role", RoleName(candidate.Role)), ("description", Tr.Get(candidate.Description)), ("wage", FormatMoney(candidate.DailyWage)));
                plan.AppendLine(staff.CanHire(candidate, days.Balance) ? line : HudStyle.Colored(line, _waitingColor));
            }
            plan.AppendLine();
            plan.AppendLine(Tr.Get(_manageText));
            return plan.ToString().TrimEnd();
        }

        // A button as an Xbox controller shows it: players are only ever shown those.
        private static string Button(PromptControl control) =>
            $"[{HudStyle.Glyph(control, true)}]";

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

        // The button prompt, plus who's ready when there's more than one player. (With one player,
        // being ready moves the day on at once, so there's nothing to show.)
        // While someone holds Menu, a bar fills in their colour beside the prompt (decision 2026-11-10).
        private string WithReady(string text)
        {
            var holder = HoldBar.Holder();
            var prompt = holder != null
                ? $"{Tr.Get(_readyText)}  {HoldBar.Text(holder, _settings.GetPlayerColor(holder.playerIndex), _waitingColor)}"
                : Tr.Get(_readyText);
            return PlayerInput.all.Count <= 1 ? $"{text}\n{prompt}" : $"{text}\n{prompt}\n{ReadyLine()}";
        }

        private string PausedText()
        {
            var waiting = _dayLoop.ControllerPause.WaitingFor;
            var names = string.Join(", ", waiting.Select(index => HudStyle.Colored($"P{index + 1}", _settings.GetPlayerColor(index))));
            var text = $"<b>{Tr.Format(_pausedText, ("players", names))}</b>";
            // Only someone still playing can carry on, so with nobody left the second line would lie.
            return waiting.Count < PlayerInput.all.Count ? $"{text}\n{Tr.Get(_carryOnText)}" : text;
        }

        private string ReadyLine()
        {
            _text.Clear();
            foreach (var player in PlayerInput.all.OrderBy(p => p.playerIndex))
            {
                var name = $"P{player.playerIndex + 1}";
                if (_dayLoop.ControllerPause.IsLost(player.playerIndex))
                {
                    // Not waited for: the day moves on without them.
                    _text.Append(HudStyle.Colored($"{name} {Tr.Get(_playerLostText)}", _badColor) + "   ");
                    continue;
                }
                _text.Append(_dayLoop.Ready.IsReady(player.playerIndex)
                    ? $"<b>{HudStyle.Colored($"{name} {Tr.Get(_playerReadyText)}", _settings.GetPlayerColor(player.playerIndex))}</b>   "
                    : HudStyle.Colored($"{name} {Tr.Get(_playerWaitingText)}", _waitingColor) + "   ");
            }
            return _text.ToString().TrimEnd();
        }

        // In the player's language and currency (Locale).
        private static string FormatMoney(float money) => Game.Data.Locale.Money(money, Game.Data.MoneyStyle.Whole);

        private string ClockText(ServiceSimulation service)
        {
            var minutes = _root.Services.ServiceConfig.OpensAt * 60f + service.Elapsed / 60f;
            var clock = $"{(int)(minutes / 60f) % 24:00}:{(int)minutes % 60:00}";
            return service.Phase switch
            {
                ServicePhase.LastOrders => $"{clock}  {HudStyle.Colored(Tr.Get(_lastOrdersText), _noticeColor)}",
                ServicePhase.Closed => $"{clock}  {HudStyle.Colored(Tr.Get(_closedText), _noticeColor)}",
                _ => clock,
            };
        }
    }
}
