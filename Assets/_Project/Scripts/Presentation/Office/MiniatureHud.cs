using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Data;
using Game.Gameplay.Office;
using Game.Gameplay.Stations;
using Game.Presentation.Hud;
using Game.Simulation.Campaign;
using Game.Simulation.Saving;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The city miniature's screens (Phase 6, milestone 2; the user's calls, 2026-11-10). One shared
    /// panel at the screen's right edge, inset from it, so the table and the chefs round it stay in
    /// view and it reads the same with one player or four:
    /// <list type="bullet">
    /// <item>At the table, before anyone looks: a hint, bottom centre, "A Look at the city".</item>
    /// <item>Browsing: the highlighted lot's card: name, street, size and seats, price or "Yours",
    /// previous style, old reviews, what it comes with, and what buying does to the money.</item>
    /// <item>Naming: the old name, suggestions and the letter grid, with the typed name.</item>
    /// <item>Styling: the previous style or one of the offered styles.</item>
    /// </list>
    /// Coloured dots show who is looking and who is buying. Hints name only Xbox buttons.
    /// </summary>
    public sealed class MiniatureHud : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.16f, 0.11f, 0.09f);
        private static readonly Color Cream = new Color(0.95f, 0.91f, 0.82f);
        private static readonly Color Dim = new Color(0.95f, 0.91f, 0.82f, 0.62f);
        private static readonly Color Gold = new Color(0.93f, 0.74f, 0.33f);
        private static readonly Color Owned = new Color(0.42f, 0.68f, 0.29f);
        private static readonly Color Debt = new Color(0.94f, 0.45f, 0.36f);
        private static readonly Color Key = new Color(0.95f, 0.91f, 0.82f, 0.1f);

        // The panel: inset from the right edge and clear of the join slots along the bottom.
        private const float Edge = 48f;
        private const float Top = 64f;
        private const float Bottom = 900f;
        private const float CardWidth = 600f;
        private const float GridWidth = 680f;
        private const float Pad = 28f;
        private const int MaxReviews = 2; // three overflow the card for the largest lots

        [SerializeField] private HudStyle _style;
        [SerializeField] private MiniatureTable _table;
        [Tooltip("Player colours, for the dots of who's looking and who's buying.")]
        [SerializeField] private CoopSettings _coop;
        [Tooltip("Names for the appliances a lot comes with.")]
        [SerializeField] private FurnitureCatalog _furniture;
        [Tooltip("How long \"It's yours\" shows after buying (s).")]
        [SerializeField, Min(0f)] private float _toastSeconds = 2.5f;

        private RectTransform _layout;
        private RectTransform _hint;
        private RectTransform _panel;
        private string _shown;
        private string _toast;
        private float _toastUntil;

        private void Awake()
        {
            var canvasObject = new GameObject("MiniatureCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // the whole 1920 × 1080 always fits
            _layout = UiKit.Rect(canvasObject.transform, "Layout", 0f, 0f, 1920f, 1080f);
            _layout.anchorMin = _layout.anchorMax = _layout.pivot = new Vector2(0.5f, 0.5f);
            _layout.anchoredPosition = Vector2.zero;
            _hint = UiKit.Rect(_layout, "Hint", 0f, 0f, 1920f, 1080f);
            _panel = UiKit.Rect(_layout, "Panel", 0f, 0f, 1920f, 1080f);
        }

        private void OnEnable()
        {
            if (_table != null && _table.Campaign != null)
                _table.Campaign.Bought += ShowBought;
        }

        private void OnDisable()
        {
            if (_table != null && _table.Campaign != null)
                _table.Campaign.Bought -= ShowBought;
        }

        // The day opening it plays: the one after its last finished day (day 1 if never opened).
        private static int NextDay(RestaurantSave record) =>
            Game.Simulation.Campaign.RestaurantPurchase.NeverOpened(record) ? 1 : record.FinishedDay + 1;

        private void ShowBought(RestaurantSave record)
        {
            _toast = Tr.Format("{name} is yours", ("name", record.Name));
            _toastUntil = Time.unscaledTime + _toastSeconds;
        }

        private void Update()
        {
            if (_table == null)
                return;
            if (_toast != null && Time.unscaledTime > _toastUntil)
                _toast = null;
            var state = State();
            if (state == _shown)
                return;
            _shown = state;
            UiKit.Clear(_hint);
            UiKit.Clear(_panel);
            DrawHint();
            switch (_table.Current)
            {
                case MiniatureTable.Step.Browsing:
                    DrawCard();
                    break;
                case MiniatureTable.Step.Naming:
                    DrawNaming();
                    break;
                case MiniatureTable.Step.Styling:
                    DrawStyling();
                    break;
            }
        }

        // Everything the drawing depends on, so it's redrawn only when something changed.
        private string State()
        {
            var text = new StringBuilder();
            text.Append(_table.Current).Append('|').Append(_table.Selected).Append('|').Append(_table.AtTable).Append('/').Append(_table.Joined)
                .Append('|').Append(string.Join(",", _table.Lookers.Select(p => p.playerIndex))).Append('|').Append(_toast);
            if (_table.Current == MiniatureTable.Step.Closed || _table.Campaign == null)
                return text.ToString();
            var campaign = _table.Campaign.Campaign;
            text.Append('|').Append(campaign.Balance).Append('|').Append(campaign.Restaurants.Count);
            var picker = _table.Picker;
            if (picker != null)
                text.Append('|').Append(picker.Text).Append('|').Append(picker.Row).Append(',').Append(picker.Column)
                    .Append('|').Append(string.Join(",", picker.Rows[1].Select(k => k.Text)));
            if (_table.Current == MiniatureTable.Step.Styling)
                text.Append('|').Append(_table.StyleChoice);
            return text.ToString();
        }

        // ---- The hint, bottom centre ----

        private void DrawHint()
        {
            var someoneOut = _table.AtTable > _table.Lookers.Count;
            if (_table.AtTable == 0 || !someoneOut)
                return;
            const float width = 520f;
            var card = UiKit.Rect(_hint, "Card", (1920f - width) / 2f, 830f, width, 76f);
            UiKit.RoundBox(card, "Panel", new Color(Ink.r, Ink.g, Ink.b, 0.88f), 0f, 0f, width, 76f, 38f);
            Badge(card, PromptControl.South, 14f, 12f, 52f);
            UiKit.TextAt(_style.Bold, card, "Label", Tr.Get("Look at the city"), 32f, Cream, 80f, 50f, width - 100f);
        }

        // ---- Browsing: the lot's card ----

        private void DrawCard()
        {
            var lots = _table.Campaign.Lots;
            if (_table.Selected < 0 || _table.Selected >= lots.Count)
                return;
            var lot = lots[_table.Selected];
            var record = _table.Campaign.Owned(_table.Selected);
            var campaign = _table.Campaign.Campaign;
            var inner = CardWidth - Pad * 2f;
            var card = Panel(CardWidth, out var background);
            var y = Pad;

            // Who's looking, and whether it's theirs.
            Dots(card, _table.Lookers.Select(p => p.playerIndex), Pad, y + 4f);
            // Owned, or its price: the first thing to read on a lot for sale.
            Chip(card, record != null ? Tr.Get("Yours") : Tr.Format("For sale · {price}", ("price", Money(lot.Price))), record != null ? Owned : Gold, CardWidth - Pad, y);
            y += 44f;

            y = Line(card, _style.ExtraBold, record != null ? record.Name : lot.Name, 40f, Cream, Pad, y, inner);
            var place = Tr.Format("{street} · {size} · {seats} seats", ("street", lot.Street), ("size", SizeName(lot.Size)), ("seats", lot.Seats));
            y = Line(card, _style.Medium, place, 24f, Dim, Pad, y + 2f, inner) + 14f;

            if (record != null)
            {
                // Its day, as its last summary showed it (2026-11-11): the last finished day.
                var opened = !Game.Simulation.Campaign.RestaurantPurchase.NeverOpened(record) && record.FinishedDay > 0;
                var day = opened
                    ? Tr.Format("Day {day} · {weekday}", ("day", record.FinishedDay), ("weekday", Game.Presentation.Hud.DayCardText.Weekday(record.FinishedDay)))
                    : Tr.Get("Not opened yet");
                y = Line(card, _style.Bold, day, 26f, Gold, Pad, y, inner);
                y = Line(card, _style.SemiBold, Tr.Format("Style: {style}", ("style", record.StyleName)), 26f, Cream, Pad, y + 2f, inner);
                y = Line(card, _style.Medium, Tr.Plural(record.Reviews.Count, "{count} review", "{count} reviews", ("count", record.Reviews.Count)), 22f, Dim, Pad, y + 2f, inner) + 12f;
                y = Reviews(card, record.Reviews.OrderByDescending(r => r.Day).Take(MaxReviews).Select(r => (r.Stars, r.Line, r.Guest)), y, inner);
            }
            else
            {
                y = Line(card, _style.SemiBold, Tr.Format("Previously: {style}", ("style", lot.PreviousStyle)), 26f, Cream, Pad, y, inner);
                if (lot.FixedStyle)
                    y = Line(card, _style.Medium, Tr.Get("Its style stays: you choose its name"), 22f, Dim, Pad, y + 2f, inner);
                y += 12f;
                if (lot.OldReviews.Count > 0)
                {
                    y = Line(card, _style.Bold, Tr.Get("Old reviews, kept if you keep the name"), 22f, Gold, Pad, y, inner) + 6f;
                    y = Reviews(card, lot.OldReviews.Take(MaxReviews).Select(r => (r.Stars, r.Line, r.Guest)), y, inner);
                }
            }

            y = Line(card, _style.Bold, Tr.Get("Comes with"), 22f, Gold, Pad, y + 4f, inner);
            y = Line(card, _style.Medium, Appliances(lot.InheritedAppliances), 22f, Cream, Pad, y + 2f, inner, lines: 3) + 14f;

            // What buying does to the money: it may go below zero, which is owed to the boss.
            y = Line(card, _style.SemiBold, Tr.Format("Your money: {amount}", ("amount", Money(campaign.Balance))), 24f, campaign.Balance < 0f ? Debt : Cream, Pad, y, inner);
            if (record == null && campaign.Balance - lot.Price < 0f)
                y = Line(card, _style.SemiBold, Tr.Format("Buying it leaves {amount} owed to the boss", ("amount", Money(lot.Price - campaign.Balance))), 22f, Debt, Pad, y + 2f, inner, lines: 2);
            if (_toast != null)
                y = Line(card, _style.Bold, _toast, 26f, Owned, Pad, y + 8f, inner);
            y += 18f;

            // What A does: open the restaurant once everyone is here, or buy.
            var x = Pad;
            if (record == null)
                x = Hint(card, PromptControl.South, Tr.Get("Buy"), x, y);
            else if (_table.Ready)
                x = Hint(card, PromptControl.South, Tr.Format("Open for day {day}", ("day", NextDay(record))), x, y);
            else
                x = Waiting(card, x, y);
            Hint(card, PromptControl.East, Tr.Get("Back"), x + 24f, y);
            Finish(card, background, CardWidth, y + 44f + Pad);
        }

        private float Reviews(RectTransform card, IEnumerable<(float Stars, string Line, string Guest)> reviews, float y, float width)
        {
            foreach (var (stars, line, guest) in reviews)
            {
                UiKit.StarRow(card, Pad, y, 20f, 23f, stars, Gold, new Color(Cream.r, Cream.g, Cream.b, 0.22f));
                UiKit.TextAt(_style.Medium, card, "Guest", guest, 18f, Dim, Pad + 128f, y + 17f, width - 128f);
                y = Line(card, _style.Medium, $"“{line}”", 22f, Cream, Pad, y + 26f, width, lines: 2) + 10f;
            }
            return y;
        }

        // "2 × Gas hob, Cutting board, …" in the catalogue's names.
        private string Appliances(IReadOnlyList<string> ids)
        {
            var parts = ids.GroupBy(id => id).Select(group =>
            {
                var entry = _furniture != null ? _furniture.Find(group.Key) : null;
                var name = Tr.Get(entry != null ? entry.DisplayName : group.Key);
                return group.Count() > 1 ? $"{group.Count()} × {name}" : name;
            });
            var list = string.Join(", ", parts);
            return list.Length > 0 ? list : Tr.Get("Nothing: an empty room");
        }

        // While someone isn't looking at the city yet, A can't open: how many are.
        private float Waiting(RectTransform card, float x, float y)
        {
            var label = UiKit.TextAt(_style.Bold, card, "Waiting", Tr.Format("Everyone to the table: {here} of {all}", ("here", _table.Lookers.Count), ("all", _table.Joined)), 24f, Gold, x, y + 30f, 420f);
            return x + Mathf.Min(420f, UiKit.Width(label));
        }

        // ---- Naming: the old name, suggestions and the letter grid ----

        private void DrawNaming()
        {
            var picker = _table.Picker;
            if (picker == null)
                return;
            var lot = _table.Campaign.Lots[_table.Selected];
            var inner = GridWidth - Pad * 2f;
            var card = Panel(GridWidth, out var background);
            var y = Pad;
            var buyer = _table.Buyer != null ? _table.Buyer.playerIndex : 0;

            Dots(card, new[] { buyer }, Pad, y + 8f);
            y = Line(card, _style.ExtraBold, Tr.Get("Name your restaurant"), 34f, Cream, Pad + 34f, y, inner - 34f);
            y = Line(card, _style.Medium, Tr.Format("{old} · {street}", ("old", lot.Name), ("street", lot.Street)), 22f, Dim, Pad, y + 2f, inner) + 14f;

            // The name so far, with how much room is left.
            UiKit.RoundBox(card, "Field", new Color(Cream.r, Cream.g, Cream.b, 0.14f), Pad, y, inner, 64f, 14f);
            var typed = picker.Text.Length > 0 ? picker.Text + "<color=#EDBD54>|</color>" : $"<color=#{ColorUtility.ToHtmlStringRGBA(Dim)}>{Tr.Get("Pick a name, or type one")}</color>";
            UiKit.TextAt(_style.Bold, card, "Typed", typed, 32f, Cream, Pad + 18f, y + 43f, inner - 110f);
            UiKit.TextAt(_style.Medium, card, "Count", $"{picker.Text.Length}/{picker.MaxLength}", 20f, Dim, Pad + inner - 16f, y + 40f, 90f, HorizontalAlignmentOptions.Right);
            y += 80f;

            var colour = PlayerColour(buyer);
            for (var r = 0; r < picker.Rows.Count; r++)
            {
                var row = picker.Rows[r];
                if (row.Count == 0)
                    continue;
                if (r == 0)
                    y = Line(card, _style.Bold, Tr.Get("Keep the old name and its reviews"), 20f, Gold, Pad, y, inner) + 4f;
                else if (r == 1)
                    y = Line(card, _style.Bold, Tr.Get("A new name starts with no reviews"), 20f, Gold, Pad, y, inner) + 4f;
                y = KeyRow(card, row, r, picker, colour, y, inner) + (r <= 1 ? 14f : 8f);
            }
            y += 10f;

            var x = Hint(card, PromptControl.South, Tr.Get("Choose"), Pad, y);
            x = Hint(card, PromptControl.West, Tr.Get("Delete"), x + 20f, y);
            x = Hint(card, PromptControl.North, Tr.Get("New names"), x + 20f, y);
            Hint(card, PromptControl.East, Tr.Get("Back"), x + 20f, y);
            Finish(card, background, GridWidth, y + 44f + Pad);
        }

        // One row of keys, spread across the panel; names as wide chips, letters as squares.
        private float KeyRow(RectTransform card, IReadOnlyList<NameKey> row, int r, NamePicker picker, Color colour, float y, float width)
        {
            const float gap = 8f;
            var letters = row[0].Kind == NameKeyKind.Letter;
            var height = letters ? 54f : 50f;
            var keyWidth = letters ? (width - gap * (NamePicker.LetterRows[0].Length - 1)) / NamePicker.LetterRows[0].Length : (width - gap * (row.Count - 1)) / row.Count;
            if (row[0].Kind == NameKeyKind.Keep)
                keyWidth = width;
            for (var c = 0; c < row.Count; c++)
            {
                var key = row[c];
                var selected = picker.Row == r && picker.Column == c;
                var x = Pad + c * (keyWidth + gap);
                UiKit.RoundBox(card, "Key", selected ? colour : Key, x, y, keyWidth, height, 12f);
                var ink = selected ? Readable(colour) : Cream;
                var size = letters ? 30f : key.Kind == NameKeyKind.Space || key.Kind == NameKeyKind.Delete || key.Kind == NameKeyKind.Done ? 24f : 22f;
                UiKit.TextAt(_style.Bold, card, "Label", KeyLabel(key), size, ink, x + keyWidth / 2f, y + height / 2f + size * 0.36f, keyWidth - 12f, HorizontalAlignmentOptions.Center);
            }
            return y + height;
        }

        private static string KeyLabel(NameKey key) => key.Kind switch
        {
            NameKeyKind.Space => Tr.Get("Space"),
            NameKeyKind.Delete => Tr.Get("Delete"),
            NameKeyKind.Done => Tr.Get("Done"),
            _ => key.Text,
        };

        // ---- Styling ----

        private void DrawStyling()
        {
            var lot = _table.Campaign.Lots[_table.Selected];
            var inner = CardWidth - Pad * 2f;
            var card = Panel(CardWidth, out var background);
            var y = Pad;
            var buyer = _table.Buyer != null ? _table.Buyer.playerIndex : 0;
            Dots(card, new[] { buyer }, Pad, y + 8f);
            y = Line(card, _style.ExtraBold, Tr.Get("Choose a style"), 34f, Cream, Pad + 34f, y, inner - 34f);
            y = Line(card, _style.Medium, _table.ChosenName ?? lot.Name, 24f, Dim, Pad, y + 2f, inner) + 16f;

            var colour = PlayerColour(buyer);
            for (var i = 0; i < _table.StyleOptions.Count; i++)
            {
                var selected = i == _table.StyleChoice;
                UiKit.RoundBox(card, "Option", selected ? colour : Key, Pad, y, inner, 56f, 14f);
                var label = i == 0 ? Tr.Format("Keep: {style}", ("style", _table.StyleOptions[i])) : _table.StyleOptions[i];
                UiKit.TextAt(_style.Bold, card, "Label", label, 26f, selected ? Readable(colour) : Cream, Pad + 20f, y + 37f, inner - 40f);
                y += 64f;
            }
            y += 4f;
            y = Line(card, _style.Medium, Tr.Get("Styles change who comes and what they expect (coming later)"), 20f, Dim, Pad, y, inner, lines: 2) + 16f;
            var x = Hint(card, PromptControl.South, Tr.Format("Buy for {price}", ("price", Money(lot.Price))), Pad, y);
            Hint(card, PromptControl.East, Tr.Get("Back"), x + 24f, y);
            Finish(card, background, CardWidth, y + 44f + Pad);
        }

        // ---- Pieces ----

        // A panel at the right edge; its background is sized once its content is laid out.
        private RectTransform Panel(float width, out Image background)
        {
            var card = UiKit.Rect(_panel, "Card", 1920f - Edge - width, Top, width, Bottom - Top);
            background = UiKit.RoundBox(card, "Background", new Color(Ink.r, Ink.g, Ink.b, 0.92f), 0f, 0f, width, Bottom - Top, 28f);
            return card;
        }

        // Never taller than the space above the join slots: the text was laid out to fit it.
        private static void Finish(RectTransform card, Image background, float width, float height)
        {
            height = Mathf.Min(height, Bottom - Top);
            background.rectTransform.sizeDelta = new Vector2(width, height);
            card.sizeDelta = new Vector2(width, height);
        }

        // Text from the top at y, wrapping up to `lines`; returns the y below it.
        private float Line(RectTransform parent, TMP_FontAsset font, string text, float size, Color color, float x, float y, float width, int lines = 1)
        {
            var label = UiKit.TextAt(font, parent, "Text", text, size, color, x, y + size, width, HorizontalAlignmentOptions.Left, lines);
            var height = lines > 1 ? Mathf.Min(label.GetPreferredValues(text, width, 0f).y, size * 1.25f * lines) : size * 1.25f;
            return y + Mathf.Max(size * 1.25f, height);
        }

        private float Hint(RectTransform parent, PromptControl control, string label, float x, float y)
        {
            Badge(parent, control, x, y, 44f);
            var text = UiKit.TextAt(_style.Bold, parent, "Hint", label, 24f, Cream, x + 54f, y + 31f, 360f);
            return x + 54f + Mathf.Min(360f, UiKit.Width(text));
        }

        // An Xbox face button, in its colour (hints name only Xbox buttons: 2026-11-04).
        private void Badge(Transform parent, PromptControl control, float x, float y, float size)
        {
            var badge = UiKit.Rect(parent, "Badge", x, y, size, size);
            UiKit.Ellipse(badge, "Disc", _style.BadgeColor(control, true), 0f, 0f, size, size);
            UiKit.TextAt(_style.Bold, badge, "Letter", HudStyle.Glyph(control, true), size * 0.6f, Color.white, size / 2f, size * 0.72f, size, HorizontalAlignmentOptions.Center);
        }

        private void Chip(RectTransform parent, string text, Color color, float right, float y)
        {
            var label = UiKit.TextAt(_style.Bold, parent, "ChipText", text, 22f, Ink, 0f, y + 26f, 300f);
            var width = Mathf.Min(300f, UiKit.Width(label)) + 32f;
            var box = UiKit.RoundBox(parent, "Chip", color, right - width, y, width, 36f, 18f);
            box.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
            label.rectTransform.anchoredPosition = new Vector2(right - width + 16f, label.rectTransform.anchoredPosition.y);
        }

        private void Dots(RectTransform parent, IEnumerable<int> players, float x, float y)
        {
            foreach (var player in players)
            {
                UiKit.StrokedCircle(parent, "Dot", PlayerColour(player), Cream, 2f, x, y, 22f);
                x += 28f;
            }
        }

        private Color PlayerColour(int player) => _coop != null ? _coop.GetPlayerColor(player) : Gold;

        // Ink or cream, whichever reads on that fill.
        private static Color Readable(Color fill) => fill.grayscale > 0.55f ? Ink : Color.white;

        private static string SizeName(RestaurantSize size) => size switch
        {
            RestaurantSize.Counter => Tr.In("restaurant size", "Counter"),
            RestaurantSize.Small => Tr.In("restaurant size", "Small room"),
            RestaurantSize.DiningRoom => Tr.In("restaurant size", "Dining room"),
            _ => Tr.In("restaurant size", "Large dining room"),
        };

        private static string Money(float amount) => Locale.Money(amount, MoneyStyle.Whole);
    }
}
