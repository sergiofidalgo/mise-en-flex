using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Simulation.Movement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The shop as cards (the user's notes, 2026-11-10): each player who opens the shop in management
    /// mode (Y) gets a card the size of the tailor's dressing card, so four can shop at once. Cards dock
    /// in a row from the top-left corner in player-slot order, below the clock, and slide along as
    /// others open and close. The page (furniture, appliances, kitchenware, and decoration once there
    /// is some) sits between LB and RB with a dot per page, as the dressing card's sections do.
    /// <list type="bullet">
    /// <item>A list of what's on the page, the chosen row in the player's colour, the price in red
    /// when it's out of reach.</item>
    /// <item>A line on how the chosen piece arrives: carried and paid for where it's put, or
    /// bought at once onto a free counter.</item>
    /// <item>Every press answers at once: a new row pops, a purchase flashes the card's edge, and a
    /// refusal shakes the row with the reason in red.</item>
    /// </list>
    /// Only Xbox buttons are named, and the whole card always stays on screen. The cookbook isn't
    /// here: it's a tab of the forkcast dashboard at Review.
    /// </summary>
    public sealed class ManagementCardsHud : MonoBehaviour
    {
        private const float Width = 360f;
        private const float Height = 500f;
        private const float Margin = 24f;
        private const float CardGap = 12f;
        private const float ShoulderWidth = 50f;
        private const float ListTop = 110f;
        private const float RowHeight = 40f;
        private const int Rows = 5; // the longest page today; more scroll
        private const float PriceColumn = 72f;
        private const float PopTime = 0.16f;
        private const float ShakeTime = 0.3f;
        private const float FlashTime = 0.35f;
        private const float OpenTime = 0.12f;

        [SerializeField] private HudStyle _style;
        [SerializeField] private CoopSettings _coop;
        [SerializeField] private LayoutEditor _editor;
        [SerializeField] private KitchenCompositionRoot _root;
        [Tooltip("Canvas pixels from the top to the cards, leaving the clock and the money above them clear.")]
        [SerializeField, Min(0f)] private float _top = 96f;
        [SerializeField] private Color _blocked = new Color(0.95f, 0.30f, 0.26f);

        private readonly Dictionary<int, Card> _cards = new Dictionary<int, Card>();
        private Canvas _canvas;

        private sealed class Row
        {
            public Image Highlight;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Price;
        }

        private sealed class Card
        {
            public RectTransform Root;
            public Image Edge;
            public Color Colour;
            public GameObject Turn;
            public TextMeshProUGUI Title;
            public readonly List<Image> Pages = new List<Image>();
            public TextMeshProUGUI Balance;
            public TextMeshProUGUI Count;
            public Row[] Rows;
            public TextMeshProUGUI Arrives;
            public TextMeshProUGUI Message;

            // What was shown last frame, to notice what changed.
            public bool WasOpen;
            public ShopSection Section;
            public int Index = -1;
            public int Presses;
            public float Open = 1f;     // 0 just opened, 1 settled
            public float RowPop = 1f;
            public float Shake = 1f;
            public float Flash = 1f;
        }

        private void Awake()
        {
            // On the kitchen HUD's canvas this is a sub-canvas: it fills the HUD and takes its
            // scaling, drawn over the world labels and prompts. On its own it scales itself.
            var canvasObject = new GameObject("ShopCards", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var rect = (RectTransform)canvasObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 30;
            if (GetComponentInParent<Canvas>() == null)
            {
                var scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        /// <summary>True while this player's shop shows as a card (the cursor's label then stays quiet).</summary>
        public bool Shows(LayoutEditor.Cursor cursor) => isActiveAndEnabled && cursor.ShopOpen;

        private void LateUpdate()
        {
            var dt = Time.unscaledDeltaTime;
            var open = _editor != null
                ? _editor.Cursors.Where(c => c.ShopOpen).OrderBy(c => c.Player.playerIndex).ToList()
                : new List<LayoutEditor.Cursor>();

            foreach (var card in _cards.Values)
                card.Root.gameObject.SetActive(false);

            var x = Margin;
            foreach (var cursor in open)
            {
                var index = cursor.Player.playerIndex;
                if (!_cards.TryGetValue(index, out var card))
                    _cards[index] = card = Build(index);
                card.Root.gameObject.SetActive(true);
                Fill(card, cursor);
                Animate(card, dt);
                // The next card along, whole on screen; the pivot is the top middle, so it grows from the top.
                var left = Mathf.Min(x, CanvasWidth() - Margin - Width);
                card.Root.anchoredPosition = new Vector2(left + Width / 2f, -_top);
                x += Width + CardGap;
            }
            foreach (var card in _cards.Values)
            {
                if (!card.Root.gameObject.activeSelf)
                    card.WasOpen = false;
            }
        }

        // ---- Building ----------------------------------------------------------------------------

        private Card Build(int index)
        {
            var colour = _coop != null ? _coop.GetPlayerColor(index) : Color.white;
            var card = new Card { Colour = colour };
            card.Root = UiKit.Rect(_canvas.transform, $"Player{index + 1}Shop", 0f, 0f, Width, Height);
            card.Root.pivot = new Vector2(0.5f, 1f);
            UiKit.StrokedBox(card.Root, "Back", _style.CardBackground, colour, 4f, 0f, 0f, Width, Height, 22f);
            card.Edge = card.Root.Find("BackEdge")?.GetComponent<Image>();

            var turn = UiKit.Rect(card.Root, "Turn", 0f, 0f, Width, Height);
            DrawShoulder(turn, "LB", 12f, 12f);
            DrawShoulder(turn, "RB", Width - 12f - ShoulderWidth, 12f);
            card.Turn = turn.gameObject;
            card.Title = UiKit.TextAt(_style.Bold, card.Root, "Page", "", 30f, _style.TextColor, Width / 2f, 40f, Width - 2f * (ShoulderWidth + 20f), HorizontalAlignmentOptions.Center);
            card.Title.enableAutoSizing = true;
            card.Title.fontSizeMin = 24f;
            card.Title.fontSizeMax = 30f;
            card.Balance = UiKit.TextAt(_style.Font, card.Root, "Balance", "", 24f, Dim(0.7f), Width / 2f, 94f, Width - 24f, HorizontalAlignmentOptions.Center);
            card.Count = UiKit.TextAt(_style.Font, card.Root, "Count", "", 20f, Dim(0.55f), Width - 22f, 94f, 80f, HorizontalAlignmentOptions.Right);

            card.Rows = new Row[Rows];
            for (var i = 0; i < Rows; i++)
            {
                var y = ListTop + i * RowHeight;
                var row = new Row { Highlight = UiKit.RoundBox(card.Root, "Chosen", colour, 10f, y, Width - 20f, RowHeight - 4f, 12f) };
                // The name takes all but the price's column, shrinking a little before it's cut short.
                row.Name = UiKit.TextAt(_style.Bold, card.Root, "Name", "", 24f, _style.TextColor, 22f, y + 27f, Width - 44f - PriceColumn);
                row.Name.enableAutoSizing = true;
                row.Name.fontSizeMin = 18f;
                row.Name.fontSizeMax = 24f;
                row.Price = UiKit.TextAt(_style.DisplayFont, card.Root, "Price", "", 24f, _style.TextColor, Width - 22f, y + 27f, PriceColumn - 8f, HorizontalAlignmentOptions.Right);
                card.Rows[i] = row;
            }

            var below = ListTop + Rows * RowHeight;
            UiKit.RoundBox(card.Root, "Rule", Dim(0.15f), 16f, below + 6f, Width - 32f, 2f, 0f);
            card.Arrives = UiKit.TextAt(_style.Font, card.Root, "Arrives", "", 20f, Dim(0.65f), Width / 2f, below + 40f, Width - 32f, HorizontalAlignmentOptions.Center, lines: 2);
            card.Message = UiKit.TextAt(_style.Bold, card.Root, "Message", "", 22f, _blocked, Width / 2f, 434f, Width - 24f, HorizontalAlignmentOptions.Center);

            DrawBadge(card.Root, PromptControl.South, 16f, 452f, 34f);
            UiKit.TextAt(_style.Font, card.Root, "Buy", Tr.Get("buy"), 24f, _style.TextColor, 56f, 478f, 110f);
            DrawBadge(card.Root, PromptControl.East, Width - 112f, 452f, 34f);
            UiKit.TextAt(_style.Font, card.Root, "Close", Tr.Get("close"), 24f, _style.TextColor, Width - 72f, 478f, 70f);
            return card;
        }

        private void DrawBadge(RectTransform parent, PromptControl control, float x, float y, float size)
        {
            UiKit.Box(_style, parent, "Badge", _style.BadgeColor(control, true), x, y, size, size, circle: true);
            UiKit.TextAt(_style.DisplayFont, parent, "Glyph", HudStyle.Glyph(control, true), size * 0.58f,
                Color.white, x + size / 2f, y + size * 0.7f, size, HorizontalAlignmentOptions.Center);
        }

        private void DrawShoulder(RectTransform parent, string label, float x, float y)
        {
            UiKit.Box(_style, parent, label, _style.BadgeColor(PromptControl.Triggers, true), x, y, ShoulderWidth, 34f);
            UiKit.TextAt(_style.DisplayFont, parent, label + "Glyph", label, 20f, Color.white, x + ShoulderWidth / 2f, y + 24f, ShoulderWidth, HorizontalAlignmentOptions.Center);
        }

        // ---- Each frame ---------------------------------------------------------------------------

        private void Fill(Card card, LayoutEditor.Cursor cursor)
        {
            var sections = _editor.ShopSections;
            if (!card.WasOpen)
            {
                card.Open = 0f;
                card.Index = -1;
                card.Section = cursor.ShopSection;
                card.Presses = cursor.Presses;
            }
            card.WasOpen = true;
            if (cursor.ShopSection != card.Section)
            {
                card.Section = cursor.ShopSection;
                card.Open = 0.5f; // a quick pop on turning the page
                card.Index = -1;
            }

            // The page's name between the bumpers, and a dot per page in the player's colour.
            card.Title.text = SectionName(cursor.ShopSection);
            card.Turn.SetActive(sections.Count > 1);
            while (card.Pages.Count < sections.Count)
                card.Pages.Add(UiKit.Ellipse(card.Root, "PageDot", Color.white, 0f, 54f, 12f, 12f));
            for (var i = 0; i < card.Pages.Count; i++)
            {
                var shown = i < sections.Count && sections.Count > 1;
                card.Pages[i].gameObject.SetActive(shown);
                if (!shown)
                    continue;
                card.Pages[i].rectTransform.anchoredPosition = new Vector2(Width / 2f - (sections.Count - 1) * 10f + i * 20f - 6f, -54f);
                card.Pages[i].color = sections[i] == cursor.ShopSection ? card.Colour : Dim(0.25f);
            }
            card.Balance.text = Tr.Format("{amount} to spend", ("amount", Money(_root.Services.Days.Balance)));

            var page = _editor.ShopPage(cursor);
            if (cursor.ShopIndex != card.Index)
            {
                if (card.Index >= 0)
                    card.RowPop = 0f;
                card.Index = cursor.ShopIndex;
            }
            var first = page.Count <= Rows ? 0 : Mathf.Clamp(cursor.ShopIndex - Rows / 2, 0, page.Count - Rows);
            for (var i = 0; i < Rows; i++)
            {
                var row = card.Rows[i];
                var item = first + i;
                var shown = item < page.Count;
                row.Name.gameObject.SetActive(shown);
                row.Price.gameObject.SetActive(shown);
                row.Highlight.gameObject.SetActive(false);
                if (!shown)
                    continue;
                var entry = page[item];
                row.Name.text = Tr.Get(entry.DisplayName);
                row.Price.text = Money(entry.Price);
                Choose(card, row, item == cursor.ShopIndex, _editor.CanAfford(entry) ? _style.TextColor : _blocked);
            }
            card.Count.text = page.Count > Rows ? $"{cursor.ShopIndex + 1} / {page.Count}" : "";

            var chosen = cursor.ShopIndex >= 0 && cursor.ShopIndex < page.Count ? page[cursor.ShopIndex] : null;
            card.Arrives.text = chosen == null ? "" : chosen.IsItem
                ? Tr.Get("Paid now, and put on a free counter.")
                : Tr.Get("Carry it where it goes: paid when it's put down.");

            card.Message.text = cursor.Result switch
            {
                Placement.CantAfford => Tr.Get("not enough money"),
                Placement.NoFreeCounter => Tr.Get("no free counter for it"),
                _ => "",
            };

            // Each A press answers: a flash when it went through, a shake when it didn't.
            if (cursor.Presses != card.Presses)
            {
                card.Presses = cursor.Presses;
                if (cursor.Result == Placement.CantAfford || cursor.Result == Placement.NoFreeCounter)
                    card.Shake = 0f;
                else
                    card.Flash = 0f;
            }
        }

        // The chosen row in the player's colour, its text white so it reads on any colour.
        private void Choose(Card card, Row row, bool chosen, Color colour)
        {
            row.Highlight.gameObject.SetActive(chosen);
            row.Highlight.color = new Color(card.Colour.r, card.Colour.g, card.Colour.b, 0.45f);
            row.Name.color = chosen ? Color.white : colour;
            row.Price.color = chosen && colour == _style.TextColor ? Color.white : colour;
            row.Name.rectTransform.localScale = Vector3.one * (chosen ? 1f + 0.08f * Bump(card.RowPop) : 1f);
            // A refusal shakes the chosen row.
            var shake = chosen && card.Shake < 1f ? Mathf.Sin(card.Shake * Mathf.PI * 6f) * 8f * (1f - card.Shake) : 0f;
            row.Highlight.rectTransform.anchoredPosition = new Vector2(10f + shake, row.Highlight.rectTransform.anchoredPosition.y);
        }

        private void Animate(Card card, float dt)
        {
            card.Open = Mathf.Min(1f, card.Open + dt / OpenTime);
            card.RowPop = Mathf.Min(1f, card.RowPop + dt / PopTime);
            card.Shake = Mathf.Min(1f, card.Shake + dt / ShakeTime);
            card.Flash = Mathf.Min(1f, card.Flash + dt / FlashTime);
            card.Root.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, 1f - (1f - card.Open) * (1f - card.Open));
            if (card.Edge != null)
                card.Edge.color = Color.Lerp(Color.white, card.Colour, card.Flash);
        }

        private static float Bump(float t) => t >= 1f ? 0f : Mathf.Sin(Mathf.PI * Mathf.Sqrt(t));

        private float CanvasWidth()
        {
            var width = ((RectTransform)_canvas.transform).rect.width;
            return width < 1f ? 1920f : width;
        }

        private Color Dim(float alpha) => new Color(_style.TextColor.r, _style.TextColor.g, _style.TextColor.b, alpha);

        private static string SectionName(ShopSection section) => section switch
        {
            ShopSection.Appliances => Tr.Get("Appliances"),
            ShopSection.Kitchenware => Tr.Get("Kitchenware"),
            ShopSection.Decoration => Tr.Get("Decoration"),
            _ => Tr.Get("Furniture"),
        };

        private static string Money(float amount) => Locale.Money(amount, MoneyStyle.Whole);
    }
}
