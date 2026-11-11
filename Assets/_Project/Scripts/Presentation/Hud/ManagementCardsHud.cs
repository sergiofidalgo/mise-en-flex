using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Simulation.Days;
using Game.Simulation.Furniture;
using Game.Simulation.Movement;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The kitchen's player cards, top left (the user's design, 2026-11-11): in Prep, one compact
    /// card per joined player, as in the office (a still of their chef's face and their number; no
    /// open seats here). A player who opens the shop in management mode (Y) has their card grow
    /// into the shop card, so four can shop at once; cards dock in a row in player-slot order and
    /// slide along as others grow and shrink.
    /// <list type="bullet">
    /// <item>The shop card is a grid of the page's pieces as baked pictures with their prices
    /// (Tools/Game/Bake Shop Pictures), three to a row, the chosen one ringed in the player's
    /// colour and anything out of reach dimmed with its price in red. The D-pad moves through it.</item>
    /// <item>Under the grid, the chosen piece's name and price and what it's for (two lines).</item>
    /// <item>The page (furniture, appliances, kitchenware) sits between LB and RB with a dot per
    /// page, as on the tailor's dressing card.</item>
    /// <item>Every press answers at once: a new choice pops, a purchase flashes the card's edge, and
    /// a refusal shakes the tile with the reason in red.</item>
    /// </list>
    /// Only Xbox buttons are named, and every card always stays whole on screen.
    /// </summary>
    public sealed class ManagementCardsHud : MonoBehaviour
    {
        private const float Width = 360f;
        private const float Height = 500f;
        private const float CompactWidth = 210f; // the office's
        private const float CompactHeight = 108f;
        private const float NumberSize = 46f;
        private const float Margin = 24f;
        private const float CardGap = 12f;
        private const float ShoulderWidth = 50f;
        private const float GridTop = 74f;
        private const float GridPad = 16f;
        private const float TileGap = 10f;
        private const float TileHeight = 110f;
        private const float PictureHeight = 76f;
        private const int GridRows = 2;
        private const float PopTime = 0.16f;
        private const float ShakeTime = 0.3f;
        private const float FlashTime = 0.35f;
        private const float OpenTime = 0.12f;
        private const float SettleDelay = 0.5f;
        private const float StillBackdropTint = 0.22f;
        private const float TabHeight = 50f;     // what shows of the rearranging tab
        private const float TabTuck = 4f;        // its top edge lies on the card's bottom edge (the card is see-through)
        private const float ChevronPeriod = 1.2f;
        private const float ChevronBob = 4f;

        [SerializeField] private HudStyle _style;
        [SerializeField] private CoopSettings _coop;
        [SerializeField] private LayoutEditor _editor;
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private Color _blocked = new Color(0.95f, 0.30f, 0.26f);

        private readonly Dictionary<int, Card> _cards = new Dictionary<int, Card>();
        private readonly Dictionary<int, Compact> _compacts = new Dictionary<int, Compact>();
        private readonly List<(int Player, Transform Chef, View Shop)> _row = new List<(int, Transform, View)>();
        private readonly HashSet<int> _rearranging = new HashSet<int>();
        private Canvas _canvas;

        /// <summary>What a shop card shows: the page, the choice, and how the last press went.</summary>
        public sealed class View
        {
            public ShopSection Section;
            public IReadOnlyList<ShopSection> Sections;
            public IReadOnlyList<FurnitureCatalog.Entry> Page;
            public int Index;
            public Placement Result;
            public int Presses;
            public Func<FurnitureCatalog.Entry, bool> CanAfford;
        }

        private sealed class Tile
        {
            public RectTransform Root;
            public Image Back;
            public Image Ring;
            public Image Picture;
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
            public Tile[] Tiles;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Price;
            public TextMeshProUGUI Description;
            public TextMeshProUGUI Message;

            // What was shown last frame, to notice what changed.
            public bool WasOpen;
            public ShopSection Section;
            public int Index = -1;
            public int Presses;
            public float Open = 1f;     // 0 just opened, 1 settled
            public float Pop = 1f;
            public float Shake = 1f;
            public float Flash = 1f;
        }

        private sealed class Compact
        {
            public RectTransform Root;
            public RectTransform Tab;       // rearranging: Y drops the shop open
            public RectTransform Chevron;
            public ChefStill Still;
            public Transform Seen;      // the chef last frame, to notice a join
            public float SettleAt = -1f;
        }

        private void Awake()
        {
            if (_canvas != null)
                return;
            // On the kitchen HUD's canvas this is a sub-canvas: it fills the HUD and takes its
            // scaling, drawn over the world labels and prompts. On its own it scales itself.
            var canvasObject = new GameObject("PlayerCards", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.layer = gameObject.layer; // the HUD's (UI) layer, as a camera drawing it expects
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

        // The same chef in every scene: each card photographs the player's own chef, who wears
        // their saved look (ChefWardrobe), and takes the picture again should that look change.
        private void OnEnable() => Game.Gameplay.Saving.ChefProfileFile.LookChanged += HandleLookChanged;

        private void OnDisable() => Game.Gameplay.Saving.ChefProfileFile.LookChanged -= HandleLookChanged;

        private void HandleLookChanged(int slot)
        {
            if (_compacts.TryGetValue(slot, out var compact))
                compact.Seen = null;
        }

        private void OnDestroy()
        {
            foreach (var compact in _compacts.Values)
                compact.Still?.Release();
        }

        /// <summary>True while this player's shop shows as a card (the cursor's label then stays quiet).</summary>
        public bool Shows(LayoutEditor.Cursor cursor) => isActiveAndEnabled && cursor.ShopOpen;

        private void LateUpdate()
        {
            _row.Clear();
            _rearranging.Clear();
            var services = _root != null ? _root.Services : null;
            var prep = services != null && services.Days != null && services.Days.Phase == DayPhase.Prep && !services.Forkcast.IsOpen;
            foreach (var player in PlayerInput.all.Where(p => !PlayerPresence.IsLost(p)).OrderBy(p => p.playerIndex))
            {
                var cursor = _editor != null ? _editor.Cursors.FirstOrDefault(c => c.Player == player && c.ShopOpen) : null;
                // The shop whenever it's open; the compact card only in Prep.
                if (cursor != null)
                    _row.Add((player.playerIndex, player.transform, ViewOf(cursor)));
                else if (prep)
                {
                    _row.Add((player.playerIndex, player.transform, null));
                    if (player.TryGetComponent(out PlayerInteractor hands) && hands.IsManaging)
                        _rearranging.Add(player.playerIndex);
                }
            }
            Lay(Time.unscaledDeltaTime, live: true);
        }

        private View ViewOf(LayoutEditor.Cursor cursor) => new View
        {
            Section = cursor.ShopSection,
            Sections = _editor.ShopSections.ToList(),
            Page = _editor.ShopPage(cursor),
            Index = cursor.ShopIndex,
            Result = cursor.Result,
            Presses = cursor.Presses,
            CanAfford = _editor.CanAfford,
        };

        // The row: each player's card as it stands, laid left to right from the corner.
        private void Lay(float dt, bool live)
        {
            _canvas.gameObject.layer = gameObject.layer; // follows the HUD's, should it move
            foreach (var card in _cards.Values)
                card.Root.gameObject.SetActive(false);
            foreach (var compact in _compacts.Values)
                compact.Root.gameObject.SetActive(false);

            var x = Margin;
            foreach (var (player, chef, shop) in _row)
            {
                if (shop != null)
                {
                    if (!_cards.TryGetValue(player, out var card))
                        _cards[player] = card = Build(player);
                    card.Root.gameObject.SetActive(true);
                    Fill(card, shop);
                    Animate(card, dt);
                    // The pivot is the top middle, so it grows from the top.
                    var left = Mathf.Min(x, CanvasWidth() - Margin - Width);
                    card.Root.anchoredPosition = new Vector2(left + Width / 2f, -Margin);
                    x += Width + CardGap;
                }
                else
                {
                    if (!_compacts.TryGetValue(player, out var compact))
                        _compacts[player] = compact = BuildCompact(player);
                    compact.Root.gameObject.SetActive(true);
                    Photograph(player, compact, chef, live);
                    ShowTab(compact, _rearranging.Contains(player));
                    var left = Mathf.Min(x, CanvasWidth() - Margin - CompactWidth);
                    compact.Root.anchoredPosition = new Vector2(left, -Margin);
                    x += CompactWidth + CardGap;
                }
            }
            foreach (var card in _cards.Values)
            {
                if (!card.Root.gameObject.activeSelf)
                    card.WasOpen = false;
            }
        }

        /// <summary>
        /// Draws the row at once outside Play mode, for a still: each player's chef (photographed
        /// for their compact card), and the shop for whoever has one.
        /// </summary>
        public Canvas Preview(IReadOnlyList<(int Player, Transform Chef, View Shop)> row, IEnumerable<int> rearranging = null)
        {
            Awake();
            _row.Clear();
            _row.AddRange(row);
            _rearranging.Clear();
            if (rearranging != null)
                _rearranging.UnionWith(rearranging);
            foreach (var compact in _compacts.Values)
                compact.Seen = null; // a fresh still of everyone
            Lay(1f, live: false);
            Canvas.ForceUpdateCanvases();
            return _canvas;
        }

        /// <summary>A shop view straight from a catalog, for stills.</summary>
        public static View PreviewView(FurnitureCatalog catalog, ShopSection section, int index, float balance)
        {
            var sections = catalog.Entries.Select(e => e.Section).Distinct().OrderBy(s => s).ToList();
            return new View
            {
                Section = section,
                Sections = sections,
                Page = catalog.Entries.Where(e => e.Section == section).ToList(),
                Index = index,
                Result = Placement.Ok,
                CanAfford = e => e.Price <= balance,
            };
        }

        // ---- The compact card --------------------------------------------------------------------

        // The office's compact card: the face in a square on the left and the player's number.
        private Compact BuildCompact(int index)
        {
            var colour = PlayerColour(index);
            var compact = new Compact { Still = new ChefStill(transform, $"KitchenPortrait{index + 1}") };
            compact.Root = UiKit.Rect(_canvas.transform, $"Player{index + 1}", 0f, 0f, CompactWidth, CompactHeight);
            // Built first, so the card's edge is drawn over the tab's where they meet.
            BuildTab(compact, colour);
            UiKit.OutlinedBox(compact.Root, "Back", _style.PrepCardFill, colour, 4f, 0f, 0f, CompactWidth, CompactHeight, 18f);
            var square = CompactHeight - 24f;
            var frame = UiKit.RoundBox(compact.Root, "PortraitFrame", new Color(0f, 0f, 0f, 0.25f), 12f, 12f, square, square, 16f);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var picture = UiKit.Rect(frame.transform, "Portrait", 0f, 0f, square, square).gameObject.AddComponent<RawImage>();
            picture.texture = compact.Still.Texture;
            picture.raycastTarget = false;
            // The number bigger, centred in the column right of the face, as the office's.
            var column = 12f + square;
            UiKit.TextAt(_style.DisplayFont, compact.Root, "Number", $"P{index + 1}", NumberSize, colour, (column + CompactWidth) / 2f, CompactHeight / 2f + NumberSize * 0.36f, CompactWidth - column - 8f, HorizontalAlignmentOptions.Center);
            return compact;
        }

        // While its player rearranges the kitchen (the user's call, 2026-11-11), a tab hangs under the
        // card: the Y button, a little grid standing for the shop, "Shop" and a down-arrow that
        // gently bobs. Y drops the shop open, and the card grows into it.
        private void BuildTab(Compact compact, Color colour)
        {
            compact.Tab = UiKit.Rect(compact.Root, "ShopTab", 0f, CompactHeight - TabTuck, CompactWidth, TabTuck + TabHeight);
            UiKit.OutlinedBox(compact.Tab, "Back", _style.PrepCardFill, colour, 4f, 10f, 0f, CompactWidth - 20f, TabTuck + TabHeight, 16f);
            var cy = TabTuck + TabHeight / 2f;
            DrawBadge(compact.Tab, PromptControl.North, 22f, cy - 15f, 30f);
            // The shop: a two-by-three grid of little tiles, as the shop card lays its pieces out.
            const float cell = 7f, gap = 3f;
            var gridLeft = 62f;
            for (var i = 0; i < 6; i++)
                UiKit.RoundBox(compact.Tab, "Cell", Dim(0.85f), gridLeft + (i % 3) * (cell + gap), cy - cell - gap / 2f + (i / 3) * (cell + gap), cell, cell, 1.5f);
            UiKit.TextAt(_style.Bold, compact.Tab, "Shop", Tr.Get("Shop"), 22f, _style.TextColor, gridLeft + 3f * cell + 2f * gap + 10f, cy + 8f, 80f);
            // The down-arrow, two short bars meeting at a point, in the player's colour.
            compact.Chevron = UiKit.Rect(compact.Tab, "Chevron", CompactWidth - 46f, cy - 10f, 20f, 20f);
            compact.Chevron.pivot = new Vector2(0.5f, 0.5f);
            compact.Chevron.anchoredPosition += new Vector2(10f, -10f);
            for (var side = -1; side <= 1; side += 2)
            {
                var bar = UiKit.RoundBox(compact.Chevron, "Stroke", colour, 0f, 0f, 13f, 4f, 2f).rectTransform;
                bar.pivot = new Vector2(0.5f, 0.5f);
                bar.anchoredPosition = new Vector2(10f + side * 4f, -10f);
                bar.localRotation = Quaternion.Euler(0f, 0f, side * 45f);
            }
            compact.Tab.gameObject.SetActive(false);
        }

        private void ShowTab(Compact compact, bool rearranging)
        {
            compact.Tab.gameObject.SetActive(rearranging);
            if (!rearranging)
                return;
            // A slow bob down, pointing the way the shop opens.
            var bob = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * Time.unscaledTime / ChevronPeriod);
            compact.Chevron.anchoredPosition = new Vector2(compact.Chevron.anchoredPosition.x, -(TabTuck + TabHeight / 2f) - ChevronBob * bob);
        }

        // A still, not a live picture: taken as the player joins, and once more as they settle.
        private void Photograph(int player, Compact compact, Transform chef, bool live)
        {
            if (chef == null)
                return;
            var now = Time.unscaledTime;
            var take = false;
            if (compact.Seen != chef)
            {
                compact.Seen = chef;
                compact.SettleAt = live ? now + SettleDelay : -1f;
                take = true;
            }
            else if (compact.SettleAt >= 0f && now >= compact.SettleAt)
            {
                compact.SettleAt = -1f;
                take = true;
            }
            if (take)
                compact.Still.Take(chef, Color.Lerp(_style.CardBackground, PlayerColour(player), StillBackdropTint), live ? 1 : 2);
        }

        // ---- The shop card -----------------------------------------------------------------------

        private static float TileWidth => (Width - 2f * GridPad - (ShopGrid.Columns - 1) * TileGap) / ShopGrid.Columns;

        private Card Build(int index)
        {
            var colour = PlayerColour(index);
            var card = new Card { Colour = colour };
            card.Root = UiKit.Rect(_canvas.transform, $"Player{index + 1}Shop", 0f, 0f, Width, Height);
            card.Root.pivot = new Vector2(0.5f, 1f);
            UiKit.OutlinedBox(card.Root, "Back", _style.PrepCardFill, colour, 4f, 0f, 0f, Width, Height, 22f);
            card.Edge = card.Root.Find("BackEdge")?.GetComponent<Image>();

            var turn = UiKit.Rect(card.Root, "Turn", 0f, 0f, Width, Height);
            DrawShoulder(turn, "LB", 12f, 12f);
            DrawShoulder(turn, "RB", Width - 12f - ShoulderWidth, 12f);
            card.Turn = turn.gameObject;
            card.Title = UiKit.TextAt(_style.Bold, card.Root, "Page", "", 30f, _style.TextColor, Width / 2f, 40f, Width - 2f * (ShoulderWidth + 20f), HorizontalAlignmentOptions.Center);
            card.Title.enableAutoSizing = true;
            card.Title.fontSizeMin = 24f;
            card.Title.fontSizeMax = 30f;

            // The grid: two rows of three tiles, each a picture over its price.
            card.Tiles = new Tile[ShopGrid.Columns * GridRows];
            for (var i = 0; i < card.Tiles.Length; i++)
            {
                var x = GridPad + (i % ShopGrid.Columns) * (TileWidth + TileGap);
                var y = GridTop + (i / ShopGrid.Columns) * (TileHeight + TileGap);
                var tile = new Tile { Root = UiKit.Rect(card.Root, "Tile", x, y, TileWidth, TileHeight) };
                tile.Ring = UiKit.RoundBox(tile.Root, "Ring", colour, -3f, -3f, TileWidth + 6f, TileHeight + 6f, 15f);
                tile.Back = UiKit.RoundBox(tile.Root, "Back", Dim(0.08f), 0f, 0f, TileWidth, TileHeight, 12f);
                tile.Picture = UiKit.Picture(tile.Root, "Picture", null, 6f, 4f, TileWidth - 12f, PictureHeight);
                tile.Picture.preserveAspect = true;
                tile.Price = UiKit.TextAt(_style.ExtraBold, tile.Root, "Price", "", 21f, Gold, TileWidth / 2f, TileHeight - 9f, TileWidth - 8f, HorizontalAlignmentOptions.Center);
                card.Tiles[i] = tile;
            }

            // The chosen piece: its name and price, then what it's for.
            var below = GridTop + GridRows * TileHeight + (GridRows - 1) * TileGap;
            card.Name = UiKit.TextAt(_style.DisplayFont, card.Root, "Name", "", 30f, _style.TextColor, 20f, below + 44f, Width - 40f - 90f);
            card.Name.enableAutoSizing = true;
            card.Name.fontSizeMin = 24f;
            card.Name.fontSizeMax = 30f;
            card.Price = UiKit.TextAt(_style.DisplayFont, card.Root, "Price", "", 28f, Gold, Width - 20f, below + 44f, 90f, HorizontalAlignmentOptions.Right);
            card.Description = UiKit.TextAt(_style.Bold, card.Root, "Description", "", 21f, Dim(0.78f), 20f, below + 76f, Width - 40f, lines: 2);
            card.Message = UiKit.TextAt(_style.Bold, card.Root, "Message", "", 22f, _blocked, Width / 2f, 434f, Width - 24f, HorizontalAlignmentOptions.Center);

            DrawBadge(card.Root, PromptControl.South, 16f, 452f, 34f);
            UiKit.TextAt(_style.Font, card.Root, "Buy", Tr.Get("Buy"), 24f, _style.TextColor, 58f, 478f, 110f);
            var done = UiKit.TextAt(_style.Font, card.Root, "Done", Tr.Get("Done"), 24f, _style.TextColor, Width - 18f, 478f, 110f, HorizontalAlignmentOptions.Right);
            DrawBadge(card.Root, PromptControl.East, Width - 18f - UiKit.Width(done) - 8f - 34f, 452f, 34f);
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

        private void Fill(Card card, View view)
        {
            var sections = view.Sections;
            if (!card.WasOpen)
            {
                card.Open = 0f;
                card.Index = -1;
                card.Section = view.Section;
                card.Presses = view.Presses;
            }
            card.WasOpen = true;
            if (view.Section != card.Section)
            {
                card.Section = view.Section;
                card.Open = 0.5f; // a quick pop on turning the page
                card.Index = -1;
            }

            // The page's name between the bumpers, and a dot per page in the player's colour.
            card.Title.text = SectionName(view.Section);
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
                card.Pages[i].color = sections[i] == view.Section ? card.Colour : Dim(0.25f);
            }

            var page = view.Page;
            if (view.Index != card.Index)
            {
                if (card.Index >= 0)
                    card.Pop = 0f;
                card.Index = view.Index;
            }
            var first = ShopGrid.FirstRow(view.Index, page.Count, GridRows) * ShopGrid.Columns;
            for (var i = 0; i < card.Tiles.Length; i++)
            {
                var tile = card.Tiles[i];
                var item = first + i;
                var shown = item < page.Count;
                tile.Root.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                var entry = page[item];
                var chosen = item == view.Index;
                var affordable = view.CanAfford(entry);
                tile.Picture.sprite = entry.Picture;
                tile.Picture.enabled = entry.Picture != null;
                tile.Picture.color = new Color(1f, 1f, 1f, affordable ? 1f : 0.45f);
                tile.Price.text = Money(entry.Price);
                tile.Price.color = affordable ? Gold : _blocked;
                tile.Ring.gameObject.SetActive(chosen);
                tile.Back.color = chosen ? Color.Lerp(_style.CardBackground, card.Colour, 0.3f) : Dim(0.08f);
                // A new choice swells a little; a refusal shakes it.
                var pop = chosen ? 1f + 0.06f * Bump(card.Pop) : 1f;
                tile.Root.localScale = new Vector3(pop, pop, 1f);
                var shake = chosen && card.Shake < 1f ? Mathf.Sin(card.Shake * Mathf.PI * 6f) * 8f * (1f - card.Shake) : 0f;
                var baseX = GridPad + (i % ShopGrid.Columns) * (TileWidth + TileGap);
                tile.Root.anchoredPosition = new Vector2(baseX + shake, tile.Root.anchoredPosition.y);
            }

            var chosenEntry = view.Index >= 0 && view.Index < page.Count ? page[view.Index] : null;
            card.Name.text = chosenEntry != null ? Tr.Get(chosenEntry.DisplayName) : "";
            card.Price.text = chosenEntry != null ? Money(chosenEntry.Price) : "";
            card.Price.color = chosenEntry != null && !view.CanAfford(chosenEntry) ? _blocked : Gold;
            card.Description.text = chosenEntry != null && !string.IsNullOrEmpty(chosenEntry.Description) ? Tr.Get(chosenEntry.Description) : "";

            card.Message.text = view.Result switch
            {
                Placement.CantAfford => Tr.Get("not enough money"),
                Placement.NoFreeCounter => Tr.Get("no free counter for it"),
                _ => "",
            };

            // Each A press answers: a flash when it went through, a shake when it didn't.
            if (view.Presses != card.Presses)
            {
                card.Presses = view.Presses;
                if (view.Result == Placement.CantAfford || view.Result == Placement.NoFreeCounter)
                    card.Shake = 0f;
                else
                    card.Flash = 0f;
            }
        }

        private void Animate(Card card, float dt)
        {
            card.Open = Mathf.Min(1f, card.Open + dt / OpenTime);
            card.Pop = Mathf.Min(1f, card.Pop + dt / PopTime);
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

        // Prices in the money card's gold.
        private Color Gold => _style.MoneyEdge;

        private Color PlayerColour(int slot) => _coop != null && slot >= 0 ? _coop.GetPlayerColor(slot) : Color.white;

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
