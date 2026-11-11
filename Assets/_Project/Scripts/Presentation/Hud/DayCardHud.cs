using Game.Gameplay;
using Game.Presentation.Menu;
using Game.Simulation.Days;
using Game.Simulation.Service;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Object = UnityEngine.Object;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The kitchen's day card, bottom left (decision 2026-11-10, after the user's request): "Day 3"
    /// and under it the phase and the game clock, "Service · 18:42". The money card's sibling but
    /// quieter: shorter, a little see-through, a thin sand outline and softer text, and nothing
    /// moves but the minutes. Last orders and Closed show in the notice amber. It hides with the
    /// money card behind the forkcast dashboard and the pause menu. Built in code on its own canvas,
    /// inside the screen's margin at any resolution or aspect; the boss's phone is bottom right.
    /// <para>In Prep it becomes two tiles above the ready bar (the user's design, 2026-11-11): a
    /// calendar page ("FRI 5") with "Prep / Opens 18:00", and two guests' faces with "About 5
    /// guests / Groups of one". The forecast follows the tables and the menu's prices as they
    /// change, as the night itself will.</para>
    /// </summary>
    public sealed class DayCardHud : MonoBehaviour
    {
        private const float Margin = 24f;      // the money card's and the player bar's
        private const float Radius = 18f;
        private const float MinWidth = 210f;
        private const float Pad = 20f;
        private const float LineGap = 6f;
        private const int SortingOrder = 15;
        // Prep's tiles: stacked above the ready bar (PrepReadyBarHud, 62 high).
        private const float BarHeight = 62f;
        private const float TileGap = 12f;
        private const float TileMinWidth = 300f;
        private const float IconColumn = 100f;
        private const float TileLineSize = 24f;
        private const float CalendarWidth = 58f, CalendarHeight = 62f, CalendarBand = 18f;
        private const float Face = 50f, FaceStep = 30f;
        private static readonly Color CalendarPaper = new Color(0.953f, 0.918f, 0.851f);
        private static readonly Color CalendarRed = new Color(0.788f, 0.290f, 0.227f);
        private static readonly Color CalendarInk = new Color(0.165f, 0.102f, 0.063f);

        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private PauseMenu _pause;

        private Canvas _canvas;
        private RectTransform _card;
        private CanvasGroup _group;
        private Image _edge;
        private Image _back;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _line;
        private string _shown;
        private RectTransform _tiles;
        private CanvasGroup _tilesGroup;
        private string _tilesShown;
        private int _guests;
        private float _groupSize = 1f;
        private float _forecastAt = -1f;

        private void Awake()
        {
            if (_canvas == null)
                Build();
        }

        private void Build()
        {
            var canvasObject = new GameObject("DayCard", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var height = _style.DayCardHeight;
            _card = UiKit.Rect(_canvas.transform, "Day", 0f, 0f, MinWidth, height);
            _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(0f, 0f);
            _card.anchoredPosition = new Vector2(Margin, Margin);
            _group = _card.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var fill = _style.CardBackground;
            fill.a *= _style.DayCardOpacity;
            _back = UiKit.StrokedBox(_card, "Back", fill, _style.DayCardEdge, _style.DayCardStroke, 0f, 0f, MinWidth, height, Radius);
            _edge = _card.Find("BackEdge").GetComponent<Image>();

            // Two lines centred in the card as a block: the title's cap height, a gap, the line's.
            var block = _style.DayTitleSize * 0.72f + LineGap + _style.DayLineSize * 0.72f;
            var top = (height - block) / 2f;
            _title = UiKit.TextAt(_style.DisplayFont, _card, "Title", "", _style.DayTitleSize, _style.DayText, Pad, top + _style.DayTitleSize * 0.72f, 600f);
            _line = UiKit.TextAt(_style.Bold, _card, "Line", "", _style.DayLineSize, _style.DayText, Pad, top + block, 600f);

            _tiles = UiKit.Rect(_canvas.transform, "PrepTiles", 0f, 0f, TileMinWidth, 2f * height + TileGap);
            _tiles.anchorMin = _tiles.anchorMax = _tiles.pivot = new Vector2(0f, 0f);
            _tiles.anchoredPosition = new Vector2(Margin, BarHeight + Margin);
            _tilesGroup = _tiles.gameObject.AddComponent<CanvasGroup>();
            _tilesGroup.alpha = 0f;
            _tilesGroup.blocksRaycasts = false;
            _tilesGroup.interactable = false;
        }

        private void LateUpdate()
        {
            if (_canvas == null)
                return;
            var services = _root != null ? _root.Services : null;
            var shown = services != null && services.Days != null && !services.Forkcast.IsOpen && !(_pause != null && _pause.IsOpen);
            var fade = _style.Fade;
            var goal = shown ? 1f : 0f;
            var prep = shown && services.Days.Phase == DayPhase.Prep;
            var cardGoal = shown && !prep ? 1f : 0f;
            var tilesGoal = prep ? 1f : 0f;
            _group.alpha = fade <= 0f ? cardGoal : Mathf.MoveTowards(_group.alpha, cardGoal, Time.unscaledDeltaTime / fade);
            _tilesGroup.alpha = fade <= 0f ? tilesGoal : Mathf.MoveTowards(_tilesGroup.alpha, tilesGoal, Time.unscaledDeltaTime / fade);
            if (!shown)
                return;
            if (prep)
            {
                Forecast(services);
                DrawTiles(services.Days.Day, services.ServiceConfig.OpensAt, _guests, _groupSize);
                return;
            }
            var service = services.Service;
            Draw(services.Days.Day, services.Days.Phase, service != null ? service.Phase : ServicePhase.Open, services.ServiceConfig.OpensAt, service != null ? service.Elapsed : 0f);
        }

        // Tonight's forecast, worked out twice a second, not every frame: it builds the menu to read
        // its demand. The tables as they stand now, and their average group.
        private void Forecast(Game.Gameplay.KitchenServices services)
        {
            if (_forecastAt >= 0f && Time.unscaledTime - _forecastAt < 0.5f)
                return;
            _forecastAt = Time.unscaledTime;
            var tables = services.Stations.All.OfType<Game.Gameplay.Stations.TableStation>().Select(t => t.SeatCount).Where(n => n > 0).ToList();
            var demand = services.Cookbook != null ? services.Cookbook.BuildMenu().Demand : 1f;
            _guests = GuestForecast.Guests(services.Service.Settings, demand, tables);
            _groupSize = GuestForecast.GroupSize(tables);
        }

        /// <summary>Draws Prep's two tiles at once outside Play mode, for a still.</summary>
        public void PreviewPrep(int day, float opensAtHour, int guests, float groupSize)
        {
            Awake();
            _group.alpha = 0f;
            _tilesGroup.alpha = 1f;
            DrawTiles(day, opensAtHour, guests, groupSize);
            Canvas.ForceUpdateCanvases();
        }

        // Prep's tiles, rebuilt only when what they say changes.
        private void DrawTiles(int day, float opensAtHour, int guests, float groupSize)
        {
            var weekday = DayCardText.Weekday(day).ToUpperInvariant();
            var title = DayCardText.Phase(DayPhase.Prep);
            var opens = DayCardText.Opens(opensAtHour);
            var count = DayCardText.Guests(guests);
            var groups = DayCardText.Groups(groupSize);
            var key = $"{day}|{weekday}|{title}|{opens}|{count}|{groups}";
            if (key == _tilesShown)
                return;
            _tilesShown = key;
            UiKit.Clear(_tiles);

            var height = _style.DayCardHeight;
            // Measure both tiles' words first: the two share one width, the wider's.
            var probe = UiKit.TextAt(_style.DisplayFont, _tiles, "Probe", "", _style.DayTitleSize, _style.DayText, 0f, 0f, 2000f);
            float Measure(string text, TMPro.TMP_FontAsset font, float size)
            {
                probe.font = font;
                probe.fontSize = size;
                probe.text = text;
                return UiKit.Width(probe);
            }
            var words = Mathf.Max(
                Mathf.Max(Measure(title, _style.DisplayFont, _style.DayTitleSize), Measure(opens, _style.Bold, TileLineSize)),
                Mathf.Max(Measure(count, _style.DisplayFont, _style.DayTitleSize), Measure(groups, _style.Bold, TileLineSize)));
            Object.DestroyImmediate(probe.gameObject);
            var width = Mathf.Max(TileMinWidth, Pad + IconColumn + words + Pad + 8f);
            var canvasWidth = ((RectTransform)_canvas.transform).rect.width;
            if (canvasWidth > 1f)
                width = Mathf.Min(width, canvasWidth - 2f * Margin); // whole on screen, always
            _tiles.sizeDelta = new Vector2(width, 2f * height + TileGap);

            var day1 = Tile(0f, width, title, opens);
            Calendar(day1, Pad + IconColumn / 2f, height / 2f, weekday, day.ToString());
            var guestsTile = Tile(height + TileGap, width, count, groups);
            Faces(guestsTile, Pad + IconColumn / 2f, height / 2f, day);
        }

        // One tile: the day card's look (see-through, sand outline), an icon column and two lines.
        private RectTransform Tile(float top, float width, string title, string line)
        {
            var height = _style.DayCardHeight;
            var tile = UiKit.Rect(_tiles, "Tile", 0f, top, width, height);
            var fill = _style.CardBackground;
            fill.a *= _style.DayCardOpacity;
            UiKit.StrokedBox(tile, "Back", fill, _style.DayCardEdge, _style.DayCardStroke, 0f, 0f, width, height, Radius);
            const float gap = 12f; // a touch looser than the day card's: the line is smaller
            var block = _style.DayTitleSize * 0.72f + gap + TileLineSize * 0.72f;
            var textTop = (height - block) / 2f;
            var x = Pad + IconColumn;
            UiKit.TextAt(_style.DisplayFont, tile, "Title", title, _style.DayTitleSize, _style.DayText, x, textTop + _style.DayTitleSize * 0.72f, width - x - Pad);
            UiKit.TextAt(_style.Bold, tile, "Line", line, TileLineSize, Color.Lerp(_style.DayText, _style.CardBackground, 0.22f), x, textTop + block, width - x - Pad);
            return tile;
        }

        // A tear-off calendar page: the weekday on a red band, the day's number below.
        private void Calendar(RectTransform parent, float cx, float cy, string weekday, string number)
        {
            var x = cx - CalendarWidth / 2f;
            var y = cy - CalendarHeight / 2f;
            // A darker copy just below gives the page a little thickness (flat, no shadow).
            UiKit.RoundBox(parent, "Under", new Color(0f, 0f, 0f, 0.35f), x, y + 3f, CalendarWidth, CalendarHeight, 8f);
            var page = UiKit.RoundBox(parent, "Page", CalendarPaper, x, y, CalendarWidth, CalendarHeight, 8f);
            page.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            UiKit.RoundBox(page.transform, "Band", CalendarRed, 0f, 0f, CalendarWidth, CalendarBand, 0f);
            UiKit.TextAt(_style.ExtraBold, page.transform, "Weekday", weekday, 13f, Color.white, CalendarWidth / 2f, CalendarBand / 2f + 4.7f, CalendarWidth, HorizontalAlignmentOptions.Center);
            var numberSize = 34f;
            var middle = CalendarBand + (CalendarHeight - CalendarBand) / 2f;
            UiKit.TextAt(_style.DisplayFont, page.transform, "Number", number, numberSize, CalendarInk, CalendarWidth / 2f, middle + numberSize * 0.36f, CalendarWidth, HorizontalAlignmentOptions.Center);
        }

        // Two of tonight's kind of guests, overlapping: baked portraits of the customer looks, a
        // different pair each day.
        private void Faces(RectTransform parent, float cx, float cy, int day)
        {
            var left = cx - (Face + FaceStep) / 2f - 4f;
            var edge = _style.CardBackground;
            for (var i = 0; i < 2; i++)
            {
                var guest = $"tonight {day} {i}";
                var look = day * 2 + i * 3;
                UiKit.GuestPortrait(parent, _style.CustomerPortrait(look, guest), guest, left + i * FaceStep, cy - Face / 2f, Face, edge);
            }
        }

        /// <summary>Draws the card at once outside Play mode, for a still.</summary>
        public void Preview(int day, DayPhase phase, ServicePhase service, float opensAtHour, float elapsedSeconds)
        {
            Awake();
            _group.alpha = 1f;
            Draw(day, phase, service, opensAtHour, elapsedSeconds);
            Canvas.ForceUpdateCanvases();
        }

        private void Draw(int day, DayPhase phase, ServicePhase service, float opensAtHour, float elapsedSeconds)
        {
            var title = DayCardText.Title(day);
            var (line, notice) = DayCardText.Line(phase, service, opensAtHour, elapsedSeconds);
            var key = title + "\n" + line + notice;
            if (key == _shown)
                return; // the text changes once a game minute, so the layout does too
            _shown = key;
            _title.text = title;
            _line.text = line;
            _line.color = notice ? _style.DayNotice : _style.DayText;

            var width = Mathf.Max(MinWidth, Pad * 2f + Mathf.Max(UiKit.Width(_title), UiKit.Width(_line)));
            var canvasWidth = ((RectTransform)_canvas.transform).rect.width;
            if (canvasWidth > 1f)
                width = Mathf.Min(width, canvasWidth - 2f * Margin); // whole on screen, always
            var height = _style.DayCardHeight;
            var stroke = _style.DayCardStroke;
            _card.sizeDelta = new Vector2(width, height);
            _edge.rectTransform.sizeDelta = new Vector2(width + stroke, height + stroke);
            _back.rectTransform.sizeDelta = new Vector2(width - stroke, height - stroke);
            _title.rectTransform.sizeDelta = new Vector2(width - 2f * Pad, _title.rectTransform.sizeDelta.y);
            _line.rectTransform.sizeDelta = new Vector2(width - 2f * Pad, _line.rectTransform.sizeDelta.y);
        }
    }
}
