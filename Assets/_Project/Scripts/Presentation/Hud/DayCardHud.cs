using Game.Data;
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
    /// The kitchen's day card, bottom left. In service and closing it's the service timeline (the
    /// user's pick, 2026-11-14, <see cref="ServiceTimelineCard"/>): the time and "Day 1 · Dinner"
    /// over the whole service, its rushes and last orders, a sun or moon at the hour, and a callout
    /// as each rush starts. It hides with the money card behind the forkcast dashboard and the
    /// pause menu. Built in code on its own canvas, inside the screen's margin at any resolution or
    /// aspect; the boss's phone is bottom right.
    /// <para>In Prep it becomes two tiles above the ready bar (the user's design, 2026-11-11): a
    /// calendar page ("FRI 5") with "Prep · Day 5 / Opens 18:00", and two guests' faces with "About 5
    /// guests / Groups of one". The forecast follows the tables and the menu's prices as they
    /// change, as the night itself will. The View button's "Rearrange" docks under the
    /// money card, as wide as it (the user's call, 2026-11-11), the only place it's shown.</para>
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
        // The rearrange hint under them: a slimmer tile with the View button and its word.
        private const float HintHeight = 56f;
        private const float KeyWidth = 44f, KeyHeight = 30f;
        private static readonly Color KeyColour = new Color(0.32f, 0.33f, 0.37f);
        private static readonly Color CalendarPaper = new Color(0.953f, 0.918f, 0.851f);
        private static readonly Color CalendarRed = new Color(0.788f, 0.290f, 0.227f);
        private static readonly Color CalendarInk = new Color(0.165f, 0.102f, 0.063f);

        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private PauseMenu _pause;
        [Tooltip("The money card, top right: Prep's Rearrange docks under it at its width. Found in the scene if empty.")]
        [SerializeField] private MoneyCardHud _money;

        private Canvas _canvas;
        private ServiceTimelineCard _timeline;
        private int _rush = -1;
        private RectTransform _tiles;
        private RectTransform _rearrange;
        private string _rearrangeShown;
        private CanvasGroup _rearrangeGroup;
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
            _timeline = new ServiceTimelineCard(_style, _canvas.transform, Margin);

            _tiles = UiKit.Rect(_canvas.transform, "PrepTiles", 0f, 0f, TileMinWidth, 2f * height + TileGap);
            _tiles.anchorMin = _tiles.anchorMax = _tiles.pivot = new Vector2(0f, 0f);
            _tiles.anchoredPosition = new Vector2(Margin, BarHeight + Margin);
            _rearrange = UiKit.Rect(_tiles.parent, "Rearrange", 0f, 0f, 210f, HintHeight);
            // Under the money card, in its corner (top right again since 2026-11-14).
            _rearrange.anchorMin = _rearrange.anchorMax = _rearrange.pivot = new Vector2(1f, 1f);
            _rearrangeGroup = _rearrange.gameObject.AddComponent<CanvasGroup>();
            _rearrangeGroup.alpha = 0f;
            _rearrangeGroup.blocksRaycasts = false;
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
            _timeline.Alpha = fade <= 0f ? cardGoal : Mathf.MoveTowards(_timeline.Alpha, cardGoal, Time.unscaledDeltaTime / fade);
            _tilesGroup.alpha = fade <= 0f ? tilesGoal : Mathf.MoveTowards(_tilesGroup.alpha, tilesGoal, Time.unscaledDeltaTime / fade);
            _rearrangeGroup.alpha = _tilesGroup.alpha;
            if (!shown)
                return;
            if (prep)
            {
                Forecast(services);
                DrawTiles(services.Days.Day, services.Service.Settings.Clock.OpensAtHour, _guests, _groupSize);
                DrawRearrange();
                return;
            }
            var service = services.Service;
            if (service == null)
                return;
            var settings = service.Settings;
            var slot = ServiceTimeline.SlotOf(services.Opening, settings);
            var elapsed = services.Days.Phase == DayPhase.Plan ? 0f : service.Elapsed;
            var phase = services.Days.Phase == DayPhase.Plan ? ServicePhase.Open : service.Phase;
            CallRush(settings, phase, elapsed);
            _timeline.Draw(services.Days.Day, slot != null ? Tr.Get(slot.Name) : null, settings, phase, elapsed, Time.unscaledTime);
        }

        // "Dinner rush!" pops up once as the clock enters each rush, while the doors are open.
        private void CallRush(ServiceSettings settings, ServicePhase phase, float elapsed)
        {
            var rush = phase == ServicePhase.Open ? ServiceTimeline.RushAt(settings, elapsed) : -1;
            if (rush >= 0 && rush != _rush)
                _timeline.Pop(DayCardText.Rush(ServiceTimeline.Rushes(settings)[rush].PeakHour), Time.unscaledTime);
            _rush = rush;
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
            _timeline.Alpha = 0f;
            _tilesGroup.alpha = 1f;
            _rearrangeGroup.alpha = 1f;
            DrawTiles(day, opensAtHour, guests, groupSize);
            DrawRearrange();
            Canvas.ForceUpdateCanvases();
        }

        // Prep's tiles, rebuilt only when what they say changes.
        private void DrawTiles(int day, float opensAtHour, int guests, float groupSize)
        {
            var weekday = DayCardText.Weekday(day).ToUpperInvariant();
            // "Prep · Day 5" (the user's call, 2026-11-11): the day in words beside the calendar page.
            var title = Tr.Format("{phase} · Day {day}", ("phase", DayCardText.Phase(DayPhase.Prep)), ("day", day));
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

        // Rearrange under the money card, top right, as wide as it; rebuilt when the width or the
        // language changes.
        private void DrawRearrange()
        {
            if (_money == null)
                _money = FindFirstObjectByType<MoneyCardHud>();
            var width = _money != null ? Mathf.Round(_money.Width) : 210f;
            var below = _money != null ? _money.CardHeight : 108f;
            var word = Tr.Get("Rearrange");
            var key = $"{width}|{word}";
            _rearrange.anchoredPosition = new Vector2(-Margin, -(Margin + below + TileGap));
            if (key == _rearrangeShown)
                return;
            _rearrangeShown = key;
            UiKit.Clear(_rearrange);
            _rearrange.sizeDelta = new Vector2(width, HintHeight);
            Hint(_rearrange, width, word);
        }

        // The View button as a key cap (two overlapping windows, drawn: no font has it) and its word,
        // the key centred in the icon column like the icons above.
        private void Hint(RectTransform parent, float width, string word)
        {
            var tile = UiKit.Rect(parent, "Hint", 0f, 0f, width, HintHeight);
            var fill = _style.CardBackground;
            fill.a *= _style.DayCardOpacity;
            UiKit.StrokedBox(tile, "Back", fill, _style.DayCardEdge, _style.DayCardStroke, 0f, 0f, width, HintHeight, Radius);
            // The key and the word centred together, as a button reads.
            var label = UiKit.TextAt(_style.Bold, tile, "Word", word, TileLineSize, _style.DayText, 0f, HintHeight / 2f + TileLineSize * 0.36f, width - KeyWidth - 12f - 28f);
            // A long word ("Reorganizar") shrinks a little to fit the money card's width rather than being cut.
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = TileLineSize;
            label.ForceMeshUpdate();
            var block = KeyWidth + 12f + Mathf.Min(label.preferredWidth, label.rectTransform.sizeDelta.x);
            var left = (width - block) / 2f;
            label.rectTransform.anchoredPosition = new Vector2(left + KeyWidth + 12f, label.rectTransform.anchoredPosition.y);
            var cx = left + KeyWidth / 2f;
            var cy = HintHeight / 2f;
            UiKit.RoundBox(tile, "Key", KeyColour, cx - KeyWidth / 2f, cy - KeyHeight / 2f, KeyWidth, KeyHeight, 8f);
            UiKit.StrokedBox(tile, "Back", KeyColour, Color.white, 2f, cx - 9f, cy - 8f, 13f, 11f, 2f);
            UiKit.StrokedBox(tile, "Front", KeyColour, Color.white, 2f, cx - 4f, cy - 3f, 13f, 11f, 2f);
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

        /// <summary>
        /// Draws the service card at once outside Play mode, for a still: <paramref name="elapsed"/>
        /// in-game seconds in, and <paramref name="callout"/> seconds after a rush callout popped (null: none).
        /// </summary>
        public void PreviewService(int day, string slot, ServiceSettings settings, ServicePhase phase, float elapsed, float? callout = null)
        {
            Awake();
            _tilesGroup.alpha = 0f;
            _rearrangeGroup.alpha = 0f;
            _timeline.Alpha = 1f;
            const float now = 100f;
            _timeline.ClearCallout();
            if (callout.HasValue)
            {
                var rush = ServiceTimeline.RushAt(settings, elapsed);
                var rushes = ServiceTimeline.Rushes(settings);
                _timeline.Draw(day, slot, settings, phase, elapsed, now); // builds it
                _timeline.Pop(DayCardText.Rush(rushes[Mathf.Max(0, rush)].PeakHour), now - callout.Value);
            }
            _timeline.Draw(day, slot, settings, phase, elapsed, now);
            Canvas.ForceUpdateCanvases();
        }
    }
}
