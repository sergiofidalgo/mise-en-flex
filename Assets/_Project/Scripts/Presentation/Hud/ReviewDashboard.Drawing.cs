using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Days;
using Game.Simulation.Saving;
using Game.Simulation.Service;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Data;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// Drawing the Review dashboard. Positions, sizes and colours were measured from the Art Director's
    /// dashboard SVGs (1920×1080: overview, reviews, reply wheel, reply thread, menu, books); text sits
    /// on the mockup's baselines and sizes are its points × 4/3, checked against captures.
    /// </summary>
    public sealed partial class ReviewDashboard
    {
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
        private Sprite _hatch;

        private TMP_FontAsset Medium => _style.Medium;
        private TMP_FontAsset SemiBold => _style.SemiBold;
        private TMP_FontAsset Bold => _style.Bold;
        private TMP_FontAsset ExtraBold => _style.ExtraBold;
        private TMP_FontAsset Serif => _style.SerifItalic;

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

        private void Refresh()
        {
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
                case Tab.MenuAndBooks:
                    DrawMenuAndBooks();
                    break;
                case Tab.Cookbook:
                    DrawCookbook();
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
            var config = Services.ServiceConfig;
            Label(Medium, _header, Tr.Format("{style}s near you", ("style", Tr.Get(config.StyleName))), 21.33f, _sand, 374f, 106f, 500f);

            var today = Services.ReviewHistory.Count(r => r.Day == days.Day);
            var right = 1804f;
            if (today > 0)
                right = HeaderPill(today == 1 ? "1 new review" : $"{today} new reviews", _wellLight, right) - 22f;
            HeaderPill(Tr.Format("Day {day} · {weekday}", ("day", days.Day), ("weekday", Weekday(days.Day))), _well, right);
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

        // ---- Left: name, rating, and tonight's guests ----

        private void DrawLeft()
        {
            UiKit.Clear(_left);
            var config = Services.ServiceConfig;
            var all = Services.ReviewHistory;
            Label(ExtraBold, _left, config.RestaurantName, 50.67f, _ink, 120f, 576f, 600f, tracking: -1.6f);

            var rating = ReviewReplies.Rating(all);
            if (rating.HasValue)
            {
                var score = Label(Bold, _left, $"{rating.Value:0.0}", 40f, _ink, 120f, 635f, 120f);
                var starsEnd = Stars(_left, 120f + UiKit.Width(score) + 17f, 607f, 27f, 37f, rating.Value);
                Label(Medium, _left, $"({all.Count})", 22.67f, _body, starsEnd + 16f, 629f, 120f);
            }
            else
            {
                Label(Medium, _left, Tr.Get("No reviews yet"), 26.67f, _muted, 120f, 632f);
            }

            var hours = Services.Service.Settings.DurationSeconds;
            // The price band follows the menu's dearest main dish, as listings do.
            var mainPrice = Services.Cookbook != null && Services.Cookbook.Menu.Any()
                ? Services.Cookbook.Menu.Max(r => Services.Cookbook.PriceOf(r.Id))
                : config.MainPrice;
            var band = mainPrice < 15f ? 1 : mainPrice < 30f ? 2 : 3;
            Label(Medium, _left, Tr.Format("{band} · {style} · Open {from}–{to}", ("band", Locale.PriceBand(band)), ("style", Tr.Get(config.StyleName)), ("from", ClockAt(0f)), ("to", ClockAt(hours))), 21.33f, _body, 120f, 673f, 600f);

            DrawChart();
        }

        // Tonight's guests by half hour: served in green, walk-outs hatched on top.
        private void DrawChart()
        {
            Caps(_left, "TONIGHT'S GUESTS", 146f, 738f);
            var legend = 694f;
            legend = LegendItem(_left, Tr.Get("Walked out"), _bad, legend, 738f, 17.33f);
            LegendItem(_left, Tr.Get("Served"), _good, legend - 16f, 738f, 17.33f);

            var today = TonightsVisits();
            var duration = Services.Service.Settings.DurationSeconds;
            const float bucket = 30f * 60f;
            var buckets = Mathf.Max(1, Mathf.CeilToInt(duration / bucket));
            var served = new int[buckets];
            var left = new int[buckets];
            foreach (var visit in today)
            {
                var b = Mathf.Clamp((int)(visit.ArrivedAt / bucket), 0, buckets - 1);
                if (visit.WalkedOut)
                    left[b]++;
                else
                    served[b]++;
            }

            const float baseline = 862f;
            const float area = 92f;
            UiKit.RoundBox(_left, "Axis", Hex(0xc7b28c), 146f, 863f, 548f, 2f, 0f);
            var most = Mathf.Max(1, Enumerable.Range(0, buckets).Max(i => served[i] + left[i]));
            var unit = Mathf.Min(31f, (area - 4f) / most);
            // Bars centred from 18:00 at x 220 to the last half hour at x 582, as in the mockup.
            var step = buckets > 1 ? (582f - 220f) / (buckets - 1) : 0f;
            var width = Mathf.Min(132f, buckets > 1 ? step * 0.8f : 132f);
            var rush = Enumerable.Range(0, buckets).OrderByDescending(i => left[i]).First();
            for (var i = 0; i < buckets; i++)
            {
                var centre = 220f + i * step;
                var x = centre - width / 2f;
                var servedHeight = served[i] * unit;
                var leftHeight = left[i] * unit;
                if (servedHeight > 0f)
                    UiKit.RoundBox(_left, "Served", _good, x, baseline - servedHeight, width, servedHeight, 6f);
                if (leftHeight > 0f)
                    Hatched(_left, x, baseline - servedHeight - (servedHeight > 0f ? 4f : 0f) - leftHeight, width, leftHeight);
                if (served[i] + left[i] == 0)
                    Label(Medium, _left, "0", 16f, _quiet, centre, 858f, 60f, HorizontalAlignmentOptions.Center);
                if (i == 0 || i == buckets - 1 || buckets <= 4 || i % 2 == 0)
                    Label(Medium, _left, ClockAt(i * bucket), 16f, _muted, centre, 890f, 100f, HorizontalAlignmentOptions.Center);
                if (i == rush && left[i] > 0)
                {
                    var top = baseline - servedHeight - (servedHeight > 0f ? 4f : 0f) - leftHeight;
                    Label(Bold, _left, Tr.Format("Rush · {count} left", ("count", left[i])), 16.67f, _badText, centre, top - 8f, 200f, HorizontalAlignmentOptions.Center);
                }
            }
        }

        // A legend entry whose label ends at right: returns where the entry starts.
        private float LegendItem(Transform parent, string text, Color colour, float right, float baseline, float size)
        {
            var label = Label(Medium, parent, text, size, _body, 0f, baseline, 200f);
            var width = UiKit.Width(label);
            label.rectTransform.anchoredPosition = new Vector2(right - width, label.rectTransform.anchoredPosition.y);
            UiKit.RoundBox(parent, text + "Key", colour, right - width - 24f, baseline - 14f, 16f, 16f, 3f);
            return right - width - 24f;
        }

        // Walk-outs: orange diagonal stripes, clipped to a rounded bar.
        private void Hatched(Transform parent, float x, float y, float width, float height)
        {
            var bar = UiKit.RoundBox(parent, "Walked", _bad, x, y, width, height, 6f);
            bar.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var stripes = UiKit.Rect(bar.transform, "Stripes", 0f, 0f, width, height).gameObject.AddComponent<Image>();
            stripes.sprite = HatchSprite;
            stripes.type = Image.Type.Tiled;
            stripes.color = Hex(0xf08a4b);
            stripes.raycastTarget = false;
        }

        private Sprite HatchSprite
        {
            get
            {
                if (_hatch != null)
                    return _hatch;
                // 16 px tile of 45° stripes, 6 px wide: tiles seamlessly because the period divides the tile.
                const int size = 32;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave };
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = (x + y) % 16;
                    var coverage = Mathf.Clamp01(Mathf.Min(d - 0.5f, 8.5f - d) * 0.9f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, coverage));
                }
                texture.Apply();
                _hatch = Sprite.Create(texture, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 200f, 0, SpriteMeshType.FullRect);
                return _hatch;
            }
        }

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

            var summary = Services.Days.LastService;
            var x = RightX + BossWidth + 16f;
            var width = (RightEdge - x - 2f * 12f) / 3f;
            SmallStat(x, width, $"{summary?.Served ?? 0}", Tr.Get("Served"), false);
            SmallStat(x + width + 12f, width, $"{summary?.WalkOuts ?? 0}", Tr.Get("Walked out"), (summary?.WalkOuts ?? 0) > 0);
            SmallStat(x + 2f * (width + 12f), width, $"{summary?.TurnedAway ?? 0}", Tr.Get("Turned away"), (summary?.TurnedAway ?? 0) > 0);

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

            // Scroll so the chosen card is whole: start from the top and drop cards until it fits.
            var heights = reviews.Select(r => CardHeight(r, false)).ToList();
            var first = 0;
            while (first < _selected && ListTop + Enumerable.Range(first, _selected - first + 1).Sum(i => heights[i] + 14f) - 14f > ListBottom)
                first++;
            var y = ListTop;
            for (var i = first; i < reviews.Count && y < ListBottom; i++)
                y = ReviewCard(_list, reviews[i], y, i == _selected && !_onFilters, false) + 14f;
        }

        // The boss's card, the Overview's headline (the user's call, 2026-11-10): what's owed to him in
        // big letters when the money's below zero, otherwise the balance; then the next fee, the days
        // left as segments, and whether the money will be there.
        private void DrawBoss(float x, float y)
        {
            var days = Services.Days;
            UiKit.RoundBox(_content, "Boss", _ink, x, y, BossWidth, TopRowHeight, 18f);
            Caps(_content, "THE BOSS", x + 26f, y + 34f, _gold);
            if (days.OwedToBoss > 0f)
                Label(ExtraBold, _content, Tr.Format("You owe him {amount}", ("amount", Money(days.OwedToBoss, true))), 44f, _shortBy, x + 26f, y + 90f, BossWidth - 52f, tracking: -1.2f);
            else
                Label(ExtraBold, _content, Tr.Format("Balance {amount}", ("amount", Money(days.Balance, true))), 44f, _paper, x + 26f, y + 90f, BossWidth - 52f, tracking: -1.2f);

            // The fee's days as segments, top right.
            var segments = Mathf.Max(1, days.Fee.EveryDays);
            var done = days.FeeCollectedToday ? segments : segments - days.DaysUntilFee;
            const float barsWidth = 230f;
            var segment = (barsWidth - (segments - 1) * 6f) / segments;
            for (var i = 0; i < segments; i++)
                UiKit.RoundBox(_content, "Day", i < done ? _gold : _wellLight, x + BossWidth - 26f - barsWidth + i * (segment + 6f), y + 24f, segment, 10f, 5f);

            string fee;
            if (days.FeeCollectedToday)
                fee = Tr.Format("Fee of {amount} collected today", ("amount", Money(days.Fee.Amount)));
            else
            {
                var left = Tr.Plural(days.DaysUntilFee, "1 day", "{n} days");
                fee = Tr.Format("Next fee {amount} in {days}", ("amount", Money(days.Fee.Amount)), ("days", HudStyle.Colored(left, _gold)));
            }
            Label(Bold, _content, fee, 21.33f, _sand, x + 26f, y + 128f, 360f);
            if (days.OwedToBoss <= 0f && !days.FeeCollectedToday && days.Balance < days.Fee.Amount)
                Label(Bold, _content, Tr.Format("Short by {amount}", ("amount", Money(days.Fee.Amount - days.Balance, true))), 19.5f, _shortBy, x + BossWidth - 26f, y + 128f, 300f, HorizontalAlignmentOptions.Right);
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
            var active = filter == _filter;
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
            if (active && _onFilters)
            {
                var ring = UiKit.RoundBox(_content, "Focus", _gold, x - 4f, y - 4f, width + 8f, 47f, 23.5f);
                ring.transform.SetSiblingIndex(index);
            }
            return x + width;
        }
        // A reply adds the owner's line under the review (one reply per review), or two when it's long.
        private float CardHeight(ReviewRecord review, bool overview) =>
            (overview ? 118f : 110f) + (review.Reply >= 0 ? 68f + (ReplyLines(review) > 1 ? ReplyLineHeight : 0f) : 0f);

        private const float ReplyWidth = 860f, ReplySize = 21.33f, ReplyLineHeight = 27f;
        private TextMeshProUGUI _measure;

        // How many lines a reply wraps to, measured with the reply's own font, size and width.
        private int ReplyLines(ReviewRecord review)
        {
            var text = ShownReply(review);
            if (_measure == null || string.IsNullOrEmpty(text))
                return 1;
            _measure.font = Medium;
            _measure.fontSize = ReplySize;
            var height = _measure.GetPreferredValues(text, ReplyWidth, 0f).y;
            var face = Medium.faceInfo;
            var line = face.lineHeight * ReplySize / face.pointSize;
            return height > line * 1.5f ? 2 : 1;
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

        // One review card at y; returns its bottom.
        private float ReviewCard(Transform parent, ReviewRecord review, float y, bool selected, bool overview)
        {
            var height = CardHeight(review, overview) + (selected && overview && review.Reply < 0 ? 4f : 0f);
            if (selected)
                UiKit.RoundBox(parent, "Highlight", _gold, CardLeft - 5.67f, y - 5.67f, CardWidth + 11.33f, height + 11.33f, 20f);
            UiKit.StrokedBox(parent, "Review", _cardLight, selected ? _gold : _cardEdge, 2.67f, CardLeft, y, CardWidth, height, 15f);

            UiKit.StrokedEllipse(parent, "Avatar", UiKit.AvatarColour(review.Guest), _ink, 4f, CardLeft + 24f, y + (overview ? 21f : 16f), 49f, 61f);
            var nameBase = y + (overview ? 38f : 34f);
            // In the player's language: the guest's name slot looked up there (decision 2026-11-09).
            var name = Label(Bold, parent, Game.Data.GuestNames.Display(review), 20f, _ink, 857f, nameBase, 300f);
            var meta = (string.IsNullOrEmpty(review.Order) ? "" : $"· {Game.Data.ReviewText.Order(review.Order)} ") + $"· {ClockAt(review.ArrivedAt)}";
            if (review.Day != Services.Days.Day)
                meta += Tr.Format(" · day {day}", ("day", review.Day));
            // A guest who's posted before (decision 2026-11-09).
            if (review.Visit == 2)
                meta += Tr.Get(" · back again");
            else if (review.Visit > 2)
                meta += Tr.Format(" · visit {visit}", ("visit", review.Visit));
            Label(Medium, parent, meta, 19f, _muted, 857f + UiKit.Width(name) + 12f, nameBase, 420f);
            // Written in the player's language from its templates and facts (decision 2026-11-09).
            var quote = Label(Serif, parent, $"“{Game.Data.ReviewText.Line(review)}”", overview ? 30.33f : 26f, _ink, 857f, y + (overview ? 77f : 69f), 640f);
            quote.textWrappingMode = TextWrappingModes.NoWrap;

            // Right: the rating, then what it paid or cost, and Reply while it's unanswered.
            var pillTop = y + (overview ? 19f : 15f);
            if (review.WalkedOut)
            {
                UiKit.RoundBox(parent, "Rating", _alarm, 1547f, pillTop, 229f, 42f, 21f);
                UiKit.RoundBox(parent, "Dash", _badText, 1564f, pillTop + 19f, 24f, 4f, 2f);
                Stars(parent, 1605f, pillTop + 9f, 24f, 33f, 0f);
            }
            else
            {
                UiKit.RoundBox(parent, "Rating", _paper, 1546f, pillTop, 230f, 42f, 21f);
                Label(ExtraBold, parent, $"{review.RatedStars:0.0}", 24f, _ink, 1555f, pillTop + 30f, 50f);
                Stars(parent, 1605f, pillTop + 9f, 24f, 33f, review.RatedStars);
            }

            var chipBase = y + (overview ? 92f : 88f);
            var chipText = review.WalkedOut
                ? Tr.Format("Walked out · {amount}", ("amount", Money(-review.Penalty)))
                : review.Tip > 0f ? Tr.Format("{paid} + {tip} tip", ("paid", Money(review.Paid)), ("tip", Money(review.Tip))) : Tr.Format("{paid} · no tip", ("paid", Money(review.Paid)));
            var chipLeft = RightPill(parent, chipText, review.WalkedOut ? Bold : SemiBold, 16.67f, review.WalkedOut ? _alarm : _card, review.WalkedOut ? _alarmText : _wellLight, 1776f, chipBase - 21f, 30f, 12f);
            if (selected && review.Reply < 0)
                ReplyButton(parent, chipLeft - 8f, chipBase - 21f + 15f);

            if (review.Reply >= 0)
            {
                var top = y + 107f;
                UiKit.RoundBox(parent, "Thread", _gold, 856f, top, 3f, 54f + (ReplyLines(review) > 1 ? ReplyLineHeight : 0f), 0f);
                UiKit.Picture(parent, "Mark", _style.ReplyMark, 876f, top + 4f, 14f, 18f);
                var owner = Label(Bold, parent, Services.ServiceConfig.RestaurantName, 16.67f, _wellLight, 897f, top + 18f, 300f);
                Label(Medium, parent, review.Day == Services.Days.Day ? Tr.Get("· owner · just now") : Tr.Get("· owner"), 16.67f, _quiet, 897f + UiKit.Width(owner) + 7f, top + 18f, 300f);
                var reply = UiKit.TextAt(Medium, parent, "Reply", ShownReply(review), ReplySize, _ink, 877f, top + 45f, ReplyWidth, lines: 2);
                LineHeight(reply, ReplyLineHeight);
            }
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

        // ---- Menu & Books: today's money beside tonight's menu, then where it went ----

        private void DrawMenuAndBooks()
        {
            var ledger = Services.Days.Today;
            var lines = ((LedgerLine[])System.Enum.GetValues(typeof(LedgerLine))).Where(l => ledger.Of(l) != 0f).ToList();
            DrawToday(ledger, lines);
            DrawTonightsMenu();
            DrawMoneyWent(ledger, lines);
            DrawTrendAndTeam();
        }

        // Each dish on tonight's menu at the price it sold for, with how many ordered it, how many
        // gave up waiting, and the stars it got. Walk-outs tint the row, as the old table did.
        private void DrawTonightsMenu()
        {
            const float x = 1293f, width = 507f, top = 262f, height = 301f, rowHeight = 54f;
            UiKit.RoundBox(_content, "Menu", _card, x, top, width, height, 18f);
            Caps(_content, "TONIGHT'S MENU", x + 26f, top + 38f);
            var book = Services.Cookbook;
            if (book == null || !book.Menu.Any())
            {
                Label(Serif, _content, Tr.Get("No menu yet."), 24f, _muted, x + 26f, top + 100f);
                return;
            }

            var visits = TonightsVisits();
            var dishes = book.Menu.ToList();
            var rows = Mathf.Min(dishes.Count, 4);
            var y = top + 58f;
            for (var i = 0; i < rows; i++)
            {
                var recipe = dishes[i];
                var mine = visits.Where(v => v.RecipeId == recipe.Id || (v.RecipeId == null && i == 0 && v.Dish.StartsWith(recipe.MainName.ToLowerInvariant()))).ToList();
                var walked = mine.Count(v => v.WalkedOut);
                if (walked > 0)
                    UiKit.RoundBox(_content, "Alarm", _alarm, x + 12f, y, width - 24f, rowHeight - 6f, 12f);
                Label(Bold, _content, Tr.Get(recipe.Name), 21.33f, _ink, x + 26f, y + 24f, 330f);
                var rated = mine.Where(v => v.Stars.HasValue).Select(v => v.Stars.Value).ToList();
                var said = Tr.Plural(mine.Count, "1 ordered", "{n} ordered");
                if (walked > 0)
                    said += " · " + Tr.Plural(walked, "1 walked out", "{n} walked out");
                if (rated.Count > 0)
                    said += " · " + Tr.Format("{stars} stars", ("stars", rated.Average().ToString("0.0")));
                Label(Medium, _content, said, 16.67f, walked > 0 ? _alarmText : _muted, x + 26f, y + 45f, 340f);

                var price = book.PriceOf(recipe.Id);
                Label(ExtraBold, _content, Money(price), 24f, _ink, x + width - 26f, y + 26f, 120f, HorizontalAlignmentOptions.Right);
                if (price != recipe.SuggestedPrice)
                    Label(Medium, _content, Percent(book.RatioOf(recipe)), 16.67f, _muted, x + width - 26f, y + 45f, 120f, HorizontalAlignmentOptions.Right);
                y += rowHeight;
            }
            if (dishes.Count > rows)
                Label(Medium, _content, Tr.Plural(dishes.Count - rows, "1 more dish in the cookbook tab", "{n} more dishes in the cookbook tab"), 16.67f, _muted, x + 26f, top + height - 14f, 450f);
        }

        // "+20%" or "−10%" against the suggested price.
        private static string Percent(float ratio)
        {
            var percent = Mathf.RoundToInt((ratio - 1f) * 100f);
            return percent > 0 ? $"+{percent}%" : $"−{-percent}%";
        }

        // ---- Cookbook: the first player picks the menu and prices it (decision 2026-11-10) ----

        private const float RecipeListWidth = 470f, RecipeRow = 92f, DetailX = 1256f;

        private void DrawCookbook()
        {
            var book = Services.Cookbook;
            if (book == null || book.Recipes.Count == 0)
            {
                Label(Serif, _content, Tr.Get("No cookbook here."), 26.67f, _muted, RightX, 320f);
                return;
            }
            var chosen = Mathf.Clamp(_selected, 0, book.Recipes.Count - 1);

            // The list, scrolled so the chosen recipe shows.
            var fits = Mathf.FloorToInt((FooterTop - 20f - 262f) / (RecipeRow + 12f));
            var first = book.Recipes.Count <= fits ? 0 : Mathf.Clamp(chosen - fits / 2, 0, book.Recipes.Count - fits);
            var y = 262f;
            for (var i = first; i < book.Recipes.Count && i < first + fits; i++)
            {
                RecipeCard(book, book.Recipes[i], y, i == chosen);
                y += RecipeRow + 12f;
            }

            DrawRecipe(book, book.Recipes[chosen]);
        }

        private void RecipeCard(Game.Simulation.Cookbook.RecipeBook book, Game.Simulation.Cookbook.Recipe recipe, float y, bool chosen)
        {
            if (chosen)
                UiKit.RoundBox(_content, "Highlight", _gold, RightX - 5.67f, y - 5.67f, RecipeListWidth + 11.33f, RecipeRow + 11.33f, 20f);
            UiKit.StrokedBox(_content, "Recipe", _cardLight, chosen ? _gold : _cardEdge, 2.67f, RightX, y, RecipeListWidth, RecipeRow, 15f);
            Label(Bold, _content, Tr.Get(recipe.Name), 23f, _ink, RightX + 24f, y + 38f, RecipeListWidth - 120f);
            var on = book.IsUnlocked(recipe.Id);
            var status = on
                ? Tr.Format("On the menu · {price}", ("price", Money(book.PriceOf(recipe.Id))))
                : Tr.Format("Unlock · {price}", ("price", Money(recipe.UnlockCost)));
            var colour = on ? _goodText : Services.Days.Balance >= recipe.UnlockCost ? _muted : _badText;
            Label(on ? SemiBold : Medium, _content, status, 19.5f, colour, RightX + 24f, y + 70f, RecipeListWidth - 48f);
            ComplexityDots(recipe.Complexity, RightX + RecipeListWidth - 24f - 3f * 20f, y + 26f);
        }

        private void ComplexityDots(int complexity, float x, float y)
        {
            for (var i = 0; i < 3; i++)
                UiKit.Ellipse(_content, "Complexity", i < complexity ? _gold : _rule, x + i * 20f, y, 14f, 14f);
        }

        // The chosen recipe: what it is, what it needs and costs, and its price with what to expect.
        private void DrawRecipe(Game.Simulation.Cookbook.RecipeBook book, Game.Simulation.Cookbook.Recipe recipe)
        {
            const float width = RightEdge - DetailX, top = 262f, bottom = 932f;
            const float x = DetailX + 28f;
            UiKit.RoundBox(_content, "Detail", _card, DetailX, top, width, bottom - top, 18f);
            Label(ExtraBold, _content, Tr.Get(recipe.Name), 34f, _ink, x, top + 54f, width - 56f, tracking: -0.8f);
            ComplexityDots(recipe.Complexity, x, top + 76f);
            var word = recipe.Complexity <= 1 ? Tr.Get("simple") : recipe.Complexity == 2 ? Tr.Get("some work") : Tr.Get("demanding");
            Label(Medium, _content, word, 20f, _body, x + 70f, top + 90f, 200f);

            Caps(_content, "NEEDS", x, top + 136f);
            var px = x;
            foreach (var appliance in recipe.Appliances)
                px = Pill(_content, Tr.Get(appliance), SemiBold, 19.5f, _paper, _ink, px, top + 150f, 36f, 14f, 25f, RightEdge - 28f) + 10f;

            Caps(_content, "INGREDIENTS", x, top + 224f);
            var ix = x;
            for (var i = 0; i < recipe.IngredientTypeIds.Count; i++)
            {
                var icon = _icons != null ? _icons.Find(recipe.IngredientTypeIds[i]) : null;
                if (icon != null)
                {
                    UiKit.Picture(_content, "Ingredient", icon, ix, top + 238f, 40f, 40f);
                    ix += 44f;
                }
                var name = Label(Medium, _content, Tr.Get(recipe.Ingredients[i]), 20f, _ink, ix, top + 266f, 200f);
                ix += UiKit.Width(name) + 22f;
            }
            Label(Medium, _content, Tr.Format("about {cost} a serving", ("cost", Money(recipe.IngredientCost))), 20f, _body, x, top + 308f, width - 56f);
            UiKit.RoundBox(_content, "Rule", _rule, x, top + 332f, width - 56f, 2f, 0f);

            var on = book.IsUnlocked(recipe.Id);
            if (on)
            {
                var price = book.PriceOf(recipe.Id);
                var ratio = book.RatioOf(recipe);
                Caps(_content, "PRICE", x, top + 370f);
                Label(ExtraBold, _content, "‹", 48f, _ink, x, top + 434f, 30f);
                Label(ExtraBold, _content, Money(price), 54f, _ink, x + 120f, top + 436f, 170f, HorizontalAlignmentOptions.Center, -1.5f);
                Label(ExtraBold, _content, "›", 48f, _ink, x + 216f, top + 434f, 30f);
                var suggested = Tr.Format("suggested {price}", ("price", Money(recipe.SuggestedPrice)));
                Label(Medium, _content, suggested, 20f, _body, x + 270f, top + 414f, 230f);
                if (price != recipe.SuggestedPrice)
                    Label(Bold, _content, Percent(ratio), 20f, ratio > 1f ? _badText : _goodText, x + 270f, top + 442f, 230f);

                // What this price does: tonight's guests, from the whole menu's prices, and this dish's stars.
                Caps(_content, "AT THESE PRICES", x, top + 492f);
                var demand = book.BuildMenu().Demand;
                var change = Mathf.RoundToInt((demand - 1f) * 100f);
                var guests = change == 0 ? Tr.Get("Guests: as many as at the suggested prices")
                    : change < 0 ? Tr.Format("Guests: about {percent}% fewer", ("percent", -change))
                    : Tr.Format("Guests: about {percent}% more", ("percent", change));
                Label(Medium, _content, guests, 21.33f, change < 0 ? _badText : _ink, x, top + 528f, width - 56f);
                var shift = Game.Simulation.Cookbook.PriceResponse.Expectation(ratio, book.Pricing);
                var stars = Mathf.Abs(shift) < 0.05f ? Tr.Get("Stars: rated as the dish deserves")
                    : shift < 0f ? Tr.Format("Stars: {stars} fewer for the same dish", ("stars", (-shift).ToString("0.#")))
                    : Tr.Format("Stars: {stars} more for the same dish", ("stars", shift.ToString("0.#")));
                Label(Medium, _content, stars, 21.33f, shift < 0f ? _badText : _ink, x, top + 562f, width - 56f);
                Label(Medium, _content, Tr.Format("earns about {value}", ("value", Money(price - recipe.IngredientCost))) + " " + Tr.Get("a serving"), 21.33f, _ink, x, top + 596f, width - 56f);
            }
            else
            {
                var affordable = Services.Days.Balance >= recipe.UnlockCost;
                Caps(_content, "UNLOCK", x, top + 370f);
                Label(ExtraBold, _content, Money(recipe.UnlockCost), 54f, affordable ? _ink : _badText, x, top + 436f, 260f, tracking: -1.5f);
                Label(Medium, _content, Tr.Format("You have {amount}", ("amount", Money(Services.Days.Balance))), 20f, _body, x + 270f, top + 414f, 230f);
                Label(Medium, _content, Tr.Format("suggested {price}", ("price", Money(recipe.SuggestedPrice))) + " · " +
                    Tr.Format("earns about {value}", ("value", Money(recipe.SuggestedPrice - recipe.IngredientCost))), 21.33f, _ink, x, top + 492f, width - 56f);
            }

            if (_unlockResult == Game.Simulation.Cookbook.UnlockResult.CantAfford)
                Label(Bold, _content, Tr.Get("not enough money"), 22f, _badText, x, top + 636f - 6f, width - 56f);
            else if (_unlockResult == Game.Simulation.Cookbook.UnlockResult.Unlocked && on)
                Label(Bold, _content, Tr.Get("On the menu from tomorrow"), 22f, _goodText, x, top + 630f, width - 56f);
        }

        // ---- Staff: the team, read-only (hiring stays on the Plan card for now) ----

        private void DrawStaff()
        {
            var team = Services.Staff.Hired;
            if (team.Count == 0)
            {
                Label(Serif, _content, Tr.Get("Nobody on the team yet."), 26.67f, _muted, RightX, 320f);
                Label(Medium, _content, Tr.Get("Hire help on the Plan card each morning."), 21.33f, _body, RightX, 362f, 900f);
                return;
            }

            const float height = 118f;
            var y = 262f;
            foreach (var member in team.Take(5))
            {
                UiKit.StrokedBox(_content, "Member", _cardLight, _cardEdge, 2.67f, CardLeft, y, CardWidth, height, 15f);
                UiKit.StrokedEllipse(_content, "Avatar", UiKit.AvatarColour(member.Name), _ink, 4f, CardLeft + 24f, y + 22f, 49f, 61f);
                var name = Label(Bold, _content, member.Name, 22f, _ink, 857f, y + 38f, 300f);
                Label(Medium, _content, "· " + ServiceStatusHud.RoleName(member.Role), 19f, _muted, 857f + UiKit.Width(name) + 10f, y + 38f, 300f);
                Label(Serif, _content, $"“{Tr.Get(member.Description)}”", 22f, _ink, 857f, y + 72f, 600f);
                Label(Medium, _content, ServiceStatusHud.WorkDone(member), 17.33f, _body, 857f, y + 102f, 620f);

                Label(Bold, _content, Tr.Format("{wage} a day", ("wage", Money(member.DailyWage))), 21.33f, _ink, 1776f, y + 38f, 260f, HorizontalAlignmentOptions.Right);
                Meter(Tr.Get("Skill"), member.Skill, 1500f, y + 52f);
                Meter(Tr.Get("Speed"), member.Speed / 2f, 1500f, y + 74f); // 1 is an average pace: the middle of the bar
                Label(SemiBold, _content, ServiceStatusHud.Mood(member), 17.33f, _body, 1776f, y + 108f, 260f, HorizontalAlignmentOptions.Right);
                y += height + 14f;
            }
            var wages = team.Sum(m => m.DailyWage);
            Label(Medium, _content, Tr.Format("Wages {amount} a day · hiring is on the Plan card", ("amount", Money(wages))), 19.5f, _muted, RightX, Mathf.Min(y + 24f, 936f), 900f);
        }

        // A label and a short bar, 0..1, right-aligned to the card's edge.
        private void Meter(string label, float value, float x, float y)
        {
            Label(Medium, _content, label, 16f, _muted, x, y + 12f, 90f);
            UiKit.RoundBox(_content, "Track", _rule, x + 90f, y, 186f, 12f, 6f);
            UiKit.RoundBox(_content, "Fill", _gold, x + 90f, y, Mathf.Max(12f, 186f * Mathf.Clamp01(value)), 12f, 6f);
        }
        // One guest's night, whether or not they reviewed it.
        private sealed class Visit
        {
            public string Dish = "";
            public float ArrivedAt;
            public bool WalkedOut;
            public float Taken;
            /// <summary>The cookbook recipe ordered, or null when only a review remembers the visit.</summary>
            public string RecipeId;
            /// <summary>The stars they gave, if they ate.</summary>
            public float? Stars;
        }

        // Everyone served or who walked out tonight: not every guest reviews (2026-11-09), so the
        // chart and the menu count guests, not reviews. Reviews stand in only when there are no
        // guests to count, as in the screenshot test's staged evening.
        private List<Visit> TonightsVisits()
        {
            var visits = Services.Service.Customers
                .Where(c => c.Order != null && (c.Satisfaction != null || c.WalkedOut))
                .Select(c => new Visit
                {
                    Dish = Game.Simulation.Saving.SaveConversions.OrderName(c.Order),
                    ArrivedAt = c.ArrivedAt,
                    WalkedOut = c.WalkedOut,
                    Taken = c.Bill.HasValue ? c.Bill.Value.Payment + c.Bill.Value.Tip : 0f,
                    RecipeId = c.Order.RecipeId,
                    Stars = c.RatedStars,
                }).ToList();
            if (visits.Count > 0)
                return visits;
            return Services.ReviewHistory.Where(r => r.Day == Services.Days.Day)
                .Select(r => new Visit { Dish = r.Order, ArrivedAt = r.ArrivedAt, WalkedOut = r.WalkedOut, Taken = r.Paid + r.Tip, Stars = r.WalkedOut ? (float?)null : r.RatedStars }).ToList();
        }

        private string TabName(Tab tab) => tab switch
        {
            Tab.Overview => Tr.Get("Overview"),
            Tab.MenuAndBooks => Tr.Get("Menu & Books"),
            Tab.Cookbook => Tr.Get("Cookbook"),
            _ => Tr.Get("Staff"),
        };

        // Today's money in and out, line by line.
        private void DrawToday(Ledger ledger, List<LedgerLine> lines)
        {
            UiKit.RoundBox(_content, "Today", _card, RightX, 262f, 507f, 301f, 18f);
            Caps(_content, "TODAY", 790f, 300f);
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
            Label(Bold, _content, Tr.Get("Net today"), 22.67f, _ink, 790f, 517f, 300f);
            var net = ledger.Net;
            Label(ExtraBold, _content, Money(net, true, signed: true), 32.67f, net < 0f ? _badText : _goodText, 1244f, 517f, 300f, HorizontalAlignmentOptions.Right, -0.5f);
        }

        // In and out as two stacked bars on one scale, each part a ledger line.
        private void DrawMoneyWent(Ledger ledger, List<LedgerLine> lines)
        {
            UiKit.RoundBox(_content, "Went", _card, RightX, 585f, RightWidth, 196f, 18f);
            Caps(_content, "WHERE THE MONEY WENT", 790f, 625f);
            Label(Medium, _content, Tr.Format("Day {day}", ("day", Services.Days.Day)), 19.5f, _muted, 1772f, 625f, 200f, HorizontalAlignmentOptions.Right);

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
            if (Services.Days.Day < 3)
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
            var trend = days.Day >= 3 && history.Count > 0;
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
            LedgerLine.Fee => Tr.Get("Fee to the boss"),
            _ => line.ToString(),
        };

        // ---- The hint bar ----

        private string _hintsShown;

        // Called every frame (players ready up at any time), but only rebuilt when what it says changes.
        private void DrawHints()
        {
            var canReply = !_choosingReply && SelectedReview() is ReviewRecord selected && selected.Reply < 0;
            var filtering = _tab == Tab.Overview && _onFilters;
            var book = Services.Cookbook;
            var recipe = _tab == Tab.Cookbook && book != null && book.Recipes.Count > 0 ? book.Recipes[Mathf.Clamp(_selected, 0, book.Recipes.Count - 1)] : null;
            var onMenu = recipe != null && book.IsUnlocked(recipe.Id);
            var cook = CookbookPlayer();
            var state = $"{_tab}|{canReply}|{filtering}|{recipe?.Id}|{onMenu}|{cook?.playerIndex}|{Services.Days.Day}|" + string.Join(",", PlayerInput.all.Select(p => $"{p.playerIndex}:{_dayLoop.Ready.IsReady(p.playerIndex)}:{_dayLoop.ControllerPause.IsLost(p.playerIndex)}"));
            if (state == _hintsShown)
                return;
            _hintsShown = state;
            UiKit.Clear(_hints);
            var hints = new List<(string[] Keys, string Text)> { (new[] { "LB", "RB" }, Tr.Get("Tabs")) };
            if (_tab == Tab.Overview)
                hints.Add((new[] { "LS" }, filtering ? Tr.Get("Filter") : Tr.Get("Browse")));
            if (canReply)
                hints.Add((new[] { "Y" }, Tr.Get("Reply")));
            // The cookbook answers only the first player; the others see whose it is.
            if (recipe != null)
            {
                var who = cook != null ? $"P{cook.playerIndex + 1}: " : "";
                hints.Add((new[] { "LS" }, who + Tr.Get("Browse")));
                if (onMenu)
                {
                    hints.Add((new[] { "LS" }, Tr.Get("Price, sideways")));
                    hints.Add((new[] { "X" }, Tr.Get("Suggested price")));
                }
                else
                {
                    hints.Add((new[] { "A" }, Tr.Get("Unlock")));
                }
            }
            hints.Add((new[] { "≡" }, Tr.Format("Start day {day}", ("day", Services.Days.Day + 1))));

            // From the right edge leftwards: label, 10 px, keys 10 px apart, then 32 px to the next.
            var right = 1799f;
            for (var i = hints.Count - 1; i >= 0; i--)
            {
                var label = Label(Medium, _hints, hints[i].Text, 21.33f, _wellLight, 0f, 996f, 300f);
                var width = UiKit.Width(label);
                label.rectTransform.anchoredPosition = new Vector2(right - width, label.rectTransform.anchoredPosition.y);
                var x = right - width - 10f;
                for (var k = hints[i].Keys.Length - 1; k >= 0; k--)
                {
                    var key = hints[i].Keys[k];
                    if (key == "Y")
                    {
                        YBadge(_hints, x - 15.5f, 987.5f, 31f);
                        x -= 31f + 10f;
                        continue;
                    }
                    if (key == "A" || key == "X")
                    {
                        Badge(_hints, key, key == "A" ? _buttonA : _buttonX, x - 15.5f, 987.5f, 31f, 15.5f);
                        x -= 31f + 10f;
                        continue;
                    }
                    var keyWidth = key == "LS" ? 44f : 50f;
                    Keycap(_hints, key, x - keyWidth, 971f, keyWidth, 34f);
                    x -= keyWidth + 10f;
                }
                right = x + 10f - 32f;
            }

            // With two or more cooks, who's ready for the next day, on the left.
            if (PlayerInput.all.Count > 1)
            {
                var x = 120f;
                foreach (var player in PlayerInput.all.OrderBy(p => p.playerIndex))
                {
                    var ready = _dayLoop.Ready.IsReady(player.playerIndex);
                    var lost = _dayLoop.ControllerPause.IsLost(player.playerIndex); // not waited for
                    var colour = _settings.GetPlayerColor(player.playerIndex);
                    UiKit.Ellipse(_hints, "Cook", ready ? colour : Alpha(colour, 0.35f), x, 978f, 20f, 20f);
                    var text = lost ? Tr.Format("P{player} controller lost", ("player", player.playerIndex + 1))
                        : ready ? Tr.Format("P{player} ready", ("player", player.playerIndex + 1)) : $"P{player.playerIndex + 1}";
                    var label = Label(SemiBold, _hints, text, 19.5f, ready ? _wellLight : _quiet, x + 28f, 995f, 220f);
                    x += 28f + UiKit.Width(label) + 24f;
                }
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
            UiKit.StrokedCircle(parent, letter, colour, Color.white, 4f, cx - diameter / 2f, cy - diameter / 2f, diameter);
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
            UiKit.TextAt(_style.Medium, _layout, "CaptionText", Tr.Get("Your photo · captured at close"), 17.33f, _paper, 573f, 488f, 270f, HorizontalAlignmentOptions.Center);
            UiKit.RoundBox(_layout, "Guests", _card, 120f, 700f, 600f, 222f, 18f);

            _header = UiKit.Rect(_layout, "HeaderState", 0f, 0f, 1920f, 1080f);
            _left = UiKit.Rect(_layout, "Left", 0f, 0f, 1920f, 1080f);
            _tabs = UiKit.Rect(_layout, "Tabs", 0f, 0f, 1920f, 1080f);
            _content = UiKit.Rect(_layout, "Content", 0f, 0f, 1920f, 1080f);
            // The review list scrolls inside its window, so cards are clipped at its bottom edge.
            var window = UiKit.Rect(_layout, "ListWindow", RightX - 8f, ListTop - 8f, RightWidth + 16f, ListBottom - ListTop + 8f);
            window.gameObject.AddComponent<RectMask2D>();
            _list = UiKit.Rect(window, "List", -(RightX - 8f), -(ListTop - 8f), 1920f, 1080f);
            _hints = UiKit.Rect(_layout, "Hints", 0f, 0f, 1920f, 1080f);
            _popover = UiKit.Rect(_layout, "Popover", 0f, 0f, 1920f, 1080f);

            // Never shown: measures how many lines a reply wraps to, so its card can make room.
            _measure = UiKit.TextAt(Medium, _layout, "Measure", "", ReplySize, Color.clear, -4000f, -4000f, ReplyWidth, lines: 2);
        }
    }
}
