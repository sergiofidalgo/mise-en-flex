using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Days;
using Game.Simulation.Saving;
using Game.Simulation.Service;
using Game.Simulation.Staff;
using Game.Simulation.Campaign;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Data;
using Game.Gameplay;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// Drawing the Review dashboard. Positions, sizes and colours were measured from the Art Director's
    /// dashboard SVGs (1920×1080: overview, reviews, reply wheel, reply thread, menu, books); text sits
    /// on the mockup's baselines and sizes are its points × 4/3, checked against captures.
    /// </summary>
    public sealed partial class ReviewDashboard
    {
        // The restaurant being played (Phase 6, milestone 3): its own name and style, not the config's.
        private string ShownName => string.IsNullOrEmpty(Services.RestaurantName) ? Services.ServiceConfig.RestaurantName : Services.RestaurantName;
        private string ShownStyle => string.IsNullOrEmpty(Services.StyleName) ? Services.ServiceConfig.StyleName : Services.StyleName;

        [Header("Palette (from the mockups)")]
        [SerializeField] private Color _paper = Hex(0xf4e6cc);
        [SerializeField] private Color _ink = Hex(0x2b211b);
        [SerializeField] private Color _well = Hex(0x3e3027);
        [SerializeField] private Color _wellLight = Hex(0x4a3628);
        [Tooltip("Body text on the panel.")]
        [SerializeField] private Color _body = Hex(0x6b5240);
        [Tooltip("Meta text and section labels.")]
        [SerializeField] private Color _muted = Hex(0x7a604b);
        [SerializeField] private Color _quiet = Hex(0x9a8470);
        [SerializeField] private Color _sand = Hex(0xbfa88a);
        [SerializeField] private Color _card = Hex(0xeadbbe);
        [SerializeField] private Color _cardLight = Hex(0xfff8ec);
        [SerializeField] private Color _cardEdge = Hex(0xe6d3b0);
        [SerializeField] private Color _rule = Hex(0xdcc9a6);
        [SerializeField] private Color _gold = Hex(0xe2b04a);
        [SerializeField] private Color _good = Hex(0x3a9a78);
        [SerializeField] private Color _goodText = Hex(0x2e7a5c);
        [SerializeField] private Color _goodLight = Hex(0x7cc2a4);
        [SerializeField] private Color _bad = Hex(0xd55e00);
        [SerializeField] private Color _badText = Hex(0xb34700);
        [SerializeField] private Color _alarm = Hex(0xf6d2bc);
        [SerializeField] private Color _alarmText = Hex(0x8a3a10);
        [SerializeField] private Color _alarmDark = Hex(0x5a2a10);
        [SerializeField] private Color _shortBy = Hex(0xf08a4b);
        [SerializeField] private Color _ingredients = Hex(0x8e7a6a);
        [SerializeField] private Color _buttonA = Hex(0x5da83a);
        [SerializeField] private Color _buttonB = Hex(0xd9493b);
        [SerializeField] private Color _buttonX = Hex(0x3b72d9);
        [SerializeField] private Color _buttonY = Hex(0xe8b52e);
        [SerializeField] private Color _backdropTint = new Color(0.06f, 0.05f, 0.04f, 0.55f);

        // ---- Layout, in the mockups' 1920×1080 frame ----

        private const float PanelX = 80f, PanelY = 56f, PanelWidth = 1760f, PanelHeight = 968f, PanelRadius = 26f;
        private const float HeaderBottom = 140f, FooterTop = 952f;
        private const float RightX = 764f, RightWidth = 1036f, RightEdge = 1800f;
        private const float CardLeft = 765f, CardWidth = 1034f;
        // The Overview's review list, under the boss and the filters.
        private const float ListTop = 500f, ListBottom = 936f;

        private GameObject _canvasObject;
        private RectTransform _layout;
        private RawImage _backdrop;
        private RenderTexture _blurTexture;
        private RawImage _photo;
        private RenderTexture _photoTexture;
        private RectTransform _header;
        private RectTransform _left;
        private RectTransform _tabs;
        private RectTransform _content;
        private RectTransform _list;
        private RectTransform _popover;
        private RectTransform _hints;
        private TextMeshProUGUI _caption;

        private TMP_FontAsset Medium => _style.Medium;
        private TMP_FontAsset SemiBold => _style.SemiBold;
        private TMP_FontAsset Bold => _style.Bold;
        private TMP_FontAsset ExtraBold => _style.ExtraBold;
        private TMP_FontAsset Serif => _style.SerifItalic;
        // What guests write: upright Lora (the user's call, 2026-11-11).
        private TMP_FontAsset Upright => _style.Serif;

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);

        private static Color Alpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        private TextMeshProUGUI Label(TMP_FontAsset font, Transform parent, string text, float size, Color color, float x, float baseline, float width = 600f, HorizontalAlignmentOptions align = HorizontalAlignmentOptions.Left, float tracking = 0f)
        {
            var label = UiKit.TextAt(font, parent, text, text, size, color, x, baseline, width, align);
            if (tracking != 0f)
                label.characterSpacing = tracking / size * 100f;
            return label;
        }

        // Small caps labels such as BALANCE: letters about 1.6 px apart.
        private TextMeshProUGUI Caps(Transform parent, string text, float x, float baseline, Color? color = null) =>
            Label(Bold, parent, text, 16f, color ?? _muted, x, baseline, 500f, tracking: 1.6f);

        // The last finished day: everything forkcast shows is about it (the user's call, 2026-11-11).
        private DaySummary _summary;

        /// <summary>
        /// The day "Start" begins (2026-11-11): the same number the day loop advances to, the day
        /// after the last finished one, so the button and where the hold lands can't disagree.
        /// </summary>
        public int StartsDay => Services.Days.LastFinishedDay + 1;
        private DaySummary Summary => _summary ??= DaySummary.Of(Services.Days);

        private void Refresh()
        {
            _summary = DaySummary.Of(Services.Days);
            DrawHeader();
            DrawLeft();
            DrawTabs();
            UiKit.Clear(_content);
            UiKit.Clear(_list);
            UiKit.Clear(_popover);
            switch (_tab)
            {
                case Tab.Overview:
                    DrawOverview();
                    break;
                case Tab.Restaurant:
                    DrawCookbook();
                    break;
                case Tab.Books:
                    DrawBooks();
                    break;
                case Tab.Staff:
                    DrawStaff();
                    break;
            }
            _hintsShown = null;
            DrawHints();
        }

        // ---- Header: logo, search, day and new reviews ----

        private void DrawHeader()
        {
            UiKit.Clear(_header);
            var days = Services.Days;
            Label(Medium, _header, Tr.Format("{style}s near you", ("style", Tr.Get(ShownStyle))), 21.33f, _sand, 374f, 106f, 500f);

            var shown = Summary.HasDay ? Summary.Day : days.Day;
            var today = Services.ReviewHistory.Count(r => r.Day == shown);
            // The money chip has the very right (the user's call, 2026-11-10); the pills sit to its left.
            var right = 1804f - _moneyWidth - 22f;
            right = HeaderPill(Tr.Format("Day {day} · {weekday}", ("day", shown), ("weekday", Weekday(shown))), _well, right) - 22f;
            if (today > 0)
                HeaderPill(today == 1 ? "1 new review" : $"{today} new reviews", _wellLight, right);
        }

        // A pill whose right edge is at right; returns its left edge.
        private float HeaderPill(string text, Color back, float right)
        {
            var label = Label(SemiBold, _header, text, 18.67f, _paper, 0f, 105f, 300f);
            var width = UiKit.Width(label) + 32f;
            var box = UiKit.RoundBox(_header, text + "Pill", back, right - width, 79f, width, 39f, 19.5f);
            box.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
            label.rectTransform.anchoredPosition = new Vector2(right - width + 16f, label.rectTransform.anchoredPosition.y);
            return right - width;
        }

        // ---- The money chip: the balance, top right, animated as the money card is ----

        private const float MoneyHeight = 46f, MoneyTop = 76f, MoneyCoin = 30f, MoneyPad = 14f, MoneyMinWidth = 150f;
        private RectTransform _money;
        private TextMeshProUGUI _moneyAmount;
        private MoneyTicker _moneyTicker;
        private float _moneyWidth = MoneyMinWidth;
        private bool _moneyKnown;

        // The same rules as the money card (MoneyTicker): money in rolls up in green, money out drops
        // at once with a short red shake. The money card hides behind forkcast, so this stands in for
        // it: a recipe unlocked or a candidate hired shows here as it would anywhere else.
        private void UpdateMoney(float dt)
        {
            _moneyTicker ??= new MoneyTicker(_style.MoneyTicker);
            var balance = Services.Days.Balance;
            if (!_moneyKnown)
            {
                _moneyTicker.Snap(balance);
                _moneyKnown = true;
            }
            else
            {
                _moneyTicker.Set(balance);
            }
            _moneyTicker.Tick(dt);

            // Sized for where a roll ends, so it never twitches mid-roll; the pills follow it.
            var label = _moneyAmount;
            var needed = Mathf.Max(MoneyMinWidth, MoneyPad + MoneyCoin + 10f + label.GetPreferredValues(MoneyTicker.Format(_moneyTicker.Target), 10000f, 10000f).x + MoneyPad + 4f);
            if (_money.childCount == 0 || Mathf.Abs(needed - _moneyWidth) > 0.5f)
            {
                _moneyWidth = needed;
                DrawMoneyChip();
                DrawHeader();
            }

            var resting = _moneyTicker.Target < 0f ? _style.MoneyLoss : _paper;
            var change = _moneyTicker.Direction == MoneyChange.Up ? _style.MoneyGain : _style.MoneyLoss;
            _moneyAmount.color = Color.Lerp(resting, change, _moneyTicker.Tint);
            _moneyAmount.text = MoneyTicker.Format(_moneyTicker.Shown);
            _money.anchoredPosition = new Vector2(1804f - _moneyWidth + _moneyTicker.Offset, -MoneyTop);
        }

        private void DrawMoneyChip()
        {
            var text = _moneyAmount != null ? _moneyAmount.text : "";
            UiKit.Clear(_money);
            _money.sizeDelta = new Vector2(_moneyWidth, MoneyHeight);
            UiKit.StrokedBox(_money, "Chip", _well, _gold, 2.67f, 0f, 0f, _moneyWidth, MoneyHeight, MoneyHeight / 2f);
            // The money card's coin, smaller.
            var coinY = (MoneyHeight - MoneyCoin) / 2f;
            UiKit.StrokedCircle(_money, "Coin", Color.Lerp(_style.MoneyEdge, Color.white, 0.25f), Color.Lerp(_style.MoneyEdge, Color.black, 0.3f), 3f, MoneyPad, coinY, MoneyCoin);
            UiKit.Ellipse(_money, "CoinFace", Color.Lerp(_style.MoneyEdge, Color.black, 0.15f), MoneyPad + MoneyCoin * 0.25f, coinY + MoneyCoin * 0.25f, MoneyCoin * 0.5f, MoneyCoin * 0.5f);
            _moneyAmount = UiKit.TextAt(ExtraBold, _money, "Amount", text, 25f, _paper, _moneyWidth - MoneyPad - 2f, MoneyHeight / 2f + 9f, _moneyWidth - MoneyPad * 2f - MoneyCoin - 10f, HorizontalAlignmentOptions.Right);
        }

        // ---- Left: name, rating, and tonight's guests ----

        private void DrawLeft()
        {
            UiKit.Clear(_left);
            var all = Services.ReviewHistory;
            Label(ExtraBold, _left, ShownName, 46f, _ink, 120f, 566f, 600f, tracking: -1.4f);

            // Stars and count, then price band and category, on one line as a listing has them.
            var x = 120f;
            var rating = ReviewReplies.Rating(all);
            if (rating.HasValue)
            {
                var score = Label(Bold, _left, $"{rating.Value:0.0}", 24f, _ink, x, 604f, 80f);
                x = Stars(_left, x + UiKit.Width(score) + 10f, 586f, 20f, 26f, rating.Value) + 8f;
                x += UiKit.Width(Label(Medium, _left, $"({all.Count})", 20f, _body, x, 603f, 80f)) + 10f;
            }
            else
            {
                x += UiKit.Width(Label(Medium, _left, Tr.Get("No reviews yet"), 20f, _muted, x, 603f, 300f)) + 10f;
            }
            Label(Medium, _left, $"· {Locale.PriceBand(PriceBand())} · {Tr.Get(ShownStyle)}", 20f, _body, x, 603f, 720f - x);

            DrawFacts();
        }

        // The price band follows the menu's dearest main dish, as listings do.
        private int PriceBand()
        {
            var mainPrice = MenuPrices().DefaultIfEmpty(Services.ServiceConfig.MainPrice).Max();
            return mainPrice < 15f ? 1 : mainPrice < 30f ? 2 : 3;
        }

        private IEnumerable<float> MenuPrices() =>
            Services.Cookbook != null ? Services.Cookbook.Menu.Select(r => (float)Services.Cookbook.PriceOf(r.Id)) : new[] { Services.ServiceConfig.MainPrice };

        // ---- The listing card (the user's calls, 2026-11-10): after a Google Places card, kept calm
        // and readable. Four tiles of real facts, a big value over a small label each; the start bar sits under
        // them (2026-11-11). ----

        // Six tiles, three rows of two, from the name and rating down to the panel's foot (the user's
        // pick, 2026-11-11): known for, per person, guests fed, next opening, tonight's best plate, tips.
        private const float TileTop = 628f, TileWidth = 294f, TileHeight = 84f, TileGap = 12f;

        private void DrawFacts()
        {
            // The user's four (2026-11-11): what came in, who was fed, the tips, when it opens next.
            // Revised by the user (2026-11-11): earnings and tips, then the meal they ordered most and the next opening.
            var tiles = new[] { Earnings(), Tips(), MostOrdered(), NextOpening() };
            for (var i = 0; i < tiles.Length; i++)
            {
                var x = 120f + (i % 2) * (TileWidth + TileGap);
                var y = TileTop + (i / 2) * (TileHeight + TileGap);
                UiKit.RoundBox(_left, "Fact", _card, x, y, TileWidth, TileHeight, 16f);
                var value = Label(ExtraBold, _left, tiles[i].Value, 28f, tiles[i].Alarm ? _badText : _ink, x + 20f, y + 40f, TileWidth - 40f, tracking: -0.5f);
                value.enableAutoSizing = true;
                value.fontSizeMin = 18f;
                value.fontSizeMax = 28f;
                Label(Medium, _left, tiles[i].Label, 17.33f, _muted, x + 20f, y + 67f, TileWidth - 40f);
            }

            // The foot of the column is the start bar's (BuildStartButton).
        }

        // The finished night's most ordered meal, by dish (all its donenesses together); for a day
        // whose dishes weren't kept, the all-time favourite.
        private (string Value, string Label, bool Alarm) MostOrdered()
        {
            var key = DaySummary.MostOrdered(Summary.Dishes);
            if (key == null && Services.DishTally.Count > 0)
                key = DaySummary.MostOrdered(Services.DishTally.Select(d => new DishNight { Dish = d.Key, Ordered = d.Value }));
            if (key == null)
                return ("–", Tr.Get("Most ordered meal"), false);
            var recipe = Services.Cookbook?.Recipes.FirstOrDefault(r => r.Id == key);
            return (recipe != null ? Tr.Get(recipe.Name) : Capitalised(key), Tr.Get("Most ordered meal"), false);
        }

        private (string Value, string Label, bool Alarm) GuestsFed()
        {
            var fed = Services.Days.History.Sum(d => d.Served);
            // The finished night's walk-outs beside the running total: the comic, honest bit.
            var walked = Summary.WalkOuts;
            var label = fed == 0 ? Tr.Get("guests fed so far")
                : walked == 0 ? Tr.Get("guests fed")
                : Tr.Plural(walked, "guests fed · 1 fled", "guests fed · {n} fled");
            return ($"{fed}", label, walked > 0);
        }

        // Never open while forkcast shows: next is tonight in a morning, tomorrow after a service.
        private (string Value, string Label, bool Alarm) NextOpening()
        {
            return ($"{NextOpensAt()} {Weekday(StartsDay)}", Tr.Get("Closed · Opens"), false);
        }

        // Everything that came in that night: meals and tips (the user's call: money in, not profit).
        private (string Value, string Label, bool Alarm) Earnings()
        {
            var earned = Summary.HasLines ? Summary.Ledger.In : Summary.Record?.In ?? 0f;
            return (Money(earned), Tr.Get("today total earnings"), false);
        }

        // What the guests left on top, that night.
        private (string Value, string Label, bool Alarm) Tips()
        {
            if (Summary.HasDay && !Summary.HasLines)
                return ("–", Tr.Get("tips tonight"), false);
            var tips = Summary.Ledger.Of(LedgerLine.Tips);
            // The caption follows how generous they were for what they spent (DaySummary.TipsMood).
            var caption = DaySummary.TipsMood(tips, Summary.Ledger.Of(LedgerLine.Payments)) switch
            {
                DaySummary.TipMood.None => Tr.Get("tips tonight · not a penny"),
                DaySummary.TipMood.Polite => Tr.Get("tips tonight · polite, at best"),
                DaySummary.TipMood.Loved => Tr.Get("tips tonight · they loved you"),
                _ => Tr.Get("tips tonight · they liked you"),
            };
            return (Money(tips), caption, false);
        }

        private static string Capitalised(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        // ---- Tabs ----

        private void DrawTabs()
        {
            UiKit.Clear(_tabs);
            Keycap(_tabs, "LB", RightX, 185f, 54f, 36f);
            Keycap(_tabs, "RB", RightEdge - 54f, 185f, 54f, 36f);
            UiKit.RoundBox(_tabs, "Rule", _rule, RightX, 233f, RightWidth, 3f, 0f);
            var x = 846f;
            foreach (Tab tab in System.Enum.GetValues(typeof(Tab)))
            {
                var text = TabName(tab);
                var active = tab == _tab;
                var label = Label(active ? Bold : SemiBold, _tabs, text, 28.33f, active ? _ink : _quiet, x, active ? 207f : 209f, 300f);
                var width = UiKit.Width(label);
                if (active)
                    UiKit.RoundBox(_tabs, "Underline", _gold, x - 22f, 230f, width + 44f, 6f, 0f);
                x += width + 50f;
            }
        }

        // ---- Overview: the boss first, the night's numbers, then every review ----

        private const float BossWidth = 604f, TopRowHeight = 150f, FilterTop = 440f;

        private void DrawOverview()
        {
            DrawBoss(RightX, 262f);

            var day = Summary;
            var x = RightX + BossWidth + 16f;
            var width = (RightEdge - x - 2f * 12f) / 3f;
            SmallStat(x, width, $"{day.Served}", Tr.Get("Served"), false);
            SmallStat(x + width + 12f, width, $"{day.WalkOuts}", Tr.Get("Walked out"), day.WalkOuts > 0);
            SmallStat(x + 2f * (width + 12f), width, $"{day.TurnedAway}", Tr.Get("Turned away"), day.TurnedAway > 0);

            var all = AllReviews();
            var reviews = ListedReviews();
            var chipX = RightX;
            foreach (Filter filter in System.Enum.GetValues(typeof(Filter)))
                chipX = FilterChip(filter, all.Count(r => Matches(r, filter)), chipX, FilterTop) + 12f;
            Label(Medium, _content, Tr.Get("Sorted: needs attention first"), 19.5f, _muted, RightEdge, FilterTop + 26f, 400f, HorizontalAlignmentOptions.Right);

            if (reviews.Count == 0)
            {
                Label(Serif, _content, all.Count == 0 ? Tr.Get("No reviews yet.") : Tr.Get("No reviews match this filter."), 26.67f, _muted, RightX, ListTop + 42f);
                return;
            }

            // Scroll only as far as the chosen card needs to be whole (ScrollWindow): a step moves
            // the highlight, not the list.
            var heights = reviews.Select(r => CardHeight(r, false) + 14f).ToList();
            var first = _reviewFirst = ScrollWindow.First(_reviewFirst, Selected, heights, ListBottom - ListTop + 14f);
            var y = ListTop;
            for (var i = first; i < reviews.Count && y < ListBottom; i++)
                y = ReviewCard(_list, reviews[i], y, i == Selected && !OnFilters, false) + 14f;
        }

        // The boss's card, the Overview's headline, after the user's simpler frame (2026-11-10):
        // "THE BOSS", the debt collection and its amount, an orange bar as far as the balance covers
        // it, and what you have against what's short. Short with two days or fewer left, it pulses
        // and the boss appears, frowning.
        private const float BossHeight = 150f;

        private void DrawBoss(float x, float y)
        {
            var days = Services.Days;
            var inner = BossWidth - 52f;
            // As the finished day ended: a collection that night shows what he took; otherwise the
            // next one, counted from the end of that day ("tomorrow" is the next service).
            var record = Summary.Record;
            var collected = record != null && record.Collected;
            var card = BossCard.For(days.NextCollection.Due, days.Balance, days.DaysUntilFee,
                false, Services.Boss.Step, Services.RestaurantsOwned);
            var urgent = !collected && card.Urgent;

            _bossGlow = UiKit.RoundBox(_content, "BossGlow", Alpha(_shortBy, 0f), x - 7f, y - 7f, BossWidth + 14f, BossHeight + 14f, 24f);
            _bossUrgent = urgent;
            UiKit.RoundBox(_content, "Boss", _ink, x, y, BossWidth, BossHeight, 18f);
            Caps(_content, Tr.Get("THE BOSS"), x + 26f, y + 34f, _gold);
            // How the money stands (2026-11-11): green when it's covered, amber when only just, orange
            // when short; short and close, the card pulses. No avatar (the user's call).
            var standing = collected
                ? (record.CollectionPaid >= record.CollectionDue - 0.005f ? BossCard.Standing.Covered : BossCard.Standing.Short)
                : card.Stands;
            var tone = standing == BossCard.Standing.Covered ? _goodLight : standing == BossCard.Standing.JustCovered ? _gold : _shortBy;

            string lead, left, right = null;
            float covered;
            if (collected)
            {
                // Collection night: what he took.
                var due = record.CollectionDue;
                var paid = record.CollectionPaid;
                var paidInFull = paid >= due - 0.005f;
                covered = due > 0f ? Mathf.Clamp01(paid / due) : 1f;
                lead = Tr.Format("Debt collection taken: {amount}", ("amount", Money(due)));
                left = paidInFull ? Tr.Get("Paid in full") : Tr.Format("He took {paid}", ("paid", Money(paid)));
                right = paidInFull ? Tr.Get("He's happy. For now.") : Tr.Format("Short by {amount}", ("amount", Money(due - paid)));
            }
            else
            {
                covered = card.Covered;
                lead = card.Falls == BossCard.When.Tonight ? Tr.Format("Debt collection tonight: {amount}", ("amount", Money(card.Due)))
                    : card.Falls == BossCard.When.Tomorrow ? Tr.Format("Debt collection tomorrow: {amount}", ("amount", Money(card.Due)))
                    : Tr.Format("Debt collection in {days} days: {amount}", ("days", card.DaysLeft), ("amount", Money(card.Due)));
                left = Tr.Format("You have {amount}", ("amount", Money(days.Balance)));
                right = standing == BossCard.Standing.Short ? Tr.Format("Short by {amount}", ("amount", Money(card.Shortfall)))
                    : standing == BossCard.Standing.JustCovered ? Tr.Format("Just covers it · {spare} to spare", ("spare", Money(card.Spare)))
                    : Tr.Format("Covered · {spare} to spare", ("spare", Money(card.Spare)));
            }

            // Shrinks to fit rather than cutting the amount off.
            var leadLabel = Label(ExtraBold, _content, lead, 34f, _paper, x + 26f, y + 78f, inner, tracking: -0.8f);
            leadLabel.enableAutoSizing = true;
            leadLabel.fontSizeMin = 24f;
            leadLabel.fontSizeMax = 34f;
            UiKit.RoundBox(_content, "CoverTrack", _wellLight, x + 26f, y + 94f, BossWidth - 52f, 12f, 6f);
            if (covered > 0f)
                UiKit.RoundBox(_content, "CoverFill", tone, x + 26f, y + 94f, Mathf.Max(12f, (BossWidth - 52f) * covered), 12f, 6f);
            // The money chip's coin before the money (the user's mock, 2026-11-11).
            // Only where the line is the balance ("You have $810"), not on a collection night.
            var coin = collected ? 0f : 24f;
            if (!collected)
            {
                UiKit.StrokedCircle(_content, "Coin", Color.Lerp(_style.MoneyEdge, Color.white, 0.25f), Color.Lerp(_style.MoneyEdge, Color.black, 0.3f), 2.5f, x + 26f, y + 113f, coin);
                UiKit.Ellipse(_content, "CoinFace", Color.Lerp(_style.MoneyEdge, Color.black, 0.15f), x + 26f + coin * 0.25f, y + 113f + coin * 0.25f, coin * 0.5f, coin * 0.5f);
            }
            Label(Bold, _content, left, 19.5f, _sand, x + 26f + (collected ? 0f : coin + 8f), y + 132f, 240f);
            if (right != null)
                Label(Bold, _content, right, 19.5f, tone, x + BossWidth - 26f, y + 132f, 340f, HorizontalAlignmentOptions.Right);
        }

        private bool _bossUrgent;

        // A gentle pulse of the glow round the card while it's urgent: about one a second.
        private void PulseBoss()
        {
            if (_bossGlow == null)
                return;
            var alpha = _bossUrgent && _tab == Tab.Overview ? 0.15f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * _bossPulsePerSecond)) : 0f;
            _bossGlow.color = Alpha(_shortBy, alpha);
        }

        private void SmallStat(float x, float width, string number, string label, bool alarm)
        {
            UiKit.RoundBox(_content, label, alarm ? _alarm : _card, x, 262f, width, TopRowHeight, 16f);
            Label(ExtraBold, _content, number, 46f, alarm ? _badText : _ink, x + 20f, 262f + 76f, width - 30f, tracking: -1.5f);
            var text = UiKit.TextAt(Medium, _content, label, label, 18f, alarm ? _alarmText : _body, x + 20f, 262f + 108f, width - 30f, lines: 2);
            LineHeight(text, 22f);
        }

        // One filter chip, such as "Walk-outs · 2" or "1–2 ★ · 1", with its top at y; returns its right
        // edge. The active filter is dark, as "All" is in the mockup; with the focus on the chips it
        // also has a gold ring.
        private float FilterChip(Filter filter, int count, float x, float y)
        {
            var active = filter == ShownFilter;
            var back = active ? _ink : filter == Filter.WalkOuts ? _alarm : _card;
            var fore = active ? _paper : filter == Filter.WalkOuts ? _alarmText : _ink;
            var star = filter == Filter.Low || filter == Filter.High;
            var text = filter switch
            {
                Filter.All => Tr.Format("All · {count}", ("count", count)),
                Filter.WalkOuts => Tr.Format("Walk-outs · {count}", ("count", count)),
                Filter.Low => "1–2",
                Filter.High => "3+",
                _ => Tr.Format("Unreplied · {count}", ("count", count)),
            };

            var first = Label(SemiBold, _content, text, 19.5f, fore, x + 16f, y + 26f, 300f);
            var right = x + 16f + UiKit.Width(first);
            if (star)
            {
                UiKit.Picture(_content, "Star", StarSprite, right + 6f, y + 11f, 17f, 17f, fore);
                var after = Label(SemiBold, _content, $"· {count}", 19.5f, fore, right + 29f, y + 26f, 100f);
                right += 29f + UiKit.Width(after);
            }
            var width = right + 16f - x;

            var index = first.transform.GetSiblingIndex();
            var fill = UiKit.RoundBox(_content, text + "Chip", back, x, y, width, 39f, 19.5f);
            fill.transform.SetSiblingIndex(index);
            if (active && OnFilters)
            {
                var ring = UiKit.RoundBox(_content, "Focus", _gold, x - 4f, y - 4f, width + 8f, 47f, 23.5f);
                ring.transform.SetSiblingIndex(index);
            }
            return x + width;
        }
        // The finished day the cards are dated against; a capture sets its own.
        private int? _captureToday;
        private int Today => _captureToday ?? Summary.Day;

        /// <summary>
        /// Draws review cards at once, without Play mode or a running kitchen, for the capture tool
        /// (Tools/Game/Capture Review Cards): <paramref name="reviews"/> top to bottom on the dashboard's
        /// paper, dated against <paramref name="today"/>, with <paramref name="selected"/> chosen.
        /// </summary>
        public Canvas DrawCardsForCapture(IReadOnlyList<ReviewRecord> reviews, int today, int selected)
        {
            _captureToday = today;
            var canvasObject = new GameObject("ReviewCardsCapture", typeof(RectTransform));
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var root = UiKit.Rect(canvasObject.transform, "Layout", 0f, 0f, 1920f, 1080f);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            UiKit.RoundBox(root, "Paper", _paper, 0f, 0f, 1920f, 1080f, 0f);
            _measure = UiKit.TextAt(Medium, root, "Measure", "", ReplySize, Color.clear, -4000f, -4000f, ContentWidth, lines: 4);
            var y = 40f;
            for (var i = 0; i < reviews.Count && y < 1040f; i++)
                y = ReviewCard(root, reviews[i], y, i == selected, false) + 14f;
            _captureToday = null;
            return canvas;
        }

        // ---- Review cards, as speech bubbles (the user's pick, option B, 2026-11-11) ----
        // Left, the guest: their portrait, name and what kind of guest they are. Right, what they
        // said in a bubble in upright Lora, the owner's answer in a darker bubble from the other side, then a quiet
        // line of facts: the rating, what they had and paid. No quotation marks: the bubble says it.

        private const float CardPad = 22f, GuestColumn = 112f, GuestGap = 18f, Portrait = 84f;
        private const float QuoteSize = 30f, QuoteSmallest = 22f, QuoteInset = 28f;
        // Line pitch as a share of the type size: 42 px at 30, looser than before (the user's call).
        private const float QuoteLeading = 1.4f;
        private const float ReplySize = 24f, ReplyPitch = 33f, ReplyIndent = 56f, ReplyInset = 22f;
        // The reply's bubble is snugger than the review's (the user's call): bigger type, less padding.
        private const float ReplyTop = 11f, ReplyBottom = 21f;
        // Room inside a bubble above the first line and below the last (the user's call: more air).
        private const float BubbleTop = 15f, BubbleBottom = 28f; // the capitals sit below the size line, so less above looks even
        private const float BubbleGap = 12f, FactsHeight = 44f, BubbleCorner = 18f, TailCorner = 5f;
        // A review is never more than two lines (the user's call, 2026-11-11): a long one is set a
        // little smaller until it fits, down to QuoteSmallest, and only then cut with an ellipsis.
        private const int QuoteMaxLines = 2, ReplyMaxLines = 3;

        private static float ContentX => CardLeft + CardPad + GuestColumn + GuestGap;
        private static float ContentWidth => CardWidth - CardPad - GuestColumn - GuestGap - CardPad;

        // The guest's column: portrait, gap, name, gap, type chip.
        private const float GuestHeight = Portrait + 10f + 24f + 8f + 28f;

        private TextMeshProUGUI _measure;

        private float CardHeight(ReviewRecord review, bool overview)
        {
            var fit = QuoteFit(review);
            var right = Bubble(fit.Lines, fit.Size, fit.Size * QuoteLeading) + FactsHeight;
            if (review.Reply >= 0)
                right += BubbleGap + Bubble(Lines(ShownReply(review), Medium, ReplySize, ContentWidth - ReplyIndent - 2f * ReplyInset, ReplyMaxLines), ReplySize, ReplyPitch, ReplyTop, ReplyBottom);
            return CardPad + Mathf.Max(right, GuestHeight) + CardPad - 6f;
        }

        // A bubble's height for this many lines: padding, the first line, then the pitch per line.
        private static float Bubble(int lines, float size, float pitch, float above = BubbleTop, float below = BubbleBottom) => above + size + (lines - 1) * pitch + below;

        // The biggest size, from QuoteSize down, at which the review fits in two lines.
        private (float Size, int Lines) QuoteFit(ReviewRecord review)
        {
            var text = Game.Data.ReviewText.Line(review);
            for (var size = QuoteSize; size > QuoteSmallest; size -= 1f)
            {
                var lines = Lines(text, Upright, size, ContentWidth - 2f * QuoteInset, 99);
                if (lines <= QuoteMaxLines)
                    return (size, lines);
            }
            return (QuoteSmallest, QuoteMaxLines);
        }

        // How many lines text wraps to, measured with its own font, size and width.
        private int Lines(string text, TMP_FontAsset font, float size, float width, int most)
        {
            if (_measure == null || string.IsNullOrEmpty(text) || font == null)
                return 1;
            // Laid out as the label will be, and its lines counted (a height divided by a line
            // height miscounts, as the first and last lines carry the ascent and descent).
            _measure.font = font;
            _measure.fontSize = size;
            _measure.textWrappingMode = TextWrappingModes.Normal;
            _measure.overflowMode = TextOverflowModes.Overflow;
            _measure.maxVisibleLines = 99;
            _measure.rectTransform.sizeDelta = new Vector2(width, 4000f);
            _measure.text = text;
            _measure.ForceMeshUpdate();
            return Mathf.Clamp(_measure.textInfo.lineCount, 1, most);
        }

        // TextMeshPro spaces lines by the font's own line height; this sets an exact pitch instead.
        private static void LineHeight(TextMeshProUGUI label, float pixels)
        {
            var face = label.font.faceInfo;
            var natural = face.lineHeight * label.fontSize / face.pointSize;
            label.lineSpacing = (pixels - natural) / label.fontSize * 100f;
        }

        // Replies from before replies were written per review fall back to the fixed lines.
        // Written in the player's language from its templates (decision 2026-11-09).
        private string ShownReply(ReviewRecord review)
        {
            var written = Game.Data.ReviewText.Reply(review);
            return string.IsNullOrEmpty(written) ? ReplyText(review) : written;
        }

        // What kind of guest they were (the user's call, 2026-11-11), so a critic or an influencer
        // stands out once they've posted: their own colours; everyone else a quiet chip.
        private (string Name, Color Back, Color Text) GuestType(ReviewRecord review) => (Persona)review.Persona switch
        {
            Persona.Critic => (Tr.Get("Food critic"), _gold, _ink),
            Persona.Influencer => (Tr.Get("Influencer"), Hex(0xe58fae), _ink),
            Persona.Foodie => (Tr.Get("Foodie"), Hex(0xcfe3d6), _goodText),
            Persona.Tourist => (Tr.Get("Tourist"), _card, _body),
            Persona.Local => (Tr.Get("Local"), _card, _body),
            _ => (Tr.Get("Regular"), _card, _body),
        };

        // A speech bubble: rounded, with one sharp corner pointing at whoever's talking.
        private void SpeechBubble(Transform parent, string name, Color fill, float x, float y, float width, float height, bool tailLeft)
        {
            UiKit.RoundBox(parent, name, fill, x, y, width, height, BubbleCorner);
            UiKit.RoundBox(parent, name + "Tail", fill, tailLeft ? x : x + width - BubbleCorner, y + height - BubbleCorner, BubbleCorner, BubbleCorner, TailCorner);
        }

        // One review card at y; returns its bottom.
        private float ReviewCard(Transform parent, ReviewRecord review, float y, bool selected, bool overview)
        {
            var height = CardHeight(review, overview);
            var walkedOut = review.WalkedOut;
            if (selected)
                UiKit.RoundBox(parent, "Highlight", _gold, CardLeft - 5.67f, y - 5.67f, CardWidth + 11.33f, height + 11.33f, 20f);
            UiKit.StrokedBox(parent, "Review", walkedOut ? Hex(0xfcefe6) : _cardLight, selected ? _gold : walkedOut ? _alarm : _cardEdge, 2.67f, CardLeft, y, CardWidth, height, 15f);

            // The guest: the customer they came as, their name, what kind of guest (2026-11-11).
            var type = GuestType(review);
            var critic = (Persona)review.Persona == Persona.Critic;
            var columnCentre = CardLeft + CardPad + GuestColumn / 2f;
            var top = y + CardPad;
            UiKit.GuestPortrait(parent, _style.CustomerPortrait(review.Look, review.Guest), review.Guest,
                columnCentre - Portrait / 2f, top, Portrait, critic ? _gold : _ink);
            Label(Bold, parent, Game.Data.GuestNames.Display(review), 19f, _ink, columnCentre, top + Portrait + 10f + 20f, GuestColumn + 16f, HorizontalAlignmentOptions.Center);
            var chip = Label(critic ? Bold : SemiBold, parent, type.Name, 15f, type.Text, columnCentre, top + Portrait + 10f + 24f + 8f + 19f, GuestColumn + 16f, HorizontalAlignmentOptions.Center);
            var chipWidth = Mathf.Min(GuestColumn + 16f, UiKit.Width(chip) + 22f);
            var chipBox = UiKit.RoundBox(parent, "Type", type.Back, columnCentre - chipWidth / 2f, top + Portrait + 10f + 24f + 8f, chipWidth, 26f, 13f);
            chipBox.transform.SetSiblingIndex(chip.transform.GetSiblingIndex());

            // What they said, in their bubble. Written in the player's language (decision 2026-11-09).
            var fit = QuoteFit(review);
            var pitch = fit.Size * QuoteLeading;
            var bubble = Bubble(fit.Lines, fit.Size, pitch);
            SpeechBubble(parent, "Said", walkedOut ? _alarm : Hex(0xf1e5cf), ContentX, top, ContentWidth, bubble, tailLeft: true);
            var quote = UiKit.TextAt(Upright, parent, "Quote", Game.Data.ReviewText.Line(review), fit.Size, _ink, ContentX + QuoteInset, top + BubbleTop + fit.Size, ContentWidth - 2f * QuoteInset, lines: QuoteMaxLines);
            // Past the smallest size (about 150 characters), the third line on is left out.
            LineHeight(quote, pitch);
            var below = top + bubble;

            // The owner's answer: bigger, from the other side, no heading (the user's call).
            if (review.Reply >= 0)
            {
                var replyWidth = ContentWidth - ReplyIndent;
                var replyLines = Lines(ShownReply(review), Medium, ReplySize, replyWidth - 2f * ReplyInset, ReplyMaxLines);
                var replyHeight = Bubble(replyLines, ReplySize, ReplyPitch, ReplyTop, ReplyBottom);
                below += BubbleGap;
                SpeechBubble(parent, "Answer", Hex(0x2f4a3f), ContentX + ReplyIndent, below, replyWidth, replyHeight, tailLeft: false);
                var reply = UiKit.TextAt(Medium, parent, "Reply", ShownReply(review), ReplySize, Hex(0xf3ead9), ContentX + ReplyIndent + ReplyInset, below + ReplyTop + ReplySize, replyWidth - 2f * ReplyInset, lines: ReplyMaxLines);
                LineHeight(reply, ReplyPitch);
                below += replyHeight;
            }

            // The facts, under the conversation: the rating, what they had and what it paid.
            var baseline = below + 32f;
            float x;
            if (walkedOut)
                x = ContentX + 4f + UiKit.Width(Label(Bold, parent, Tr.Get("Walked out"), 20f, _badText, ContentX + 4f, baseline, 300f));
            else
            {
                var score = Label(ExtraBold, parent, $"{review.RatedStars:0.0}", 21f, _ink, ContentX + 4f, baseline, 60f);
                x = Stars(parent, ContentX + 4f + UiKit.Width(score) + 8f, baseline - 19f, 21f, 25f, review.RatedStars);
            }
            var facts = string.IsNullOrEmpty(review.Order) ? "" : $" · {Game.Data.ReviewText.Order(review.Order)}";
            facts += walkedOut ? $" · {Money(-review.Penalty)}"
                : review.Tip > 0f ? " · " + Tr.Format("{paid} + {tip} tip", ("paid", Money(review.Paid)), ("tip", Money(review.Tip)))
                : " · " + Tr.Format("{paid} · no tip", ("paid", Money(review.Paid)));
            if (review.Day != Today)
                facts += Tr.Format(" · day {day}", ("day", review.Day));
            // A guest who's posted before (decision 2026-11-09).
            if (review.Visit == 2)
                facts += Tr.Get(" · back again");
            else if (review.Visit > 2)
                facts += Tr.Format(" · visit {visit}", ("visit", review.Visit));
            Label(Medium, parent, facts, 19f, walkedOut ? _alarmText : _muted, x + 6f, baseline, ContentWidth - (x - ContentX) - 150f);

            var right = CardLeft + CardWidth - CardPad;
            if (review.Reply >= 0)
                Label(Medium, parent, Tr.Get("Answered"), 18f, _quiet, right, baseline, 200f, HorizontalAlignmentOptions.Right);
            else if (selected)
                ReplyButton(parent, right, baseline - 7f);
            return y + height;
        }

        // The Y Reply button, its right edge at right and centred on cy; with the wheel open it's pressed in.
        private void ReplyButton(Transform parent, float right, float cy)
        {
            var label = Label(Bold, parent, Tr.Get("Reply"), 18.67f, _choosingReply ? _paper : _ink, 0f, cy + 7f, 120f);
            var width = UiKit.Width(label);
            var badgeX = right - width - 9f - 31f;
            if (_choosingReply)
            {
                // Pressed in: a dark pill with a gold edge, behind the badge and the label.
                var index = label.transform.GetSiblingIndex();
                var fill = UiKit.StrokedBox(parent, "Pressed", _ink, _gold, 4f, badgeX - 7f, cy - 21f, width + 31f + 9f + 22f, 42f, 21f);
                var edge = fill.transform.parent.GetChild(fill.transform.GetSiblingIndex() - 1);
                edge.SetSiblingIndex(index);
                fill.transform.SetSiblingIndex(index + 1);
            }
            YBadge(parent, badgeX + 15.5f, cy, 31f);
            label.rectTransform.anchoredPosition = new Vector2(badgeX + 31f + 9f, label.rectTransform.anchoredPosition.y);
            if (_choosingReply)
                DrawWheel(badgeX + 15.5f, cy);
        }

        // The reply wheel: each reply on the face button in its place, B to leave it (decision 2026-11-09).
        private void DrawWheel(float badgeX, float badgeY)
        {
            const float width = 562f, height = 391f;
            // Right-aligned inside the review card, as in the mockup; above the button when there's no room below.
            const float x = 1780f - width;
            var below = badgeY + 35.5f + height < FooterTop - 10f;
            var y = below ? badgeY + 35.5f : badgeY - 35.5f - height;

            // The tail points at the Reply button.
            var tailY = below ? y - 9f : y + height - 9f;
            var tail = UiKit.RoundBox(_popover, "Tail", _gold, badgeX - 12f, tailY, 24f, 24f, 2f);
            tail.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tail.rectTransform.anchoredPosition += new Vector2(12f, -12f);
            tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            UiKit.StrokedBox(_popover, "Wheel", _ink, _gold, 4f, x, y, width, height, 22f);
            var inner = UiKit.RoundBox(_popover, "TailInner", _ink, badgeX - 9f, tailY + 3f, 18f, 18f, 1f);
            inner.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            inner.rectTransform.anchoredPosition += new Vector2(9f, -9f);
            inner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var hx = x + 299f;
            var hy = y + 191f;
            UiKit.StrokedCircle(_popover, "Hub", Hex(0x1e1712), _wellLight, 4f, hx - 35f, hy - 35f, 70f);
            Badge(_popover, "Y", _buttonY, hx, hy - 64.5f, 55f, 24f);
            Badge(_popover, "X", _buttonX, hx - 64f, hy, 55f, 24f);
            Badge(_popover, "B", _buttonB, hx + 64f, hy, 55f, 24f);
            Badge(_popover, "A", _buttonA, hx, hy + 64.5f, 55f, 24f);
            Label(ExtraBold, _popover, Tr.Get("Thank"), 34.67f, _paper, hx, hy - 143f, 400f, HorizontalAlignmentOptions.Center, -0.7f);
            Label(ExtraBold, _popover, Tr.Get("Apologise"), 34.67f, _paper, hx - 105f, hy - 3f, 300f, HorizontalAlignmentOptions.Right, -0.7f);
            Label(ExtraBold, _popover, Tr.Get("Leave it"), 34.67f, _paper, hx + 108f, hy - 4f, 300f, tracking: -0.7f);
            Label(ExtraBold, _popover, Tr.Get("Stand your ground"), 34.67f, _paper, hx, hy + 134f, 520f, HorizontalAlignmentOptions.Center, -0.7f);
        }

        // ---- Books: the finished day's money, what they ordered, then where it went (2026-11-11) ----

        private void DrawBooks()
        {
            // The finished day's books (the user's call, 2026-11-11), even after a relaunch.
            var ledger = Summary.Ledger;
            var lines = ((LedgerLine[])System.Enum.GetValues(typeof(LedgerLine))).Where(l => ledger.Of(l) != 0f).ToList();
            DrawToday(ledger, lines);
            DrawTonightsMenu();
            DrawMoneyWent(ledger, lines);
            DrawTrendAndTeam();
        }

        // Each dish ordered that night, at the price it sold for, with how many ordered it, how many
        // gave up waiting and the stars it got; walk-outs tint the row. From the day's record, so it's
        // the finished day's even after a relaunch (2026-11-11).
        private void DrawTonightsMenu()
        {
            const float x = 1293f, width = 507f, top = 262f, height = 301f, rowHeight = 54f;
            UiKit.RoundBox(_content, "Menu", _card, x, top, width, height, 18f);
            Caps(_content, Tr.Get("WHAT THEY ORDERED"), x + 26f, top + 38f);
            var dishes = Summary.Dishes.OrderByDescending(d => d.Ordered).ToList();
            if (dishes.Count == 0)
            {
                Label(Serif, _content, Summary.HasDay && !Summary.HasLines ? Tr.Get("Not kept for this day.") : Tr.Get("Nobody ordered."), 24f, _muted, x + 26f, top + 100f);
                return;
            }

            var rows = Mathf.Min(dishes.Count, 4);
            var y = top + 58f;
            for (var i = 0; i < rows; i++)
            {
                var dish = dishes[i];
                if (dish.WalkedOut > 0)
                    UiKit.RoundBox(_content, "Alarm", _alarm, x + 12f, y, width - 24f, rowHeight - 6f, 12f);
                Label(Bold, _content, DishName(dish), 21.33f, _ink, x + 26f, y + 24f, 330f);
                var said = Tr.Plural(dish.Ordered, "1 ordered", "{n} ordered");
                if (dish.WalkedOut > 0)
                    said += " · " + Tr.Plural(dish.WalkedOut, "1 walked out", "{n} walked out");
                if (dish.Stars.HasValue)
                    said += " · " + Tr.Format("{stars} stars", ("stars", dish.Stars.Value.ToString("0.0")));
                Label(Medium, _content, said, 16.67f, dish.WalkedOut > 0 ? _alarmText : _muted, x + 26f, y + 45f, 340f);
                if (dish.Price > 0)
                    Label(ExtraBold, _content, Money(dish.Price), 24f, _ink, x + width - 26f, y + 26f, 120f, HorizontalAlignmentOptions.Right);
                y += rowHeight;
            }
            if (dishes.Count > rows)
                Label(Medium, _content, Tr.Plural(dishes.Count - rows, "1 more dish", "{n} more dishes"), 16.67f, _muted, x + 26f, top + height - 14f, 450f);
        }

        // The recipe's name when the cookbook knows it, otherwise the order as guests said it.
        private string DishName(DishNight dish)
        {
            var recipe = Services.Cookbook?.Recipes.FirstOrDefault(r => r.Id == dish.RecipeId);
            return recipe != null ? Tr.Get(recipe.Name) : Capitalised(dish.Dish);
        }

        // "+20%" or "−10%" against the suggested price.
        private static string Percent(float ratio)
        {
            var percent = Mathf.RoundToInt((ratio - 1f) * 100f);
            return percent > 0 ? $"+{percent}%" : $"−{-percent}%";
        }

        // ---- Staff: the team, then today's candidates (the user's call, 2026-11-10) ----

        private const float MemberHeight = 96f, CandidateHeight = 76f, RowGap = 10f;

        private void DrawStaff()
        {
            var staff = Services.Staff;
            var rows = StaffBoard.Rows(staff);
            var selected = Mathf.Clamp(Selected, 0, Mathf.Max(0, rows.Count - 1));
            var balance = Services.Days.Balance;
            var y = 262f;

            Caps(_content, Tr.Get("YOUR TEAM"), RightX, y + 18f);
            if (staff.Hired.Count > 0)
                Label(Medium, _content, Tr.Format("Wages {amount} a day", ("amount", Money(staff.TotalWages))), 19.5f, _muted, RightEdge, y + 18f, 500f, HorizontalAlignmentOptions.Right);
            y += 32f;
            if (staff.Hired.Count == 0)
            {
                Label(Serif, _content, Tr.Get("Nobody on the team yet."), 24f, _muted, RightX, y + 28f, 900f);
                y += 44f;
            }

            for (var i = 0; i < rows.Count && !rows[i].IsCandidate; i++)
            {
                MemberCard(rows[i].Member, y, i == selected);
                y += MemberHeight + RowGap;
            }

            y += 8f;
            Caps(_content, Tr.Get("LOOKING FOR WORK TODAY"), RightX, y + 18f);
            y += 32f;
            var candidates = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                if (!rows[i].IsCandidate)
                    continue;
                CandidateCard(rows[i].Candidate, StaffBoard.WhyNot(staff, rows[i].Candidate, balance), y, i == selected);
                y += CandidateHeight + RowGap;
                candidates++;
            }
            if (candidates == 0)
            {
                Label(Serif, _content, Tr.Get("Nobody else is looking for work today."), 24f, _muted, RightX, y + 28f, 900f);
                y += 44f;
            }

            // What the last press did, or, in Review, when hiring happens.
            var (text, colour) = StaffNote();
            if (!string.IsNullOrEmpty(text))
                Label(SemiBold, _content, text, 19.5f, colour, RightX, Mathf.Min(y + 18f, 940f), 1030f);
        }

        private (string Text, Color Colour) StaffNote()
        {
            var name = _staffResultName ?? "";
            switch (_staffResult)
            {
                case StaffBoard.Outcome.Hired:
                    return (Tr.Format("{name} starts today.", ("name", name)), _goodText);
                case StaffBoard.Outcome.LetGo:
                    return (Tr.Format("{name} has been let go.", ("name", name)), _body);
                case StaffBoard.Outcome.CantAfford:
                    return (Tr.Format("Not enough money for {name}: wages are paid each night.", ("name", name)), _badText);
                case StaffBoard.Outcome.RoleTaken:
                    return (Tr.Get("Someone already does that job: let them go first."), _badText);
                case StaffBoard.Outcome.ReadOnly:
                    return (Tr.Get("Hiring is for the morning, before prep."), _badText);
            }
            return (null, _body);
        }

        private void MemberCard(Game.Simulation.Staff.StaffMember member, float y, bool chosen)
        {
            UiKit.StrokedBox(_content, "Member", _cardLight, chosen ? _gold : _cardEdge, chosen ? 4f : 2.67f, CardLeft, y, CardWidth, MemberHeight, 15f);
            UiKit.StrokedEllipse(_content, "Avatar", UiKit.AvatarColour(member.Name), _ink, 4f, CardLeft + 24f, y + 18f, 49f, 61f);
            var name = Label(Bold, _content, member.Name, 22f, _ink, 857f, y + 32f, 300f);
            Label(Medium, _content, "· " + ServiceStatusHud.RoleName(member.Role), 19f, _muted, 857f + UiKit.Width(name) + 10f, y + 32f, 300f);
            Label(Serif, _content, $"“{Tr.Get(member.Description)}”", 21f, _ink, 857f, y + 60f, 600f);
            Label(Medium, _content, ServiceStatusHud.WorkDone(member), 17.33f, _body, 857f, y + 86f, 620f);

            var memberWage = Label(Bold, _content, Tr.Format("{wage} a day", ("wage", Money(member.DailyWage))), 21.33f, _ink, 1776f, y + 32f, 260f, HorizontalAlignmentOptions.Right);
            // The action is on the row itself: Y lets the chosen member go, in the morning.
            if (chosen && StaffEditable)
                Chip(_content, "Y", Tr.Get("Let go"), 1480f, y + 10f);
            Meter(Tr.Get("Skill"), member.Skill, 1500f, y + 42f);
            Meter(Tr.Get("Speed"), member.Speed / 2f, 1500f, y + 62f); // 1 is an average pace: the middle of the bar
            Label(SemiBold, _content, ServiceStatusHud.Mood(member), 17.33f, _body, 1776f, y + 88f, 260f, HorizontalAlignmentOptions.Right);
        }

        // Someone looking for work: their words, not numbers (as on the old Plan card), and the wage.
        // Greyed, with the reason, when they're out of reach.
        private void CandidateCard(Game.Simulation.Staff.Candidate candidate, StaffBoard.Outcome whyNot, float y, bool chosen)
        {
            var reachable = whyNot == StaffBoard.Outcome.Nothing;
            var text = reachable ? _ink : _quiet;
            UiKit.StrokedBox(_content, "Candidate", reachable ? _card : Alpha(_card, 0.55f), chosen ? _gold : _cardEdge, chosen ? 4f : 2.67f, CardLeft, y, CardWidth, CandidateHeight, 15f);
            UiKit.StrokedEllipse(_content, "Avatar", reachable ? UiKit.AvatarColour(candidate.Name) : _rule, reachable ? _ink : _sand, 4f, CardLeft + 28f, y + 14f, 40f, 50f);
            var name = Label(Bold, _content, candidate.Name, 22f, text, 857f, y + 32f, 300f);
            Label(Medium, _content, "· " + ServiceStatusHud.RoleName(candidate.Role), 19f, reachable ? _muted : _quiet, 857f + UiKit.Width(name) + 10f, y + 32f, 300f);
            Label(Serif, _content, $"“{Tr.Get(candidate.Description)}”", 21f, text, 857f, y + 62f, 600f);

            var wage = Label(Bold, _content, Tr.Format("{wage} a day", ("wage", Money(candidate.DailyWage))), 21.33f, reachable ? _ink : _badText, 1776f, y + 32f, 260f, HorizontalAlignmentOptions.Right);
            // A hires the chosen candidate, in the morning; holding Y is the Start button's.
            if (chosen && reachable && StaffEditable)
                Chip(_content, "A", Tr.Get("Hire"), 1776f - UiKit.Width(wage) - 24f, y + 15f);
            var reason = whyNot == StaffBoard.Outcome.RoleTaken ? Tr.Get("job taken")
                : whyNot == StaffBoard.Outcome.CantAfford ? Tr.Get("out of reach") : "";
            if (reason.Length > 0)
                Label(SemiBold, _content, reason, 17.33f, _badText, 1776f, y + 62f, 260f, HorizontalAlignmentOptions.Right);
        }

        // A label and a short bar, 0..1, right-aligned to the card's edge.
        private void Meter(string label, float value, float x, float y)
        {
            Label(Medium, _content, label, 16f, _muted, x, y + 12f, 90f);
            UiKit.RoundBox(_content, "Track", _rule, x + 90f, y, 186f, 12f, 6f);
            UiKit.RoundBox(_content, "Fill", _gold, x + 90f, y, Mathf.Max(12f, 186f * Mathf.Clamp01(value)), 12f, 6f);
        }
        private string TabName(Tab tab) => tab switch
        {
            Tab.Overview => Tr.Get("Overview"),
            Tab.Restaurant => Tr.Get("Restaurant"),
            Tab.Books => Tr.Get("Books"),
            _ => Tr.Get("Staff"),
        };

        // Today's money in and out, line by line.
        private void DrawToday(Ledger ledger, List<LedgerLine> lines)
        {
            UiKit.RoundBox(_content, "Today", _card, RightX, 262f, 507f, 301f, 18f);
            Caps(_content, Tr.Format("DAY {day}", ("day", Summary.Day)), 790f, 300f);
            // A day from before line-by-line books (2026-11-11): say so rather than look broken.
            if (Summary.HasDay && !Summary.HasLines)
                Label(Medium, _content, Tr.Get("only the totals were kept"), 16.67f, _muted, 1244f, 300f, 300f, HorizontalAlignmentOptions.Right);
            var pitch = lines.Count > 1 ? Mathf.Min(36f, (447f - 339f) / (lines.Count - 1)) : 36f;
            var y = 339f;
            foreach (var line in lines)
            {
                var income = Ledger.IsIncome(line);
                var amount = ledger.Of(line);
                Label(Medium, _content, LineName(line), 21.33f, _ink, 790f, y, 300f);
                Label(Bold, _content, Money(income ? amount : -amount, true, signed: true), 21.33f, income ? _goodText : _badText, 1244f, y, 200f, HorizontalAlignmentOptions.Right);
                y += pitch;
            }
            UiKit.RoundBox(_content, "Rule", Hex(0xd9c39a), 790f, 470f, 455f, 2f, 0f);
            Label(Bold, _content, Tr.Get("Net"), 22.67f, _ink, 790f, 517f, 300f);
            var net = ledger.Net;
            Label(ExtraBold, _content, Money(net, true, signed: true), 32.67f, net < 0f ? _badText : _goodText, 1244f, 517f, 300f, HorizontalAlignmentOptions.Right, -0.5f);
        }

        // In and out as two stacked bars on one scale, each part a ledger line.
        private void DrawMoneyWent(Ledger ledger, List<LedgerLine> lines)
        {
            UiKit.RoundBox(_content, "Went", _card, RightX, 585f, RightWidth, 196f, 18f);
            Caps(_content, "WHERE THE MONEY WENT", 790f, 625f);
            Label(Medium, _content, Tr.Format("Day {day}", ("day", Summary.Day)), 19.5f, _muted, 1772f, 625f, 200f, HorizontalAlignmentOptions.Right);

            var income = lines.Where(Ledger.IsIncome).ToList();
            var spending = lines.Where(l => !Ledger.IsIncome(l)).ToList();
            var totalIn = income.Sum(l => ledger.Of(l));
            var totalOut = spending.Sum(l => ledger.Of(l));
            var scale = 656f / Mathf.Max(1f, Mathf.Max(totalIn, totalOut));
            MoneyBar(Tr.Get("In"), income, ledger, scale, 648f, 670f, Money(totalIn), _goodText);
            MoneyBar(Tr.Get("Out"), spending, ledger, scale, 692f, 714f, Money(totalOut), _badText);

            var x = 790f;
            foreach (var line in lines)
            {
                UiKit.RoundBox(_content, "Key", LineColour(line), x, 740f, 16f, 16f, 3f);
                var label = Label(Medium, _content, LineName(line), 17.33f, _body, x + 24f, 754f, 200f);
                x += 24f + UiKit.Width(label) + 22f;
            }
            if (Summary.Day < 3)
                Label(Medium, _content, Tr.Get("Trend appears from day 3"), 19.5f, _muted, 1773f, 754f, 400f, HorizontalAlignmentOptions.Right);
        }

        private void MoneyBar(string title, List<LedgerLine> parts, Ledger ledger, float scale, float top, float baseline, string total, Color totalColour)
        {
            Label(Bold, _content, title, 20f, _ink, 790f, baseline, 80f);
            var x = 864f;
            foreach (var line in parts)
            {
                var width = Mathf.Max(4f, ledger.Of(line) * scale);
                UiKit.RoundBox(_content, line.ToString(), LineColour(line), x, top, width, 30f, 4f);
                x += width + 3f;
            }
            Label(Bold, _content, total, 22.67f, totalColour, 1773f, baseline + 1f, 200f, HorizontalAlignmentOptions.Right);
        }

        // From day 3: each day's net and rating; and the team's day, when there's a team.
        private void DrawTrendAndTeam()
        {
            var days = Services.Days;
            var team = Services.Staff.Hired.Select(m => Tr.Format("{work}, {mood}", ("work", ServiceStatusHud.WorkDone(m)), ("mood", ServiceStatusHud.Mood(m))))
                .Concat(Services.Staff.LastQuit.Select(m => Tr.Format("{work}, and quit", ("work", ServiceStatusHud.WorkDone(m))))).ToList();
            var close = days.LastClose;
            if (close.Spoiled > 0 || close.LeftOut > 0)
                team.Add(Tr.Format("Overnight: {spoiled} spoiled, {left} left out", ("spoiled", close.Spoiled), ("left", close.LeftOut)));
            var history = days.History.Skip(Mathf.Max(0, days.History.Count - 12)).ToList();
            var trend = Summary.Day >= 3 && history.Count > 0;
            if (!trend && team.Count == 0)
                return;

            UiKit.RoundBox(_content, "Trend", _card, RightX, 801f, RightWidth, 130f, 18f);
            if (trend)
            {
                Caps(_content, "HOW IT'S GOING", 790f, 832f);
                var biggest = Mathf.Max(1f, history.Max(d => Mathf.Abs(d.In - d.Out)));
                for (var i = 0; i < history.Count; i++)
                {
                    var day = history[i];
                    var net = day.In - day.Out;
                    var height = Mathf.Max(3f, 36f * Mathf.Abs(net) / biggest);
                    var x = 790f + i * 44f;
                    UiKit.RoundBox(_content, "Net", net >= 0f ? _good : _bad, x, net >= 0f ? 880f - height : 880f, 30f, height, 3f);
                    Label(Medium, _content, $"{day.Day}", 14f, _muted, x + 15f, 926f, 40f, HorizontalAlignmentOptions.Center);
                }
            }
            var teamX = trend ? 1340f : 790f;
            var ty = 838f;
            foreach (var line in team.Take(4))
            {
                Label(Medium, _content, line, 17.33f, _body, teamX, ty, RightEdge - 26f - teamX);
                ty += 26f;
            }
        }

        private Color LineColour(LedgerLine line) => line switch
        {
            LedgerLine.Payments => _good,
            LedgerLine.Tips => _goodLight,
            LedgerLine.Sales => Hex(0x5fb08f),
            LedgerLine.Ingredients => _ingredients,
            LedgerLine.WalkOuts => _bad,
            LedgerLine.Wages => Hex(0xb8a07a),
            LedgerLine.Energy => _gold,
            LedgerLine.Purchases => _body,
            LedgerLine.Fee => _ink,
            LedgerLine.Loan => Hex(0x6b4f3a),
            LedgerLine.Repossessed => Hex(0x8a7a66),
            _ => _quiet,
        };

        private static string LineName(LedgerLine line) => line switch
        {
            LedgerLine.Payments => Tr.Get("Meals"),
            LedgerLine.Tips => Tr.Get("Tips"),
            LedgerLine.Sales => Tr.Get("Sold"),
            LedgerLine.Ingredients => Tr.Get("Ingredients"),
            LedgerLine.WalkOuts => Tr.Get("Walk-outs"),
            LedgerLine.Wages => Tr.Get("Wages"),
            LedgerLine.Energy => Tr.Get("Energy"),
            LedgerLine.Purchases => Tr.Get("Bought"),
            LedgerLine.Fee => Tr.Get("The boss's cut"),
            LedgerLine.Loan => Tr.Get("Loan instalment"),
            LedgerLine.Repossessed => Tr.Get("Repossessed"),
            _ => line.ToString(),
        };

        // ---- The footer is the start bar (the user's call, 2026-11-11) ----
        // The whole footer is one button: hold Y to start the next day. Its bar fills as Y is held,
        // split into one segment per joined cook, each filling in their colour (a single bar for one
        // cook). The A and "Hold to start day N" sit on it; with two or more cooks, who's still to
        // hold follows. No control hints: the tab bar has its own LB/RB, and lists browse with the
        // D-pad as every menu does. Each segment eases after its hold, and a white flash pops as a
        // hold completes; the A pulses at rest and sinks while held.

        // The footer strip: flush with the panel's bottom edge and sides (the user's design, 2026-11-11).
        private const float BarX = PanelX, BarTop = FooterTop + 10f, BarWidth = PanelWidth, BarHeight = PanelY + PanelHeight - FooterTop - 10f, SegmentGap = 4f;
        // The start card in the left column, under the four facts.
        // A round-ended pill, standing on its own centred in the gap between the cards (they end at
        // 808) and the footer strip (the user's mock, 2026-11-11).
        private const float StartCardHeight = 84f;
        private const float StartCardTop = 808f + (BarTop - 808f - StartCardHeight) / 2f;

        [Header("Start bar")]
        [Tooltip("How quickly each segment catches up with its player's hold, per second (higher is snappier).")]
        [SerializeField, Min(0.1f)] private float _startFillSpeed = 12f;
        [Tooltip("Seconds a player's label pops for as they become ready.")]
        [SerializeField, Min(0.05f)] private float _readyPopSeconds = 0.45f;
        [Tooltip("How much a player's label grows at the top of its ready pop.")]
        [SerializeField, Range(0f, 0.5f)] private float _readyPop = 0.18f;
        [Tooltip("Seconds the white flash takes to fade after a player's hold completes.")]
        [SerializeField, Min(0.01f)] private float _startFlashSeconds = 0.45f;
        [Tooltip("Pulses a second of the start button while nobody's holding.")]
        [SerializeField, Min(0.01f)] private float _buttonPulsePerSecond = 0.7f;
        [Tooltip("How much the start button grows at the top of a pulse, and how far it sinks while held.")]
        [SerializeField, Range(0f, 0.1f)] private float _buttonPulse = 0.02f;
        [SerializeField, Range(0.8f, 1f)] private float _buttonPressed = 0.96f;

        private string _hintsShown;
        private RectTransform _bar;
        private RectTransform _lanes;
        private TextMeshProUGUI _startText;
        private RectTransform _startRing;
        private ReadyLanes _readyLanes;
        private RectTransform _startA;
        private Image _startFlash;
        private int _startReady;
        private float _startFlashLeft;

        /// <summary>The cooks forkcast counts: everyone joined whose controller is here, as the day loop counts them.</summary>
        private static List<PlayerInput> JoinedPlayers() =>
            PlayerInput.all.Where(p => !PlayerPresence.IsLost(p)).OrderBy(p => p.playerIndex).ToList();

        /// <summary>How many segments the start bar shows, for tests.</summary>
        public int StartSegments => _readyLanes != null ? _readyLanes.Count : 0;

        // Called every frame, but only rebuilt when who's here or ready changes.
        private void DrawHints()
        {
            var joined = JoinedPlayers();
            var state = $"{StartsDay}|{PlayerInput.all.Count}|" + string.Join(",", joined.Select(p => $"{p.playerIndex}:{_dayLoop.Ready.IsReady(p.playerIndex)}"));
            if (state == _hintsShown)
                return;
            _hintsShown = state;
            BuildLanes(joined);

            // The start card: what holding Y does. Nobody's here yet: A joins first.
            _startText.text = PlayerInput.all.Count == 0 ? Tr.Get("Press to join") : Tr.Format("Hold to start Day {day}", ("day", StartsDay));
            // The ring and the words centred together on the button.
            var textWidth = UiKit.Width(_startText);
            var left = (600f - (52f + 16f + textWidth)) / 2f;
            _startRing.anchoredPosition = new Vector2(left, _startRing.anchoredPosition.y);
            _startText.rectTransform.anchoredPosition = new Vector2(left + 52f + 16f, _startText.rectTransform.anchoredPosition.y);

            // Each segment says whose it is and whether they're in; a ready one shows B to take it back
            // (centred in the segment, the user's mock, 2026-11-11).
            _readyLanes.Label(p => _dayLoop.Ready.IsReady(p), (player, ready) =>
            {
                var name = player >= 0 ? player + 1 : 1;
                return ready
                    ? (Tr.Format("Player {player} READY", ("player", name)), new ReadyLanes.Glyph("B", _buttonB))
                    : (Tr.Format("Waiting for Player {player}", ("player", name)), (ReadyLanes.Glyph?)null);
            });
        }

        private void BuildStartButton()
        {
            // The footer strip, clipped to the panel's rounded bottom: the mask's top corners sit
            // above the strip, out of sight, so only its bottom corners round off.
            var mask = UiKit.RoundBox(_layout, "StartMask", Color.white, BarX, BarTop - PanelRadius, BarWidth, BarHeight + PanelRadius, PanelRadius);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            _bar = UiKit.Rect(mask.transform, "StartBar", 0f, PanelRadius, BarWidth, BarHeight);
            _lanes = UiKit.Rect(_bar, "Lanes", 0f, 0f, BarWidth, BarHeight);
            _readyLanes = new ReadyLanes(_lanes, BarWidth, BarHeight, new ReadyLanes.Look
            {
                ReadyFont = ExtraBold,
                WaitingFont = SemiBold,
                BadgeFont = Bold,
                FontSize = 21f,
                Light = _cardLight,
                Ink = _ink,
                Nobody = _gold,
                Gap = SegmentGap,
                FillSpeed = _startFillSpeed,
                PopSeconds = _readyPopSeconds,
                Pop = _readyPop,
            });
            _startFlash = UiKit.RoundBox(_bar, "Flash", new Color(1f, 1f, 1f, 0f), 0f, 0f, BarWidth, BarHeight, 0f);

            // The start button in the left column (the user's mock, 2026-11-11): one button in the
            // Xbox Y's yellow (Y, not A, since 2026-11-11), a white edge, the Y as a white ring and
            // "Hold to start day N" in ink, centred. It animates as a unit: a gentle pulse at rest, sinking while held.
            _startA = UiKit.Rect(_layout, "StartButton", 120f, StartCardTop, 600f, StartCardHeight);
            _startA.pivot = new Vector2(0.5f, 0.5f);
            _startA.anchoredPosition += new Vector2(300f, -StartCardHeight / 2f);
            var face = UiKit.StrokedBox(_startA, "Button", _buttonY, Color.white, 4f, 0f, 0f, 600f, StartCardHeight, StartCardHeight / 2f);
            // A darker lower band gives it depth, like a key (flat, no gradient), clipped to the pill.
            face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            UiKit.RoundBox(face.transform, "Shade", Color.Lerp(_buttonY, _ink, 0.12f), 0f, StartCardHeight * 0.6f, 600f, StartCardHeight * 0.4f, 0f);
            _startRing = UiKit.Rect(_startA, "Ring", 0f, (StartCardHeight - 52f) / 2f, 52f, 52f);
            // The Y exactly as forkcast's other button glyphs (the footer's B), only bigger: the kit's
            // badge, its white ring scaled with it (the user's call, 2026-11-11).
            Badge(_startRing, "Y", _buttonY, 26f, 26f, 52f, 26f);
            _startText = UiKit.TextAt(ExtraBold, _startA, "Text", "", 38f, _ink, 0f, StartCardHeight / 2f + 13f, 520f);
        }

        // One segment per joined cook, in player-slot order; rebuilt only when who's here changes.
        private void BuildLanes(List<PlayerInput> joined) =>
            _readyLanes.Build(joined.Select(p => p.playerIndex).ToList(), _settings.GetPlayerColor);

        // Every frame while forkcast is up: each segment eases to its player's hold, the A pulses or sinks.
        private void UpdateStart(float dt)
        {
            if (_bar == null)
                return;
            // With one segment, anyone's hold fills it (a lone cook, or nobody joined yet).
            var single = _readyLanes.Count == 1;
            var (anyHold, ready) = _readyLanes.Tick(dt, p => _dayLoop.Ready.IsReady(p),
                p => p < 0 || single ? Mathf.Max(p >= 0 ? _start.Progress(p) : 0f, _start.MostProgress) : _start.Progress(p));

            if (ready > _startReady)
                _startFlashLeft = _startFlashSeconds;
            _startReady = ready;
            _startFlashLeft = Mathf.Max(0f, _startFlashLeft - dt);
            _startFlash.color = new Color(1f, 1f, 1f, 0.6f * (_startFlashSeconds > 0f ? _startFlashLeft / _startFlashSeconds : 0f));

            var scale = anyHold ? _buttonPressed : 1f + _buttonPulse * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * _buttonPulsePerSecond);
            _startA.localScale = new Vector3(scale, scale, 1f);
        }

        // A secondary action as a pill: its button and what it does. Right edge at edge (or left edge
        // there, fromLeft); returns its left edge.
        private float Chip(Transform parent, string key, string text, float edge, float top = 965f, bool fromLeft = false)
        {
            const float height = 46f;
            var label = Label(SemiBold, parent, text, 20f, _ink, 0f, top + 30f, 260f);
            var keyWidth = key == "View" ? 44f : 30f;
            var width = 14f + keyWidth + 10f + UiKit.Width(label) + 18f;
            var x = fromLeft ? edge : edge - width;
            var box = UiKit.StrokedBox(parent, "Chip", _cardLight, _cardEdge, 2f, x, top, width, height, height / 2f);
            box.transform.parent.GetChild(box.transform.GetSiblingIndex() - 1).SetSiblingIndex(label.transform.GetSiblingIndex());
            box.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
            ChipKey(parent, key, x + 14f, top + (height - 30f) / 2f, keyWidth);
            label.rectTransform.anchoredPosition = new Vector2(x + 14f + keyWidth + 10f, label.rectTransform.anchoredPosition.y);
            return x;
        }

        // A button inside a chip: a face button as its badge, View or the D-pad as a key cap.
        private void ChipKey(Transform parent, string key, float x, float y, float width)
        {
            switch (key)
            {
                case "A": Badge(parent, "A", _buttonA, x + 15f, y + 15f, 30f, 15f); break;
                case "B": Badge(parent, "B", _buttonB, x + 15f, y + 15f, 30f, 15f); break;
                case "X": Badge(parent, "X", _buttonX, x + 15f, y + 15f, 30f, 15f); break;
                case "Y": YBadge(parent, x + 15f, y + 15f, 30f); break;
                default: Keycap(parent, key, x, y, width, 30f); break;
            }
        }

        // ---- Pieces ----

        // A dark key cap with its name; ≡ is drawn as three bars (the fonts have no such glyph).
        private void Keycap(Transform parent, string key, float x, float y, float width, float height, bool outlined = false)
        {
            if (outlined)
                UiKit.StrokedBox(parent, key, _ink, Color.white, 4f, x, y, width, height, 8f);
            else
                UiKit.RoundBox(parent, key, _ink, x, y, width, height, 8f);
            if (key == "DPad")
            {
                // Lists browse with the D-pad, not the stick (2026-11-10): drawn, as no font has it.
                UiKit.DPad(parent, _paper, x + width / 2f, y + height / 2f, 20f);
                return;
            }
            if (key == "View")
            {
                // The View button's two overlapping windows, drawn (no font has it).
                UiKit.StrokedBox(parent, "Back", _ink, _paper, 2f, x + width / 2f - 9f, y + height / 2f - 8f, 13f, 11f, 2f);
                UiKit.StrokedBox(parent, "Front", _ink, _paper, 2f, x + width / 2f - 4f, y + height / 2f - 3f, 13f, 11f, 2f);
                return;
            }
            if (key == "≡")
            {
                for (var i = 0; i < 3; i++)
                    UiKit.RoundBox(parent, "Bar", _paper, x + width / 2f - 7f, y + height / 2f - 5f + i * 4f, 14f, 2f, 1f);
                return;
            }
            Label(Bold, parent, key, 15.5f, _paper, x + width / 2f, y + height / 2f + 5.5f, width, HorizontalAlignmentOptions.Center);
        }

        // The Y button as the mockups draw it: yellow, a white ring and a thin ink edge.
        private void YBadge(Transform parent, float cx, float cy, float diameter)
        {
            UiKit.Ellipse(parent, "YEdge", _ink, cx - diameter / 2f - 2.67f, cy - diameter / 2f - 2.67f, diameter + 5.33f, diameter + 5.33f);
            UiKit.StrokedCircle(parent, "Y", _buttonY, Color.white, 4f, cx - diameter / 2f, cy - diameter / 2f, diameter);
            Label(Bold, parent, "Y", 15.5f, _ink, cx, cy + 5.5f, diameter, HorizontalAlignmentOptions.Center);
        }

        private void Badge(Transform parent, string letter, Color colour, float cx, float cy, float diameter, float size)
        {
            // The ring is the kit's 4 px at its 31 px size, and keeps that proportion at any size, so
            // a big A and a small B read as the same glyph.
            UiKit.StrokedCircle(parent, letter, colour, Color.white, Mathf.Max(3f, diameter * 4f / 31f), cx - diameter / 2f, cy - diameter / 2f, diameter);
            Label(Bold, parent, letter, size, letter == "Y" ? _ink : Color.white, cx, cy + size * 0.37f, diameter, HorizontalAlignmentOptions.Center);
        }

        // A pill at (x, y); returns its right edge, or −1 if it would pass limit (and isn't drawn).
        private float Pill(Transform parent, string text, TMP_FontAsset font, float size, Color back, Color fore, float x, float y, float height, float pad, float baselineOffset, float limit = float.MaxValue)
        {
            var label = Label(font, parent, text, size, fore, x + pad, y + baselineOffset, 600f);
            var width = UiKit.Width(label) + pad * 2f;
            if (x + width > limit)
            {
                DestroyImmediate(label.gameObject);
                return -1f;
            }
            var box = UiKit.RoundBox(parent, text + "Pill", back, x, y, width, height, height / 2f);
            box.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
            return x + width;
        }

        // A pill whose right edge is at right; returns its left edge.
        private float RightPill(Transform parent, string text, TMP_FontAsset font, float size, Color back, Color fore, float right, float y, float height, float pad)
        {
            var label = Label(font, parent, text, size, fore, 0f, y + height / 2f + size * 0.36f, 600f);
            var width = UiKit.Width(label) + pad * 2f;
            var box = UiKit.RoundBox(parent, text + "Pill", back, right - width, y, width, height, height / 2f);
            box.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
            label.rectTransform.anchoredPosition = new Vector2(right - width + pad, label.rectTransform.anchoredPosition.y);
            return right - width;
        }

        // Five outlined stars from the mockup's art, in halves. Returns the right edge.
        private float Stars(Transform parent, float x, float y, float size, float pitch, float stars)
        {
            var halves = Mathf.Clamp(Mathf.RoundToInt(stars * 2f), 0, 10);
            var height = size * 26f / 27f;
            for (var i = 0; i < 5; i++)
            {
                var sprite = halves >= (i + 1) * 2 ? _style.StarFull : halves == i * 2 + 1 ? _style.StarHalf : _style.StarEmpty;
                if (sprite != null)
                    UiKit.Picture(parent, "Star", sprite, x + i * pitch - 1f, y - 1f, size + 2f, height + 2f);
                else
                    UiKit.Picture(parent, "Star", StarSprite, x + i * pitch, y, size, size, halves >= (i + 1) * 2 - 1 ? _gold : _rule);
            }
            return x + 4f * pitch + size;
        }

        // ---- Building the fixed parts ----

        private void Build()
        {
            _canvasObject = new GameObject("ReviewDashboard", typeof(RectTransform));
            _canvasObject.transform.SetParent(transform, false);
            var canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // It's nested under the kitchen's HUD canvas: without its own sorting it drew in the HUD's
            // order, under whatever the HUD made after it (a cutting dial, the guests' edge markers),
            // so those showed over the dashboard as the day ended (2026-11-11 playtest round).
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20;
            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Expand: the whole 1920×1080 layout always fits, 16:10 screens included.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var canvasRect = _canvasObject.GetComponent<RectTransform>();

            // The kitchen as the doors closed, blurred and dimmed, fills the screen behind the app.
            _backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _backdrop.transform.SetParent(canvasRect, false);
            UiKit.Fill(_backdrop.rectTransform);
            _backdrop.raycastTarget = false;
            _backdrop.enabled = false;
            var tint = UiKit.RoundBox(canvasRect, "Tint", _backdropTint, 0f, 0f, 0f, 0f, 0f);
            UiKit.Fill(tint.rectTransform);

            _layout = UiKit.Rect(canvasRect, "Layout", 0f, 0f, 1920f, 1080f);
            _layout.anchorMin = _layout.anchorMax = _layout.pivot = new Vector2(0.5f, 0.5f);
            _layout.anchoredPosition = Vector2.zero;

            UiKit.RoundBox(_layout, "Panel", _paper, PanelX, PanelY, PanelWidth, PanelHeight, PanelRadius);
            UiKit.RoundBox(_layout, "Header", _ink, PanelX, PanelY, PanelWidth, HeaderBottom - PanelY, PanelRadius);
            UiKit.RoundBox(_layout, "HeaderFoot", _ink, PanelX, HeaderBottom - 30f, PanelWidth, 30f, 0f);
            UiKit.Picture(_layout, "Logo", _style.ForkcastLogo, 114f, 74f, 188f, 46f);
            UiKit.RoundBox(_layout, "Search", _well, 318f, 74f, 581f, 48f, 24f);
            UiKit.Picture(_layout, "SearchIcon", _style.SearchIcon, 340f, 87f, 22f, 22f);
            UiKit.RoundBox(_layout, "Footer", _card, PanelX, FooterTop, PanelWidth, PanelY + PanelHeight - FooterTop, PanelRadius);
            UiKit.RoundBox(_layout, "FooterTop", _card, PanelX, FooterTop, PanelWidth, 30f, 0f);

            // The photo, clipped to its rounded corners, with its caption.
            var frame = UiKit.RoundBox(_layout, "Photo", _card, 120f, 172f, 600f, 338f, 18f);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _photo = UiKit.Rect(frame.transform, "Image", 0f, 0f, 600f, 338f).gameObject.AddComponent<RawImage>();
            _photo.raycastTarget = false;
            // The close-time photo is wider than the frame: crop its sides.
            var aspect = 600f / 338f / ((float)_photoSize.x / _photoSize.y);
            _photo.uvRect = new UnityEngine.Rect((1f - aspect) / 2f, 0f, aspect, 1f);
            UiKit.RoundBox(_layout, "Caption", Alpha(_ink, 0.85f), 438f, 466f, 270f, 32f, 8f);
            _caption = UiKit.TextAt(_style.Medium, _layout, "CaptionText", Tr.Get("Your photo · captured at close"), 17.33f, _paper, 573f, 488f, 270f, HorizontalAlignmentOptions.Center);

            _header = UiKit.Rect(_layout, "HeaderState", 0f, 0f, 1920f, 1080f);
            _money = UiKit.Rect(_layout, "Money", 1804f - _moneyWidth, MoneyTop, _moneyWidth, MoneyHeight);
            DrawMoneyChip();
            _left = UiKit.Rect(_layout, "Left", 0f, 0f, 1920f, 1080f);
            _tabs = UiKit.Rect(_layout, "Tabs", 0f, 0f, 1920f, 1080f);
            _content = UiKit.Rect(_layout, "Content", 0f, 0f, 1920f, 1080f);
            // The review list scrolls inside its window, so cards are clipped at its bottom edge.
            var window = UiKit.Rect(_layout, "ListWindow", RightX - 8f, ListTop - 8f, RightWidth + 16f, ListBottom - ListTop + 8f);
            window.gameObject.AddComponent<RectMask2D>();
            _list = UiKit.Rect(window, "List", -(RightX - 8f), -(ListTop - 8f), 1920f, 1080f);
            _hints = UiKit.Rect(_layout, "Hints", 0f, 0f, 1920f, 1080f);
            BuildStartButton();
            _popover = UiKit.Rect(_layout, "Popover", 0f, 0f, 1920f, 1080f);

            // Never shown: measures how many lines a reply wraps to, so its card can make room.
            _measure = UiKit.TextAt(Medium, _layout, "Measure", "", ReplySize, Color.clear, -4000f, -4000f, ContentWidth, lines: 4);
        }
    }
}
