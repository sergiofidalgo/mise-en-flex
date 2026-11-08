using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Simulation.Days;
using Game.Simulation.Saving;
using Game.Simulation.Service;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The Review dashboard (decisions 2026-11-06 and 2026-11-08, after the user's "Plates" mockup): a
    /// review-listing app over the kitchen while Review lasts. It's read-only, except for replying to
    /// reviews. LB and RB switch tabs, the stick browses reviews, Y replies, and Menu readies up for
    /// the next day as usual. It's built in code, like the rest of the HUD.
    /// </summary>
    public sealed class ReviewDashboard : MonoBehaviour
    {
        private enum Tab
        {
            Overview,
            Reviews,
            Menu,
            Books,
        }

        private static readonly string[] Weekdays = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private DayLoop _dayLoop;
        [SerializeField] private CoopSettings _settings;

        [Header("Look")]
        [SerializeField] private Color _dim = new Color(0.07f, 0.05f, 0.04f, 0.82f);
        [SerializeField] private Color _paper = new Color(0.95f, 0.91f, 0.84f);
        [SerializeField] private Color _footer = new Color(0.91f, 0.86f, 0.77f);
        [SerializeField] private Color _ink = new Color(0.16f, 0.12f, 0.09f);
        [SerializeField] private Color _muted = new Color(0.47f, 0.40f, 0.33f);
        [SerializeField] private Color _card = new Color(0.92f, 0.86f, 0.75f);
        [SerializeField] private Color _cardLight = new Color(0.99f, 0.97f, 0.93f);
        [SerializeField] private Color _bar = new Color(0.17f, 0.12f, 0.09f);
        [SerializeField] private Color _accent = new Color(0.85f, 0.39f, 0.17f);
        [SerializeField] private Color _gold = new Color(0.89f, 0.69f, 0.24f);
        [SerializeField] private Color _good = new Color(0.24f, 0.60f, 0.47f);
        [SerializeField] private Color _alarm = new Color(0.96f, 0.82f, 0.76f);
        [SerializeField] private Color _alarmText = new Color(0.75f, 0.30f, 0.12f);
        [SerializeField] private Vector2Int _photoSize = new Vector2Int(1280, 680);

        [Header("Feel")]
        [Tooltip("How far the stick goes before it moves to the next review.")]
        [SerializeField, Range(0.1f, 1f)] private float _stickThreshold = 0.5f;
        [Tooltip("Real seconds between steps while the stick is held.")]
        [SerializeField, Min(0.05f)] private float _stickRepeat = 0.22f;
        [Tooltip("Review cards shown at once.")]
        [SerializeField, Min(1)] private int _visibleCards = 3;

        [Header("Text")]
        [SerializeField] private string _appName = "Plates";
        [SerializeField] private string _apologise = "We're sorry. We'll do better next time.";
        [SerializeField] private string _thank = "Thank you for coming! See you soon.";
        [SerializeField] private string _standGround = "We stand by our kitchen.";

        private const float PanelWidth = 1780f;
        private const float PanelHeight = 1000f;

        private GameObject _canvasObject;
        private RectTransform _panel;
        private RectTransform _content;
        private RawImage _photo;
        private RenderTexture _photoTexture;
        private TextMeshProUGUI _dayPill;
        private TextMeshProUGUI _newPill;
        private TextMeshProUGUI _search;
        private TextMeshProUGUI _name;
        private TextMeshProUGUI _rating;
        private RectTransform _ratingStars;
        private TextMeshProUGUI _info;
        private RectTransform _chart;
        private readonly List<TextMeshProUGUI> _tabLabels = new List<TextMeshProUGUI>();
        private RectTransform _tabUnderline;
        private TextMeshProUGUI _footerText;

        private Tab _tab;
        private int _selected;
        private bool _choosingReply;
        private float _nextStickStep;
        private bool _dirty = true;
        private int _shownForDay = -1;

        private KitchenServices Services => _root.Services;

        private void Start()
        {
            Build();
            _canvasObject.SetActive(false);
            Services.Days.PhaseChanged += HandlePhaseChanged;
            if (Services.Days.Phase == DayPhase.Review)
                HandlePhaseChanged(DayPhase.Review);
        }

        private void OnDestroy()
        {
            if (_root != null && _root.Services != null)
                Services.Days.PhaseChanged -= HandlePhaseChanged;
            if (_photoTexture != null)
                _photoTexture.Release();
        }

        private void HandlePhaseChanged(DayPhase phase)
        {
            var show = phase == DayPhase.Review;
            _canvasObject.SetActive(show);
            if (!show)
                return;

            // A new day's Review starts on its overview, with the photo taken as the doors close.
            if (_shownForDay != Services.Days.Day)
            {
                _shownForDay = Services.Days.Day;
                _tab = Tab.Overview;
                _selected = 0;
                _choosingReply = false;
                TakePhoto();
            }
            _dirty = true;
        }

        private void Update()
        {
            if (!_canvasObject.activeSelf)
                return;
            ReadInput();
            if (_dirty)
            {
                _dirty = false;
                Refresh();
            }
            _footerText.text = FooterText();
        }

        // ---- Input ----

        private void ReadInput()
        {
            foreach (var player in PlayerInput.all)
            {
                if (!_dayLoop.WasHereLastFrame(player))
                    continue;
                var actions = player.actions;

                if (_choosingReply)
                {
                    if (Pressed(actions, "Interact"))
                        Reply(ReviewReply.Apologise);
                    else if (Pressed(actions, "Use"))
                        Reply(ReviewReply.Thank);
                    else if (Pressed(actions, "ButtonY"))
                        Reply(ReviewReply.StandYourGround);
                    else if (Pressed(actions, "ButtonB"))
                        SetChoosing(false);
                    continue;
                }

                if (Pressed(actions, "RotateLeft"))
                    SwitchTab(-1);
                else if (Pressed(actions, "RotateRight"))
                    SwitchTab(1);
                else if (Pressed(actions, "ButtonY") && SelectedReview() is ReviewRecord review && review.Reply < 0)
                    SetChoosing(true);

                var y = actions["Move"].ReadValue<Vector2>().y;
                if (Mathf.Abs(y) < _stickThreshold)
                {
                    _nextStickStep = 0f;
                }
                else if (Time.unscaledTime >= _nextStickStep)
                {
                    // A fresh push moves at once; holding repeats.
                    _nextStickStep = Time.unscaledTime + _stickRepeat;
                    Browse(y > 0f ? -1 : 1);
                }
            }
        }

        private static bool Pressed(InputActionAsset actions, string name)
        {
            var action = actions.FindAction(name);
            return action != null && action.WasPressedThisFrame();
        }

        private void SwitchTab(int step)
        {
            var count = System.Enum.GetValues(typeof(Tab)).Length;
            _tab = (Tab)(((int)_tab + step + count) % count);
            _selected = 0;
            _dirty = true;
        }

        private void Browse(int step)
        {
            var count = ListedReviews().Count;
            if (count == 0)
                return;
            _selected = Mathf.Clamp(_selected + step, 0, count - 1);
            _dirty = true;
        }

        private void SetChoosing(bool choosing)
        {
            _choosingReply = choosing;
            _dirty = true;
        }

        private void Reply(ReviewReply reply)
        {
            var review = SelectedReview();
            if (review != null)
                _root.ReplyTo(review, reply);
            SetChoosing(false);
        }

        // ---- Data ----

        private List<ReviewRecord> TodaysReviews() =>
            Services.ReviewHistory.Where(r => r.Day == Services.Days.Day).OrderBy(r => r.ArrivedAt).ToList();

        private List<ReviewRecord> ListedReviews() => _tab switch
        {
            Tab.Overview => TodaysReviews(),
            Tab.Reviews => Services.ReviewHistory.AsEnumerable().Reverse().ToList(),
            _ => new List<ReviewRecord>(),
        };

        private ReviewRecord SelectedReview()
        {
            var list = ListedReviews();
            return _selected >= 0 && _selected < list.Count ? list[_selected] : null;
        }

        private string Money(float amount) =>
            (amount < 0f ? "−" : string.Empty) + Services.ServiceConfig.Currency + Mathf.Abs(amount).ToString(Mathf.Abs(amount) < 100f ? "0.##" : "0");

        private string ClockAt(float serviceSeconds)
        {
            var minutes = Services.ServiceConfig.OpensAt * 60f + serviceSeconds / 60f;
            return $"{(int)(minutes / 60f) % 24:00}:{(int)minutes % 60:00}";
        }

        private static string ReasonFor(ReviewTopic topic) => topic switch
        {
            ReviewTopic.Praise => "Loved it",
            ReviewTopic.Decent => "A decent meal",
            ReviewTopic.Undercooked => "Undercooked",
            ReviewTopic.Overcooked => "Overcooked",
            ReviewTopic.Burnt => "Burnt",
            ReviewTopic.NoMain => "No main on the plate",
            ReviewTopic.NoSide => "Missing the side",
            ReviewTopic.Cold => "Served cold",
            ReviewTopic.PaleCrust => "Pale crust",
            ReviewTopic.NotRested => "Not rested",
            ReviewTopic.NotFresh => "Not fresh",
            ReviewTopic.BadCut => "Rough cut",
            ReviewTopic.SideOff => "The side was off",
            ReviewTopic.SlowService => "Slow service",
            ReviewTopic.WalkedOut => "Gave up waiting",
            _ => topic.ToString(),
        };

        private string ReplyText(ReviewReply reply) => reply switch
        {
            ReviewReply.Apologise => _apologise,
            ReviewReply.Thank => _thank,
            _ => _standGround,
        };

        // Stars are drawn, not typed: the HUD font has no star glyph.
        private void StarRow(Transform parent, float x, float y, float size, float stars)
        {
            var filled = Mathf.Clamp(Mathf.RoundToInt(stars), 0, 5);
            for (var i = 0; i < 5; i++)
            {
                var star = Rect(parent, "Star", x + i * size * 1.1f, y, size, size).gameObject.AddComponent<Image>();
                star.sprite = StarSprite;
                star.color = i < filled ? _gold : new Color(_muted.r, _muted.g, _muted.b, 0.25f);
                star.raycastTarget = false;
            }
        }

        private static Sprite _starSprite;

        // A five-pointed star, made once, anti-aliased by sampling each pixel several times.
        private static Sprite StarSprite
        {
            get
            {
                if (_starSprite != null)
                    return _starSprite;
                const int size = 64;
                var corners = new Vector2[10];
                for (var i = 0; i < 10; i++)
                {
                    var angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                    var radius = i % 2 == 0 ? 0.5f : 0.2f;
                    corners[i] = new Vector2(0.5f + radius * Mathf.Cos(angle), 0.53f + radius * Mathf.Sin(angle));
                }
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (var py = 0; py < size; py++)
                for (var px = 0; px < size; px++)
                {
                    var inside = 0;
                    for (var s = 0; s < 16; s++)
                    {
                        var point = new Vector2((px + (s % 4 + 0.5f) / 4f) / size, (py + (s / 4 + 0.5f) / 4f) / size);
                        if (Inside(point, corners))
                            inside++;
                    }
                    texture.SetPixel(px, py, new Color(1f, 1f, 1f, inside / 16f));
                }
                texture.Apply();
                _starSprite = Sprite.Create(texture, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                return _starSprite;
            }
        }

        private static bool Inside(Vector2 point, Vector2[] polygon)
        {
            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if ((polygon[i].y > point.y) != (polygon[j].y > point.y) &&
                    point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                    inside = !inside;
            }
            return inside;
        }

        // ---- Refresh ----

        private void Refresh()
        {
            var days = Services.Days;
            var config = Services.ServiceConfig;
            var all = Services.ReviewHistory;
            var today = TodaysReviews();

            _search.text = $"Restaurants near {config.Area}";
            _dayPill.text = $"Day {days.Day} · {Weekdays[(days.Day - 1) % 7]}";
            _newPill.text = today.Count == 1 ? "1 new review" : $"{today.Count} new reviews";
            _newPill.transform.parent.gameObject.SetActive(today.Count > 0);

            _name.text = config.RestaurantName;
            var rating = ReviewReplies.Rating(all);
            Clear(_ratingStars);
            _rating.text = rating.HasValue ? $"<b>{rating.Value:0.0}</b>" : HudStyle.Colored("No reviews yet", _muted);
            if (rating.HasValue)
            {
                var after = _rating.preferredWidth + 18f;
                StarRow(_ratingStars, after, 6f, 40f, rating.Value);
                Text(_ratingStars, "Count", $"({all.Count})", 30f, _muted, FontStyles.Normal, after + 5f * 44f + 16f, 6f, 120f, 44f);
            }
            var hours = Services.Service.Settings.DurationSeconds;
            var band = config.MainPrice < 15f ? 1 : config.MainPrice < 30f ? 2 : 3;
            _info.text = $"{new string(config.Currency[0], band)} · {config.StyleName} · Open {ClockAt(0f)}–{ClockAt(hours)}";

            // Tabs sit an even gap apart, whatever their labels say.
            var tabX = 840f;
            for (var i = 0; i < _tabLabels.Count; i++)
            {
                var tab = (Tab)i;
                var label = _tabLabels[i];
                label.text = tab == Tab.Reviews ? $"Reviews {all.Count}" : tab.ToString();
                label.color = tab == _tab ? _ink : _muted;
                label.fontStyle = tab == _tab ? FontStyles.Bold : FontStyles.Normal;
                label.rectTransform.anchoredPosition = new Vector2(tabX, label.rectTransform.anchoredPosition.y);
                tabX += label.preferredWidth + 56f;
            }
            var selectedLabel = _tabLabels[(int)_tab].rectTransform;
            _tabUnderline.anchoredPosition = new Vector2(selectedLabel.anchoredPosition.x, _tabUnderline.anchoredPosition.y);
            _tabUnderline.sizeDelta = new Vector2(_tabLabels[(int)_tab].preferredWidth, _tabUnderline.sizeDelta.y);

            DrawChart(today);

            Clear(_content);
            switch (_tab)
            {
                case Tab.Overview:
                    DrawOverview(today);
                    break;
                case Tab.Reviews:
                    DrawReviewList(ListedReviews(), 0f);
                    break;
                case Tab.Menu:
                    DrawMenu(today, all);
                    break;
                case Tab.Books:
                    DrawBooks();
                    break;
            }
        }

        private string FooterText()
        {
            var hints = $"{Badge("LB")} {Badge("RB")} Tabs     {Badge("LS")} Browse     ";
            if (_choosingReply)
                return $"{Badge("A")} Apologise     {Badge("X")} Thank     {Badge("Y")} Stand your ground     {Badge("B")} Back";
            if (SelectedReview() is ReviewRecord review && review.Reply < 0)
                hints += $"{Badge("Y")} Reply     ";
            hints += $"{Badge("≡")} Start day {Services.Days.Day + 1}";
            if (PlayerInput.all.Count > 1)
                hints += "   " + string.Join("  ", PlayerInput.all.OrderBy(p => p.playerIndex).Select(p =>
                    _dayLoop.Ready.IsReady(p.playerIndex)
                        ? HudStyle.Colored($"P{p.playerIndex + 1} ready", _settings.GetPlayerColor(p.playerIndex))
                        : HudStyle.Colored($"P{p.playerIndex + 1}", _muted)));
            return hints;
        }

        private string Badge(string button) => $"<b>{HudStyle.Colored($"[{button}]", _ink)}</b>";

        private void DrawOverview(List<ReviewRecord> today)
        {
            var summary = Services.Days.LastService;
            var y = 0f;
            const float cardWidth = 235f;
            const float gap = 20f;
            StatCard(0f * (cardWidth + gap), y, cardWidth, $"{summary?.Served ?? 0}", "Served", false);
            StatCard(1f * (cardWidth + gap), y, cardWidth, $"{summary?.WalkOuts ?? 0}", "Walked out", (summary?.WalkOuts ?? 0) > 0);
            StatCard(2f * (cardWidth + gap), y, cardWidth, $"{summary?.TurnedAway ?? 0}", "Turned away", (summary?.TurnedAway ?? 0) > 0);
            var balance = Box(_content, "Balance", _bar, 3f * (cardWidth + gap), y, cardWidth, 150f);
            Text(balance, "Amount", Money(Services.Days.Balance), 46f, _paper, FontStyles.Bold, 22f, 14f, cardWidth - 40f, 60f);
            Text(balance, "Today", $"Balance · {Money(Services.Days.Today.Net)} today", 20f, _card, FontStyles.Normal, 22f, 80f, cardWidth - 40f, 56f);

            y += 175f;
            Text(_content, "Mentions", "GUESTS MENTION", 20f, _muted, FontStyles.Bold, 0f, y + 10f, 220f, 32f).characterSpacing = 6f;
            var x = 230f;
            foreach (var topic in today.GroupBy(r => (ReviewTopic)r.Topic).OrderByDescending(g => g.Count()).Take(3))
            {
                var complaint = ReviewReplies.IsComplaint(topic.Key);
                var label = $"{ReasonFor(topic.Key)} · {topic.Count()}";
                var chip = Box(_content, "Chip", complaint ? _alarm : _card, x, y, 0f, 50f);
                var text = Text(chip, "Label", label, 22f, complaint ? _alarmText : _ink, FontStyles.Bold, 18f, 0f, 400f, 50f);
                text.alignment = TextAlignmentOptions.MidlineLeft;
                chip.sizeDelta = new Vector2(text.preferredWidth + 36f, 50f);
                x += chip.sizeDelta.x + 14f;
            }

            y += 75f;
            if (today.Count == 0)
                Text(_content, "None", "No reviews today.", 24f, _muted, FontStyles.Italic, 0f, y + 20f, 600f, 40f);
            DrawReviewList(today, y);
        }

        private void StatCard(float x, float y, float width, string number, string label, bool alarm)
        {
            var card = Box(_content, label, alarm ? _alarm : _card, x, y, width, 150f);
            Text(card, "Number", number, 54f, alarm ? _alarmText : _ink, FontStyles.Bold, 22f, 14f, width - 40f, 64f);
            Text(card, "Label", label, 24f, alarm ? _alarmText : _muted, FontStyles.Normal, 22f, 86f, width - 40f, 40f);
        }

        // A window of review cards around the selected one.
        private void DrawReviewList(List<ReviewRecord> reviews, float top)
        {
            const float height = 150f;
            const float gap = 14f;
            var first = Mathf.Clamp(_selected - _visibleCards + 2, 0, Mathf.Max(0, reviews.Count - _visibleCards));
            for (var i = first; i < Mathf.Min(reviews.Count, first + _visibleCards); i++)
                ReviewCard(reviews[i], top + (i - first) * (height + gap), height, i == _selected);
        }

        private void ReviewCard(ReviewRecord review, float y, float height, bool selected)
        {
            const float width = 1000f;
            if (selected)
                Box(_content, "Selected", _gold, -4f, y - 4f, width + 8f, height + 8f);
            var card = Box(_content, "Review", _cardLight, 0f, y, width, height);

            var avatar = Box(card, "Avatar", Color.HSVToRGB(Mathf.Abs(review.Guest.GetHashCode() % 360) / 360f, 0.3f, 0.75f), 24f, 22f, 58f, 64f, circle: true);
            avatar.GetComponent<Image>().type = Image.Type.Simple;

            var heading = $"<b>{review.Guest}</b>{HudStyle.Colored((string.IsNullOrEmpty(review.Order) ? "" : $" · {review.Order}") + $" · {ClockAt(review.ArrivedAt)}", _muted)}";
            if (review.Day != Services.Days.Day)
                heading += HudStyle.Colored($" · day {review.Day}", _muted);
            Text(card, "Heading", heading, 22f, _ink, FontStyles.Normal, 104f, 14f, 640f, 32f);
            StarRow(card, width - 24f - 5f * 26f * 1.1f, 16f, 26f, review.RatedStars);
            Text(card, "Quote", $"“{review.Line}”", 30f, _ink, FontStyles.Bold, 104f, 48f, width - 130f, 42f);

            var chipText = review.WalkedOut
                ? $"Walked out · {Money(-review.Penalty)}"
                : $"{review.Stars:0.#} stars · {Money(review.Paid)} + {Money(review.Tip)} tip";
            var chip = Box(card, "Chip", review.WalkedOut ? _alarm : _card, 104f, 98f, 0f, 36f);
            var chipLabel = Text(chip, "Label", chipText, 19f, review.WalkedOut ? _alarmText : _ink, FontStyles.Bold, 14f, 0f, 400f, 36f);
            chipLabel.alignment = TextAlignmentOptions.MidlineLeft;
            chip.sizeDelta = new Vector2(chipLabel.preferredWidth + 28f, 36f);
            var under = review.Reply >= 0
                ? HudStyle.Colored($"You replied: “{ReplyText((ReviewReply)review.Reply)}”", _good)
                : ReasonFor((ReviewTopic)review.Topic);
            Text(card, "Reason", under, 20f, _muted, FontStyles.Normal, 104f + chip.sizeDelta.x + 16f, 98f, width - 260f - chip.sizeDelta.x, 36f).alignment = TextAlignmentOptions.MidlineLeft;

            if (selected && review.Reply < 0)
                Text(card, "Reply", _choosingReply ? HudStyle.Colored("Choose a reply", _accent) : $"{Badge("Y")} Reply", 22f, _ink, FontStyles.Normal, width - 200f, 98f, 176f, 36f).alignment = TextAlignmentOptions.Right;
        }

        private void DrawMenu(List<ReviewRecord> today, List<ReviewRecord> all)
        {
            Text(_content, "Heading", "What guests ordered", 30f, _ink, FontStyles.Bold, 0f, 0f, 900f, 44f);
            Row(60f, "Dish", "Today", "Stars today", "Taken today", "All time", true);
            var y = 110f;
            foreach (var dish in all.Where(r => !string.IsNullOrEmpty(r.Order)).GroupBy(r => r.Order).OrderBy(g => g.Key))
            {
                var todays = dish.Where(r => today.Contains(r)).ToList();
                var stars = todays.Count > 0 ? todays.Average(r => r.Stars).ToString("0.0") : "–";
                Row(y, dish.Key, todays.Count.ToString(), stars, Money(todays.Sum(r => r.Paid + r.Tip)), dish.Count().ToString(), false);
                y += 52f;
            }
            Text(_content, "Note", HudStyle.Colored("Prices and the menu are set in the cookbook (coming soon).", _muted), 20f, _ink, FontStyles.Italic, 0f, y + 20f, 900f, 32f);
        }

        private void Row(float y, string a, string b, string c, string d, string e, bool header)
        {
            var colour = header ? _muted : _ink;
            var style = header ? FontStyles.Bold : FontStyles.Normal;
            Text(_content, "A", a, 24f, colour, style, 0f, y, 360f, 40f);
            Text(_content, "B", b, 24f, colour, style, 380f, y, 120f, 40f);
            Text(_content, "C", c, 24f, colour, style, 520f, y, 160f, 40f);
            Text(_content, "D", d, 24f, colour, style, 700f, y, 160f, 40f);
            Text(_content, "E", e, 24f, colour, style, 880f, y, 120f, 40f);
        }

        private void DrawBooks()
        {
            var days = Services.Days;
            LedgerColumn("Today", days.Today, 0f);
            LedgerColumn(days.FeeCollectedToday ? $"The last {days.Fee.EveryDays} days" : "Since the last fee", days.Period, 340f);

            // The boss: what's owed and when he collects (decision 2026-11-08).
            var boss = Box(_content, "Boss", _bar, 680f, 0f, 320f, 330f);
            Text(boss, "Title", "THE BOSS", 20f, _card, FontStyles.Bold, 24f, 20f, 280f, 30f).characterSpacing = 6f;
            var due = days.FeeCollectedToday
                ? $"Fee of {Money(days.Fee.Amount)} collected today"
                : $"Fee {Money(days.Fee.Amount)} due in {days.DaysUntilFee} day{(days.DaysUntilFee == 1 ? "" : "s")}";
            Text(boss, "Fee", due, 26f, _paper, FontStyles.Bold, 24f, 64f, 280f, 70f);
            Text(boss, "Owed", days.OwedToBoss > 0f ? $"You owe him {Money(days.OwedToBoss)}" : "You owe him nothing", 26f, days.OwedToBoss > 0f ? _alarm : _paper, FontStyles.Normal, 24f, 150f, 280f, 70f);
            Text(boss, "Balance", $"Balance {Money(days.Balance)}", 22f, _card, FontStyles.Normal, 24f, 240f, 280f, 60f);

            DrawProgress(370f);
        }

        private void LedgerColumn(string title, Ledger ledger, float x)
        {
            var box = Box(_content, title, _card, x, 0f, 320f, 330f);
            Text(box, "Title", title.ToUpperInvariant(), 18f, _muted, FontStyles.Bold, 20f, 18f, 280f, 28f).characterSpacing = 4f;
            var y = 56f;
            foreach (LedgerLine line in System.Enum.GetValues(typeof(LedgerLine)))
            {
                var amount = ledger.Of(line);
                if (amount == 0f)
                    continue;
                var income = Ledger.IsIncome(line);
                Text(box, line.ToString(), LineName(line), 21f, _ink, FontStyles.Normal, 20f, y, 170f, 30f);
                Text(box, line + "Amount", Money(income ? amount : -amount), 21f, income ? _good : _alarmText, FontStyles.Bold, 180f, y, 120f, 30f).alignment = TextAlignmentOptions.TopRight;
                y += 30f;
            }
            Text(box, "Net", $"<b>Net {Money(ledger.Net)}</b>", 24f, ledger.Net < 0f ? _alarmText : _good, FontStyles.Normal, 20f, 284f, 280f, 34f);
        }

        private static string LineName(LedgerLine line) => line switch
        {
            LedgerLine.Payments => "Meals",
            LedgerLine.Tips => "Tips",
            LedgerLine.Sales => "Sold",
            LedgerLine.Ingredients => "Ingredients",
            LedgerLine.WalkOuts => "Walk-outs",
            LedgerLine.Wages => "Wages",
            LedgerLine.Energy => "Energy",
            LedgerLine.Purchases => "Bought",
            LedgerLine.Fee => "Fee to the boss",
            _ => line.ToString(),
        };

        // The restaurant's progress: each finished day's money and rating, and the team.
        private void DrawProgress(float top)
        {
            var box = Box(_content, "Progress", _card, 0f, top, 1000f, 345f);
            Text(box, "Title", "HOW IT'S GOING", 18f, _muted, FontStyles.Bold, 20f, 16f, 400f, 28f).characterSpacing = 4f;
            var history = Services.Days.History.Skip(Mathf.Max(0, Services.Days.History.Count - 10)).ToList();
            var biggest = Mathf.Max(1f, history.Count == 0 ? 1f : history.Max(d => Mathf.Abs(d.In - d.Out)));
            const float barArea = 110f;
            const float baseline = 170f;
            for (var i = 0; i < history.Count; i++)
            {
                var day = history[i];
                var net = day.In - day.Out;
                var height = Mathf.Max(4f, barArea * Mathf.Abs(net) / biggest);
                var x = 24f + i * 62f;
                Box(box, "Bar", net >= 0f ? _good : _accent, x, net >= 0f ? baseline - height : baseline, 44f, height);
                Text(box, "Day", $"{day.Day}", 16f, _muted, FontStyles.Normal, x, baseline + barArea + 8f, 44f, 22f).alignment = TextAlignmentOptions.Top;
                if (day.Rating >= 0f)
                    Text(box, "Rating", $"{day.Rating:0.0} stars", 15f, _ink, FontStyles.Normal, x - 12f, 46f, 68f, 22f).alignment = TextAlignmentOptions.Top;
            }

            var team = string.Join("\n", Services.Staff.Hired.Select(m => $"{ServiceStatusHud.WorkDone(m)}, {ServiceStatusHud.Mood(m)}")
                .Concat(Services.Staff.LastQuit.Select(m => HudStyle.Colored($"{ServiceStatusHud.WorkDone(m)}, and quit", _alarmText))));
            var close = Services.Days.LastClose;
            if (close.Spoiled > 0 || close.LeftOut > 0)
                team += (team.Length > 0 ? "\n" : "") + $"Overnight: {close.Spoiled} spoiled, {close.LeftOut} left out";
            Text(box, "Team", team.Length > 0 ? team : HudStyle.Colored("No staff hired.", _muted), 20f, _ink, FontStyles.Normal, 660f, 50f, 320f, 280f);
        }

        private void DrawChart(List<ReviewRecord> today)
        {
            Clear(_chart);
            var duration = Services.Service.Settings.DurationSeconds;
            const float bucket = 30f * 60f;
            var buckets = Mathf.Max(1, Mathf.CeilToInt(duration / bucket));
            var served = new int[buckets];
            var left = new int[buckets];
            foreach (var review in today)
            {
                var b = Mathf.Clamp((int)(review.ArrivedAt / bucket), 0, buckets - 1);
                if (review.WalkedOut)
                    left[b]++;
                else
                    served[b]++;
            }

            var most = Mathf.Max(1, Enumerable.Range(0, buckets).Max(i => served[i] + left[i]));
            var width = _chart.rect.width > 0f ? _chart.rect.width : 600f;
            var step = width / buckets;
            const float area = 110f;
            const float baseline = 140f;
            var rush = Enumerable.Range(0, buckets).OrderByDescending(i => left[i]).First();
            for (var i = 0; i < buckets; i++)
            {
                var x = i * step + step * 0.15f;
                var w = step * 0.7f;
                var servedHeight = area * served[i] / most;
                var leftHeight = area * left[i] / most;
                Box(_chart, "Track", _card, x, baseline - 3f, w, 3f);
                if (servedHeight > 0f)
                    Box(_chart, "Served", _good, x, baseline - servedHeight, w, servedHeight);
                if (leftHeight > 0f)
                    Box(_chart, "Left", _accent, x, baseline - servedHeight - leftHeight, w, leftHeight);
                if (i % 2 == 0)
                    Text(_chart, "Time", ClockAt(i * bucket), 17f, _muted, FontStyles.Normal, x - 10f, baseline + 8f, w + 30f, 24f);
            }
            if (left[rush] > 0)
            {
                var x = rush * step;
                Text(_chart, "Rush", $"Rush · {left[rush]} left", 18f, _accent, FontStyles.Bold, x - 20f, 0f, step + 120f, 26f);
            }
        }

        // ---- The photo ----

        // The dining room as the doors close, from the game camera, without the HUD.
        private void TakePhoto()
        {
            var camera = Camera.main;
            if (camera == null)
                return;
            if (_photoTexture == null)
                _photoTexture = new RenderTexture(_photoSize.x, _photoSize.y, 24);
            var previous = camera.targetTexture;
            camera.targetTexture = _photoTexture;
            camera.Render();
            camera.targetTexture = previous;
            _photo.texture = _photoTexture;
        }

        // ---- Building ----

        private void Build()
        {
            _canvasObject = new GameObject("ReviewDashboard", typeof(RectTransform));
            _canvasObject.transform.SetParent(transform, false);
            var canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = Box(_canvasObject.transform, "Dim", _dim, 0f, 0f, 0f, 0f, rounded: false);
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.sizeDelta = Vector2.zero;

            // The panel is a fixed size, centred, scaled down to fit inside the screen's margins.
            _panel = Box(_canvasObject.transform, "Panel", _paper, 0f, 0f, PanelWidth, PanelHeight);
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = Vector2.zero;
            _panel.gameObject.AddComponent<RectMask2D>();

            var header = Box(_panel, "Header", _bar, 0f, 0f, PanelWidth, 90f, rounded: false);
            // The app's mark: a gold ring with a dot, drawn (the font has no such glyph).
            Box(header, "Mark", _gold, 40f, 27f, 36f, 36f, circle: true);
            Box(header, "MarkHole", _bar, 45f, 32f, 26f, 26f, circle: true);
            Box(header, "MarkDot", _gold, 51f, 38f, 14f, 14f, circle: true);
            Text(header, "Logo", _appName, 40f, _gold, FontStyles.Bold, 90f, 0f, 180f, 90f).alignment = TextAlignmentOptions.MidlineLeft;
            var search = Box(header, "Search", new Color(1f, 1f, 1f, 0.08f), 260f, 18f, 680f, 54f);
            _search = Text(search, "Text", "", 26f, _card, FontStyles.Normal, 28f, 0f, 640f, 54f);
            _search.alignment = TextAlignmentOptions.MidlineLeft;
            var dayPill = Box(header, "Day", new Color(1f, 1f, 1f, 0.08f), PanelWidth - 470f, 18f, 190f, 54f);
            _dayPill = Text(dayPill, "Text", "", 25f, _paper, FontStyles.Bold, 0f, 0f, 190f, 54f);
            _dayPill.alignment = TextAlignmentOptions.Center;
            var newPill = Box(header, "New", _accent, PanelWidth - 262f, 18f, 222f, 54f);
            _newPill = Text(newPill, "Text", "", 25f, _paper, FontStyles.Bold, 0f, 0f, 222f, 54f);
            _newPill.alignment = TextAlignmentOptions.Center;

            // Left: the photo, the name and rating, and tonight's guests.
            var photoFrame = Box(_panel, "Photo", _card, 40f, 120f, 660f, 350f);
            _photo = new GameObject("Image", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _photo.transform.SetParent(photoFrame, false);
            Fill(_photo.rectTransform);
            var caption = Box(photoFrame, "Caption", new Color(0.1f, 0.07f, 0.05f, 0.75f), 380f, 300f, 264f, 38f);
            Text(caption, "Text", "Your photo · captured at close", 18f, _paper, FontStyles.Normal, 0f, 0f, 264f, 38f).alignment = TextAlignmentOptions.Center;
            _name = Text(_panel, "Name", "", 56f, _ink, FontStyles.Bold, 40f, 490f, 660f, 70f);
            _rating = Text(_panel, "Rating", "", 40f, _ink, FontStyles.Normal, 40f, 565f, 660f, 54f);
            _ratingStars = Rect(_panel, "RatingStars", 40f, 565f, 660f, 54f);
            _info = Text(_panel, "Info", "", 26f, _muted, FontStyles.Normal, 40f, 624f, 660f, 40f);
            var chartBox = Box(_panel, "Guests", _card, 40f, 680f, 660f, 220f);
            Text(chartBox, "Title", "TONIGHT'S GUESTS", 18f, _muted, FontStyles.Bold, 24f, 16f, 300f, 28f).characterSpacing = 4f;
            Text(chartBox, "Legend", HudStyle.Colored("● ", _good) + "Served   " + HudStyle.Colored("● ", _accent) + "Walked out", 18f, _ink, FontStyles.Normal, 360f, 16f, 280f, 28f).alignment = TextAlignmentOptions.TopRight;
            _chart = Rect(chartBox, "Chart", 24f, 44f, 612f, 176f);

            // Right: the tabs and what's under them.
            Badge(_panel, "LB", 740f, 122f);
            foreach (Tab tab in System.Enum.GetValues(typeof(Tab)))
                _tabLabels.Add(Text(_panel, tab.ToString(), tab.ToString(), 34f, _muted, FontStyles.Normal, 0f, 116f, 220f, 50f));
            Badge(_panel, "RB", PanelWidth - 110f, 122f);
            Box(_panel, "Rule", _card, 740f, 172f, 1000f, 3f, rounded: false);
            _tabUnderline = Box(_panel, "Underline", _gold, 830f, 169f, 160f, 6f, rounded: false);
            _content = Rect(_panel, "Content", 740f, 200f, 1000f, 700f);

            var footer = Box(_panel, "Footer", _footer, 0f, PanelHeight - 90f, PanelWidth, 90f, rounded: false);
            _footerText = Text(footer, "Hints", "", 26f, _ink, FontStyles.Normal, 40f, 0f, PanelWidth - 80f, 90f);
            _footerText.alignment = TextAlignmentOptions.MidlineRight;

            FitPanel();
        }

        // The whole panel is scaled to stay inside the screen, whatever its shape (HUD never clipped).
        private void FitPanel()
        {
            var margin = _style.ScreenMargin * 2f;
            var scale = Mathf.Min(1f, (1920f - margin) / PanelWidth, (1080f - margin) / PanelHeight);
            _panel.localScale = new Vector3(scale, scale, 1f);
        }

        private void Badge(RectTransform parent, string label, float x, float y)
        {
            var badge = Box(parent, label, _bar, x, y, 70f, 44f);
            Text(badge, "Text", label, 22f, _paper, FontStyles.Bold, 0f, 0f, 70f, 44f).alignment = TextAlignmentOptions.Center;
        }

        private static void Clear(RectTransform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // Everything is laid out from its parent's top-left corner, in canvas pixels, like the mockup.
        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private RectTransform Box(Transform parent, string name, Color color, float x, float y, float width, float height, bool rounded = true, bool circle = false)
        {
            var rect = Rect(parent, name, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (circle && _style.Circle != null)
            {
                image.sprite = _style.Circle;
            }
            else if (rounded && _style.Rounded != null)
            {
                image.sprite = _style.Rounded;
                image.type = Image.Type.Sliced;
            }
            return rect;
        }

        private TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, FontStyles style, float x, float y, float width, float height)
        {
            var label = Rect(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _style.Font;
            label.fontSize = size;
            label.color = color;
            label.fontStyle = style;
            label.text = text;
            label.richText = true;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.TopLeft;
            return label;
        }
    }
}
