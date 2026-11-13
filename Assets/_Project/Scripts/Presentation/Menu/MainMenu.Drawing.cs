using Game.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Gameplay.Saving;
using Game.Presentation.Hud;
using Game.Simulation.Saving;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Menu
{
    /// <summary>
    /// Drawing the main menu. Every position, size and colour here was measured from the Art
    /// Director's SVG mockups (1920×1080): text is placed by its baseline, the way the mockups set
    /// it, and font sizes are the mockup's points × 4/3. Static art (the sign, the forkcast logo,
    /// icons, polaroids, the dashed slot frame) is exported from the same SVGs as sprites.
    /// </summary>
    public sealed partial class MainMenu
    {
        // The hint for browsing: menus step with the D-pad, drawn as a cross (2026-11-10).
        private const string DPadHint = "DPad";

        [Header("Fonts (Inter and Lora, SIL Open Font License)")]
        [SerializeField] private TMP_FontAsset _medium;
        [SerializeField] private TMP_FontAsset _semiBold;
        [SerializeField] private TMP_FontAsset _bold;
        [SerializeField] private TMP_FontAsset _serifItalic;

        [Header("Art (exported from the mockup SVGs)")]
        [Tooltip("The logo and the like pill's heart, cut from the mockup's sign (blender/cut_menu_sign.py).")]
        [SerializeField] private Sprite _logo;
        [SerializeField] private Sprite _heart;
        [SerializeField] private Sprite _forkcastLogo;
        [SerializeField] private Sprite _searchIcon;
        [SerializeField] private Sprite _bellIcon;
        [SerializeField] private Sprite _polaroidFirst;
        [SerializeField] private Sprite _polaroidNext;
        [Tooltip("White, tinted in code: dashed outline of a free slot.")]
        [SerializeField] private Sprite _slotDashed;
        [SerializeField] private Sprite _slotPlus;
        [SerializeField] private Sprite _avatarEmpty;
        [SerializeField] private Sprite _cookEmpty;

        [Header("Palette (from the mockups)")]
        [SerializeField] private Color _ink = Hex(0x2b211b);
        [SerializeField] private Color _cream = Hex(0xf4e6cc);
        [SerializeField] private Color _paper = Hex(0xfff8ec);
        [Tooltip("Body text on cream.")]
        [SerializeField] private Color _brown = Hex(0x7a604b);
        [Tooltip("Labels and free-slot text.")]
        [SerializeField] private Color _tan = Hex(0x9a7650);
        [Tooltip("Quiet text on the dark panels.")]
        [SerializeField] private Color _sand = Hex(0xbfa88a);
        [SerializeField] private Color _gold = Hex(0xe2b04a);
        [SerializeField] private Color _panelEdge = Hex(0x4a3628);
        [SerializeField] private Color _well = Hex(0x3e3027);
        [SerializeField] private Color _rule = Hex(0xeadbbe);
        [SerializeField] private Color _cardEdge = Hex(0xe6d3b0);
        [SerializeField] private Color _dash = Hex(0xc9b08a);
        [SerializeField] private Color _starEmpty = Hex(0xdcc9a6);
        [SerializeField] private Color _dayBack = Hex(0xd7ebdc);
        [SerializeField] private Color _dayText = Hex(0x1f6b3a);
        [SerializeField] private Color _lastPlayedTag = Hex(0x2e8b4e);
        [SerializeField] private Color _owesBack = Hex(0xf6d2c2);
        [SerializeField] private Color _owesText = Hex(0xbf4d1f);
        [Tooltip("Xbox button colours: A, B, X, Y.")]
        [SerializeField] private Color _buttonA = Hex(0x5da83a);
        [SerializeField] private Color _buttonB = Hex(0xd9493b);
        [SerializeField] private Color _buttonX = Hex(0x3b72d9);
        [SerializeField] private Color _buttonY = Hex(0xe0b341);

        // ---- Layout, in the 1920×1080 frame of the mockups ----

        private const float ListX = 98f, ListY = 434f, ListWidth = 636f, ListHeight = 480f;
        // The sign and the list stand a little in from the screen's edge, so the scene shows either
        // side of them instead of being cut off behind them (the user's call, 2026-11-10); forkcast
        // and the pages move over to keep clear.
        private const float ColumnShift = 72f, AppShift = 40f;
        // The sign's board, as wide as the list under it.
        private const float BoardX = ListX, BoardY = 127f, BoardWidth = ListWidth, BoardHeight = 265f;
        private const float LogoWidth = 453.5f, LogoHeight = 187.5f, LogoScale = 1.08f;
        private static readonly Color SignGold = Hex(0xc9a04a);
        private static readonly Color SignBrass = Hex(0x8e7a4a);
        private static readonly Color SignLine = Hex(0x5c4632);
        private static readonly Color LikePink = Hex(0xe8476a);
        private static readonly Color RecRed = Hex(0xe86a5a);
        private const float ListTopPad = 18f, ListBottomPad = 26f, RuleGap = 14f;
        // The chosen row grows into a cream box. It's taller when there's no Continue, so the five
        // remaining rows still fill the panel (the new-player mockup).
        private const float ChosenHeight = 97f, ChosenHeightWithoutContinue = 115f;
        private const float AppX = 800f, AppY = 128f, AppWidth = 1024f, AppHeight = 788f, HeaderHeight = 72f;
        private const float CardX = 825f, CardY = 225f, CardWidth = 310f, CardHeight = 666f, CardPitch = 332f;
        private const float PhotoBottom = 478f;
        private const float FooterY = 1016f, FooterRight = 1824f;

        private const float TitleSize = 36.67f, RowSize = 34.33f, SubSize = 20f;
        private const float Stroke = 5.33f, ThinStroke = 2.67f;

        private RectTransform _canvas;
        private RectTransform _list;
        private RectTransform _app;
        private RectTransform _header;
        private RectTransform _cards;
        private RectTransform _page;
        private RectTransform _hints;
        // What moves on the sign: the REC light and the like count.
        private Image _recDot;
        private Image _recGlow;
        private RectTransform _likePill;
        private TextMeshProUGUI _likes;
        private int _likeCount = 124; // in hundreds: 12.4k
        private float _nextLike;
        private float _likePopped = -10f;
        private Texture2D[] _photos = new Texture2D[SaveSlots.Count];

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);

        private static Color Alpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        private void Redraw()
        {
            LoadPhotos();
            DrawList();
            // forkcast and its campaign cards are a second level, opened by Continue.
            _app.gameObject.SetActive(_onCards);
            DrawHeader();
            DrawCards();
            DrawPage();
            DrawHints();
        }

        // ---- The list ----

        private void DrawList()
        {
            UiKit.Clear(_list);
            var items = ((Item[])Enum.GetValues(typeof(Item))).Where(i => i != Item.Continue || _lastPlayed >= 0).ToList();
            var chosenHeight = _lastPlayed >= 0 ? ChosenHeight : ChosenHeightWithoutContinue;
            var pitch = (ListHeight - ListTopPad - chosenHeight - RuleGap - ListBottomPad) / (items.Count - 1);
            var focused = !_onCards && _openPage == Page.None;

            var y = ListY + ListTopPad;
            foreach (var item in items)
            {
                if (item == Item.Settings)
                {
                    y += RuleGap;
                    UiKit.RoundBox(_list, "Rule", _panelEdge, 144f, y, 544f, 2f, 0f);
                }
                if (item == _item)
                {
                    ChosenRow(item, y, chosenHeight, focused);
                    y += chosenHeight;
                    continue;
                }

                var colour = !IsAvailable(item) ? Alpha(_cream, _style.UnavailableOpacity) : item == Item.Quit ? _sand : _cream;
                // The baseline sits 20.5 px below the middle of the row: measured from both mockups.
                UiKit.TextAt(_bold, _list, item.ToString(), Label(item), RowSize, colour, 144f, y + pitch / 2f + 20.5f, 540f);
                y += pitch;
            }
        }

        // The row that's chosen: a cream box with a gold edge, the A button and a line saying what it does.
        // While the cards have the focus it stays, quietly, to show where Back returns to.
        private void ChosenRow(Item item, float y, float height, bool focused)
        {
            var middle = y + height / 2f;
            if (focused)
            {
                UiKit.StrokedBox(_list, "Chosen", _cream, _gold, Stroke, 116f, y, 600f, height, 12f);
                Badge(_list, "A", 166.5f, middle, 41f);
            }
            else
            {
                UiKit.RoundBox(_list, "Chosen", new Color(1f, 1f, 1f, 0.06f), 116f, y, 600f, height, 12f);
            }
            UiKit.TextAt(_bold, _list, "Title", Tr.Get(Label(item)), TitleSize, focused ? _ink : _cream, 204f, middle + 0.5f, 500f);
            UiKit.TextAt(_medium, _list, "Sub", Sub(item), SubSize, focused ? _brown : _sand, 204f, middle + 30.5f, 500f);
        }

        private static string Label(Item item) => item switch
        {
            Item.Continue => "Continue",
            Item.NewCampaign => "New campaign",
            Item.FreeKitchen => "Free kitchen",
            Item.Settings => "Settings",
            Item.Credits => "Credits",
            _ => "Quit",
        };

        private string Sub(Item item)
        {
            switch (item)
            {
                case Item.Continue:
                    return _lastPlayed >= 0 ? Tr.Format("{restaurant} · Day {day}", ("restaurant", Name(_saves[_lastPlayed])), ("day", Shown(_saves[_lastPlayed]).Day + 1)) : "";
                case Item.NewCampaign:
                    var free = SaveSlots.FirstFree(_saves);
                    return _lastPlayed < 0 ? Tr.Get("Open your first restaurant") : free >= 0 ? Tr.Format("Starts in slot {slot}", ("slot", free + 1)) : Tr.Get("All three slots are taken");
                case Item.Settings:
                    return Tr.Get("Cooking precision, spoilage, patience, language");
                case Item.Credits:
                    return Tr.Get("Who made Mise en Flex");
                case Item.Quit:
                    return Tr.Get("Close the game");
                default:
                    return "";
            }
        }

        private string Name(CampaignSave save) =>
            string.IsNullOrEmpty(save?.Current?.Name) ? _service.RestaurantName : save.Current.Name;

        // A slot card shows the restaurant being played (Phase 6: a campaign holds several).
        private static RestaurantSave Shown(CampaignSave save) => save?.Current ?? new RestaurantSave();

        // ---- forkcast: header and cards ----

        private void DrawHeader()
        {
            UiKit.Clear(_header);
            var used = _saves.Count(s => s != null);
            UiKit.RoundBox(_header, "Slots", _well, 1569f, 148f, 119f, 32f, 16f);
            UiKit.TextAt(_bold, _header, "SlotsText", Tr.Format("{used} of {total} slots", ("used", used), ("total", SaveSlots.Count)), 17f, _cream, 1628.5f, 170f, 119f, HorizontalAlignmentOptions.Center);
            if (used > 0)
                UiKit.StrokedCircle(_header, "Avatar", _coop.GetPlayerColor(0), _gold, Stroke * 0.75f, 1758f, 146f, 37f);
            else
                UiKit.Picture(_header, "Avatar", _avatarEmpty, 1752f, 140f, 49f, 49f);
        }

        private void DrawCards()
        {
            UiKit.Clear(_cards);
            var free = SaveSlots.FirstFree(_saves);
            for (var i = 0; i < SaveSlots.Count; i++)
            {
                var x = CardX + i * CardPitch;
                var focused = _openPage == Page.None && _onCards && _card == i;
                if (_saves[i] == null)
                {
                    EmptyCard(i, x, i == free, focused);
                    continue;
                }
                SlotCard(i, _saves[i], x, focused);
            }
        }

        private void SlotCard(int slot, CampaignSave save, float x, bool highlighted)
        {
            const float w = CardWidth;
            if (highlighted)
                UiKit.RoundBox(_cards, "Highlight", _gold, x - 5.67f, CardY - 5.67f, w + 11.33f, CardHeight + 11.33f, 22.67f);
            var paper = UiKit.StrokedBox(_cards, $"Slot{slot + 1}", _paper, highlighted ? _gold : _cardEdge, ThinStroke, x, CardY, w, CardHeight, 17f);

            // The photo, clipped to the card's rounded top by the paper itself.
            paper.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var inset = ThinStroke / 2f;
            var photoWidth = w - ThinStroke;
            var photoHeight = PhotoBottom - CardY - inset;
            if (_photos[slot] != null)
            {
                var image = UiKit.Rect(paper.transform, "Photo", 0f, 0f, photoWidth, photoHeight).gameObject.AddComponent<RawImage>();
                image.texture = _photos[slot];
                image.raycastTarget = false;
                // Crop the wide close-time photo to the card's shape, around its middle.
                var aspect = photoWidth / photoHeight / ((float)_photos[slot].width / _photos[slot].height);
                image.uvRect = aspect <= 1f
                    ? new UnityEngine.Rect((1f - aspect) / 2f, 0f, aspect, 1f)
                    : new UnityEngine.Rect(0f, (1f - 1f / aspect) / 2f, 1f, 1f / aspect);
            }
            else
            {
                UiKit.RoundBox(paper.transform, "Photo", _rule, 0f, 0f, photoWidth, photoHeight, 0f);
                UiKit.TextAt(_serifItalic, paper.transform, "NoPhoto", Tr.Get("No photo yet: one is taken\nas each day closes"), 18f, _tan, photoWidth / 2f, photoHeight / 2f - 4f, photoWidth - 40f, HorizontalAlignmentOptions.Center, 2);
            }

            if (slot == _lastPlayed)
                Tag(Tr.Get("LAST PLAYED"), _lastPlayedTag, Color.white, 13.33f, x + 13f, CardY + 13f, 26f, 10.5f, 18f, false, 2.5f);
            Tag(Tr.Format("Slot {slot}", ("slot", slot + 1)), Alpha(_ink, 0.85f), _cream, 15.4f, x + w - 13f, CardY + 13f, 27f, 10f, 19f, true);

            var left = x + 21f;
            UiKit.TextAt(_bold, _cards, "Name", Name(save), 27.33f, _ink, left, 521f, 268f);

            var shown = Shown(save);
            var rating = Game.Simulation.Service.ReviewReplies.Rating(shown.Reviews);
            if (rating.HasValue)
            {
                var score = UiKit.TextAt(_bold, _cards, "Rating", $"{rating.Value:0.0}", 20f, _ink, left, 553f, 60f);
                var starsEnd = UiKit.StarRow(_cards, left + UiKit.Width(score) + 10f, 537f, 17f, 20.75f, rating.Value, _gold, _starEmpty);
                UiKit.TextAt(_medium, _cards, "Count", $"({shown.Reviews.Count:#,0})", 20f, _brown, starsEnd + 11f, 553f, 100f);
            }
            else
            {
                UiKit.TextAt(_medium, _cards, "Rating", Tr.Get("No reviews yet"), 20f, _brown, left, 553f, 268f);
            }

            var style = string.IsNullOrEmpty(shown.StyleName) ? _service.StyleName : shown.StyleName;
            var area = string.IsNullOrEmpty(shown.Area) ? _service.Area : shown.Area;
            UiKit.TextAt(_medium, _cards, "Info", $"{Locale.PriceBand(2)} · {Tr.Get(style)} · {Tr.Get(area)}", 17f, _brown, left, 580f, 268f);

            var chipEnd = Chip(Tr.Format("Day {day}", ("day", shown.Day + 1)), _dayBack, _dayText, left, 598f);
            if (save.Balance < 0f)
                Chip(Tr.Format("Owes {amount}", ("amount", Money(-save.Balance))), _owesBack, _owesText, chipEnd + 8f, 598f);

            UiKit.RoundBox(_cards, "Rule", _rule, left, 643f, 268f, 2f, 0f);
            SmallCaps(Tr.Get("BALANCE"), left + 1f, 672f);
            UiKit.TextAt(_bold, _cards, "Balance", Money(save.Balance), 23.33f, save.Balance < 0f ? _owesText : _ink, left, 698f, 140f);
            SmallCaps(Tr.Get("COOKS"), x + 160f, 672f);
            for (var c = 0; c < 4; c++)
            {
                var dotX = x + 161f + c * 26f;
                if (save.Cooks != null && save.Cooks.Contains(c))
                    UiKit.StrokedCircle(_cards, "Cook", _coop.GetPlayerColor(c), _ink, ThinStroke, dotX, 680f, 18f);
                else
                    UiKit.Picture(_cards, "Cook", _cookEmpty, dotX - 4f, 676f, 26f, 26f);
            }
            UiKit.RoundBox(_cards, "Rule", _rule, left, 720f, 268f, 2f, 0f);

            var best = BestReview(save);
            if (best != null)
            {
                UiKit.StrokedEllipse(_cards, "Avatar", UiKit.AvatarColour(best.Guest), _ink, ThinStroke, x + 22f, 737f, 32f, 40f);
                var guest = UiKit.TextAt(_bold, _cards, "Guest", GuestNames.Display(best), 18f, _ink, x + 65f, 751f, 160f);
                UiKit.StarRow(_cards, x + 65f + Mathf.Min(UiKit.Width(guest), 160f) + 7f, 738.5f, 13.5f, 15.9f, best.RatedStars, _gold, _starEmpty);
                var quote = UiKit.TextAt(_serifItalic, _cards, "Quote", $"“{ReviewText.Line(best)}”", 17.33f, _ink, x + 65f, 774f, 236f, lines: 2);
                LineHeight(quote, 22f);
            }

            UiKit.RoundBox(_cards, "Rule", _rule, x + inset, 845f, w - ThinStroke, 2f, 0f);
            UiKit.TextAt(_medium, _cards, "Played", WhenPlayed(save.LastPlayedTicks), 15f, _brown, left, 874f, 180f);
            UiKit.TextAt(_medium, _cards, "Time", PlayTime(save.PlayedSeconds), 15f, _brown, x + w - 22f, 874f, 120f, HorizontalAlignmentOptions.Right);
        }

        // A free slot: dashed. The first free one invites a new campaign (what New campaign would
        // fill); the others just say they're free.
        private void EmptyCard(int slot, float x, bool next, bool focused)
        {
            var centre = x + CardWidth / 2f;
            UiKit.Picture(_cards, $"Free{slot + 1}", _slotDashed, x - 5f, CardY - 5f, 321f, 677f, focused ? _gold : _dash);
            if (next)
            {
                var first = _lastPlayed < 0;
                UiKit.Picture(_cards, "Polaroid", first ? _polaroidFirst : _polaroidNext, x + 62.5f, 398f, 184f, 186f);
                UiKit.TextAt(_bold, _cards, "Title", Tr.Get("Start a new campaign"), 26.67f, focused ? _ink : _tan, centre, 616f, CardWidth, HorizontalAlignmentOptions.Center);
                UiKit.TextAt(_serifItalic, _cards, "Free", Tr.Format("Slot {slot} is free", ("slot", slot + 1)), 20f, _tan, centre, 654f, CardWidth, HorizontalAlignmentOptions.Center);
                var start = UiKit.TextAt(_semiBold, _cards, "Start", Tr.Get("Start"), 18.67f, _tan, 0f, 702f, 100f);
                var group = 29f + 9.4f + UiKit.Width(start);
                var badgeX = centre - group / 2f;
                Badge(_cards, "A", badgeX + 14.5f, 694.5f, 29f);
                start.rectTransform.anchoredPosition = new Vector2(badgeX + 38.4f, start.rectTransform.anchoredPosition.y);
                return;
            }
            UiKit.Picture(_cards, "Plus", _slotPlus, centre - 46f, 468f, 92f, 92f);
            UiKit.TextAt(_bold, _cards, "Title", Tr.Get("Empty slot"), 26.67f, focused ? _ink : _tan, centre, 607f, CardWidth, HorizontalAlignmentOptions.Center);
            UiKit.TextAt(_serifItalic, _cards, "Free", Tr.Get("Free"), 16.8f, _tan, centre, 645f, CardWidth, HorizontalAlignmentOptions.Center);
            Tag(Tr.Format("Slot {slot}", ("slot", slot + 1)), _cardEdge, _tan, 15.4f, x + CardWidth - 18.5f, CardY + 18f, 27f, 10f, 19f, true);
        }

        private void LoadPhotos()
        {
            for (var slot = 0; slot < SaveSlots.Count; slot++)
            {
                if (_photos[slot] != null)
                    DestroyImmediate(_photos[slot]);
                _photos[slot] = null;
                var path = SaveSlots.PhotoPath(slot);
                if (_saves[slot] == null || !File.Exists(path))
                    continue;
                var texture = new Texture2D(2, 2) { hideFlags = HideFlags.DontSave };
                if (texture.LoadImage(File.ReadAllBytes(path)))
                    _photos[slot] = texture;
                else
                    DestroyImmediate(texture);
            }
        }

        private void OnDestroy()
        {
            foreach (var photo in _photos)
            {
                if (photo != null)
                    Destroy(photo);
            }
        }

        // ---- Pages over forkcast: settings, credits, confirmations ----

        private void DrawPage()
        {
            UiKit.Clear(_page);
            _page.gameObject.SetActive(_openPage != Page.None);
            if (_openPage == Page.None)
                return;

            UiKit.RoundBox(_page, "Panel", _cream, 0f, 0f, AppWidth, AppHeight, 22f);
            UiKit.RoundBox(_page, "Header", _ink, 0f, 0f, AppWidth, HeaderHeight, 22f);
            UiKit.RoundBox(_page, "HeaderFoot", _ink, 0f, HeaderHeight - 24f, AppWidth, 24f, 0f);
            var title = _openPage switch
            {
                Page.Settings => _settingsPage.Title,
                Page.Credits => Tr.Get("Credits"),
                Page.ConfirmDelete => Tr.Format("Delete {restaurant}?", ("restaurant", Name(_saves[_card]))),
                _ => Tr.Get("All three slots are taken"),
            };
            UiKit.TextAt(_bold, _page, "Title", title, 26.67f, _cream, 32f, 46f, 900f);

            switch (_openPage)
            {
                case Page.Settings:
                    // The rows the pause menu shows too (SettingsModel, decision 2026-11-10).
                    UiKit.TextAt(_serifItalic, _page, "Note", Tr.Get("For every campaign. Saved as you change them."), 20f, _tan, 48f, 128f, 900f);
                    var rows = _settingsPage.Rows;
                    var pitch = rows.Count > 4 ? 104f : 120f;
                    for (var i = 0; i < rows.Count; i++)
                        SettingRow(i, rows[i], 160f + i * pitch);
                    break;
                case Page.Credits:
                    UiKit.TextAt(_medium, _page, "Body", Tr.Get(_credits), 26.67f, _ink, 48f, 150f, 900f, lines: 20);
                    break;
                case Page.ConfirmDelete:
                    var save = _saves[_card];
                    UiKit.TextAt(_medium, _page, "Body", Tr.Format("Slot {slot}: Day {day}, {balance}.", ("slot", _card + 1), ("day", Shown(save).Day + 1), ("balance", Money(save.Balance))), 26.67f, _ink, 48f, 330f, 920f);
                    UiKit.TextAt(_serifItalic, _page, "Warn", Tr.Get("This can't be undone."), 22f, _owesText, 48f, 376f, 920f);
                    break;
                case Page.SlotsFull:
                    UiKit.TextAt(_medium, _page, "Body", Tr.Get("Delete one first: choose its card and press Y."), 26.67f, _ink, 48f, 330f, 920f);
                    break;
            }
        }

        private void SettingRow(int index, SettingsModel.Row row, float y)
        {
            var selected = _settingsPage.Selected == index;
            if (selected)
                UiKit.StrokedBox(_page, "Selected", _paper, _gold, Stroke, 32f, y, 960f, 96f, 12f);
            UiKit.TextAt(_bold, _page, "Name", row.Name, 29f, _ink, 56f, y + 44f, 520f);
            UiKit.TextAt(_medium, _page, "What", row.What, 18f, _brown, 56f, y + 74f, 620f);
            var value = row.Opens ? "›" : selected ? $"‹  {row.Value}  ›" : row.Value;
            UiKit.TextAt(_bold, _page, "Value", value, 29f, _ink, 968f, y + 58f, 320f, HorizontalAlignmentOptions.Right);
        }

        // ---- The button hints along the bottom ----

        private void DrawHints()
        {
            UiKit.Clear(_hints);
            var hints = new List<(string Button, string Text)>();
            switch (_openPage)
            {
                case Page.Settings:
                    hints.Add((DPadHint, Tr.Get("Choose and change")));
                    if (_settingsPage.Rows[_settingsPage.Selected].Opens)
                        hints.Add(("A", Tr.Get("Open")));
                    hints.Add(("B", Tr.Get("Back")));
                    break;
                case Page.ConfirmDelete:
                    hints.Add(("A", Tr.Get("Delete")));
                    hints.Add(("B", Tr.Get("Keep it")));
                    break;
                case Page.Credits:
                case Page.SlotsFull:
                    hints.Add(("B", Tr.Get("Back")));
                    break;
                default:
                    hints.Add((DPadHint, Tr.Get("Browse")));
                    hints.Add(("A", Tr.Get("Select")));
                    if (_onCards)
                    {
                        hints.Add(("B", Tr.Get("Back")));
                        if (_saves[_card] != null)
                            hints.Add(("Y", Tr.Get("Delete slot")));
                    }
                    break;
            }

            // Laid out from the right edge leftwards: glyph, 11 px, label, then 38 px to the next.
            var right = FooterRight;
            for (var i = hints.Count - 1; i >= 0; i--)
            {
                var label = UiKit.TextAt(_semiBold, _hints, hints[i].Text, hints[i].Text, 21f, _cream, 0f, 1056f, 300f);
                var width = UiKit.Width(label);
                label.rectTransform.anchoredPosition = new Vector2(right - width, label.rectTransform.anchoredPosition.y);
                var glyphRight = right - width - 11f;
                if (hints[i].Button == DPadHint)
                {
                    UiKit.RoundBox(_hints, "DPad", _cream, glyphRight - 44f, 1032f, 44f, 32f, 9f);
                    UiKit.DPad(_hints, _ink, glyphRight - 22f, 1048f, 22f);
                    right = glyphRight - 44f - 38f;
                }
                else
                {
                    Badge(_hints, hints[i].Button, glyphRight - 17.5f, 1047.5f, 35f);
                    right = glyphRight - 35f - 38f;
                }
            }
        }

        // ---- Pieces ----

        // An Xbox face button: its colour, a white ring and the letter, centred on (cx, cy).
        private void Badge(Transform parent, string button, float cx, float cy, float diameter)
        {
            var colour = button switch
            {
                "A" => _buttonA,
                "B" => _buttonB,
                "X" => _buttonX,
                _ => _buttonY,
            };
            UiKit.StrokedCircle(parent, button, colour, Color.white, 4f, cx - diameter / 2f, cy - diameter / 2f, diameter);
            var size = diameter * 0.535f;
            UiKit.TextAt(_bold, parent, "Letter", button, size, button == "Y" ? _ink : Color.white, cx, cy + size * 0.37f, diameter, HorizontalAlignmentOptions.Center);
        }

        // A small rounded tag. With alignRight, x is its right edge.
        private void Tag(string text, Color back, Color fore, float size, float x, float y, float height, float pad, float baseline, bool alignRight, float spacing = 0f)
        {
            var label = UiKit.TextAt(_bold, _cards, text, text, size, fore, 0f, y + baseline, 200f);
            label.characterSpacing = spacing / size * 100f;
            var width = UiKit.Width(label) + pad * 2f;
            var left = alignRight ? x - width : x;
            var box = UiKit.RoundBox(_cards, text + "Tag", back, left, y, width, height, 8f);
            box.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
            label.rectTransform.anchoredPosition = new Vector2(left + pad, label.rectTransform.anchoredPosition.y);
        }

        // A pill: returns its right edge.
        private float Chip(string text, Color back, Color fore, float x, float y)
        {
            var label = UiKit.TextAt(_bold, _cards, text, text, 15.5f, fore, x + 11.5f, y + 20f, 240f);
            var width = UiKit.Width(label) + 23f;
            var box = UiKit.RoundBox(_cards, text + "Chip", back, x, y, width, 29f, 14.5f);
            box.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
            return x + width;
        }

        private void SmallCaps(string text, float x, float baseline)
        {
            var label = UiKit.TextAt(_bold, _cards, text, text, 13.33f, _tan, x, baseline, 130f);
            // About 1.5 px between letters, as in the mockup.
            label.characterSpacing = 1.5f / 13.33f * 100f;
        }

        // TextMeshPro spaces lines by the font's own line height; this sets an exact pitch instead.
        private static void LineHeight(TextMeshProUGUI label, float pixels)
        {
            var face = label.font.faceInfo;
            var natural = face.lineHeight * label.fontSize / face.pointSize;
            label.lineSpacing = (pixels - natural) / label.fontSize * 100f;
        }

        // In the player's language and currency (Locale), whole amounts on the cards.
        private static string Money(float amount) => Locale.Money(amount, MoneyStyle.Whole);

        private static string PlayTime(float seconds)
        {
            var minutes = Mathf.FloorToInt(seconds / 60f);
            return minutes >= 60
                ? Tr.Format("{hours} h {minutes} min", ("hours", minutes / 60), ("minutes", minutes % 60))
                : Tr.Format("{minutes} min", ("minutes", minutes));
        }

        private static string WhenPlayed(long ticks)
        {
            if (ticks <= 0)
                return "";
            var when = new DateTime(ticks);
            var days = (DateTime.Now.Date - when.Date).Days;
            return days switch
            {
                0 => Tr.Get("Played today"),
                1 => Tr.Get("Played yesterday"),
                _ => Tr.Format("Played {date}", ("date", Locale.ShortDate(when))),
            };
        }

        private static ReviewRecord BestReview(CampaignSave save) =>
            save?.Current?.Reviews?.Where(r => !string.IsNullOrEmpty(r.Line)).OrderByDescending(r => r.RatedStars).ThenByDescending(r => r.Day).FirstOrDefault();

        // ---- The sign ----

        // The sign, drawn piece by piece instead of as one picture (the user's call, 2026-11-10): the
        // board on its rod with the logo, a REC light that blinks and a like count that keeps
        // climbing. The polaroid and the review that were stuck to it are gone: the street's own
        // phones pop those now.
        private void BuildSign(RectTransform parent)
        {
            var sign = UiKit.Rect(parent, "Sign", 0f, 0f, 1920f, 1080f);
            var right = BoardX + BoardWidth;
            var middle = BoardX + BoardWidth / 2f;

            // The rod with a cap at each end, and the chains: down to the board, and on to the list.
            UiKit.RoundBox(sign, "Rod", SignGold, BoardX - 46f, 64f, BoardWidth + 92f, 15f, 0f);
            foreach (var x in new[] { BoardX - 46f, right + 26f })
                UiKit.RoundBox(sign, "RodCap", SignBrass, x, 53f, 20f, 39f, 5f);
            foreach (var x in new[] { BoardX + 80f, right - 88f })
            {
                Chain(sign, x, 79f, BoardY);
                Chain(sign, x, BoardY + BoardHeight, ListY);
            }

            // The board: its shadow, a gold edge, the dark face, a fine line inside it and a bracket
            // in each corner, like a camera's frame.
            UiKit.RoundBox(sign, "Shadow", new Color(0f, 0f, 0f, 0.38f), BoardX, BoardY + 13f, BoardWidth, BoardHeight, 18f);
            UiKit.RoundBox(sign, "Edge", SignGold, BoardX, BoardY, BoardWidth, BoardHeight, 18f);
            UiKit.RoundBox(sign, "Face", _ink, BoardX + 4f, BoardY + 4f, BoardWidth - 8f, BoardHeight - 8f, 14f);
            UiKit.StrokedBox(sign, "Line", _ink, SignLine, 1.33f, BoardX + 12.5f, BoardY + 12.5f, BoardWidth - 25f, BoardHeight - 25f, 9f);
            foreach (var east in new[] { false, true })
            foreach (var south in new[] { false, true })
            {
                var x = east ? right - 33f : BoardX + 33f;
                var y = south ? BoardY + BoardHeight - 29f : BoardY + 29f;
                UiKit.RoundBox(sign, "Bracket", SignBrass, east ? x - 25f : x, south ? y - 4f : y, 25f, 4f, 0f);
                UiKit.RoundBox(sign, "Bracket", SignBrass, east ? x - 4f : x, south ? y - 25f : y, 4f, 25f, 0f);
            }

            // The logo, a touch bigger than in the mockup now that it has the board to itself, in the
            // middle and a little low, leaving the top right for the REC light.
            var logoWidth = LogoWidth * LogoScale;
            var logoHeight = LogoHeight * LogoScale;
            UiKit.Picture(sign, "Logo", _logo, middle - logoWidth / 2f, BoardY + (BoardHeight - logoHeight) / 2f + 6f, logoWidth, logoHeight);

            // REC, and its light.
            var rec = UiKit.TextAt(_bold, sign, "Rec", "REC", 17f, RecRed, right - 66f, BoardY + 53f, 90f, HorizontalAlignmentOptions.Right);
            rec.characterSpacing = 12f;
            var dotX = right - 66f - UiKit.Width(rec) - 20f;
            var dotY = BoardY + 46.5f;
            _recGlow = UiKit.Ellipse(sign, "RecGlow", Alpha(LikePink, 0.22f), dotX - 11f, dotY - 11f, 22f, 22f);
            _recDot = UiKit.Ellipse(sign, "RecDot", LikePink, dotX - 7f, dotY - 7f, 14f, 14f);

            // The like count, tipped over the board's top corner: a pink pill with a heart.
            _likePill = UiKit.Rect(sign, "Likes", 0f, 0f, 156f, 50f);
            _likePill.pivot = new Vector2(0.5f, 0.5f);
            _likePill.anchoredPosition = new Vector2(right - 38f, -(BoardY - 14f));
            _likePill.localRotation = Quaternion.Euler(0f, 0f, -6.8f);
            var tail = UiKit.RoundBox(_likePill, "Tail", LikePink, 0f, 0f, 22f, 22f, 3f).rectTransform;
            tail.pivot = new Vector2(0.5f, 0.5f);
            tail.anchoredPosition = new Vector2(44f, -45f);
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            UiKit.RoundBox(_likePill, "Back", LikePink, 0f, 0f, 156f, 50f, 22f);
            // The heart was cut from the tipped pill, so it's already turned: set it straight again here.
            var heart = UiKit.Picture(_likePill, "Heart", _heart, 0f, 0f, 31.5f, 28.5f).rectTransform;
            heart.pivot = new Vector2(0.5f, 0.5f);
            heart.anchoredPosition = new Vector2(35f, -26f);
            heart.localRotation = Quaternion.Euler(0f, 0f, 6.8f);
            _likes = UiKit.TextAt(_bold, _likePill, "Count", LikeText(), 29f, Color.white, 59f, 35.5f, 96f);
            _nextLike = 0f;
        }

        // A chain of gold links (dashes) from `top` down to `bottom`.
        private static void Chain(RectTransform parent, float x, float top, float bottom)
        {
            const float link = 8f, gap = 6f;
            for (var y = top + 1f; y < bottom - 1f; y += link + gap)
                UiKit.RoundBox(parent, "Link", SignGold, x, y, 8f, Mathf.Min(link, bottom - y), 0f);
        }

        private string LikeText() => (_likeCount / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "k";

        // The sign's life (the user's call, 2026-11-10): the REC light blinks as a camera's does, and
        // every two or three seconds the like count goes up, with a little bounce.
        private void AnimateSign()
        {
            if (_recDot == null || _likes == null)
                return;
            var now = Time.unscaledTime;
            // On for a second, off for half, with quick fades.
            var phase = Mathf.Repeat(now, 1.5f);
            var lit = Mathf.Clamp01(Mathf.Min(phase / 0.08f, (1f - phase) / 0.08f));
            _recDot.color = Alpha(LikePink, 0.12f + 0.88f * lit);
            _recGlow.color = Alpha(LikePink, 0.22f * lit);

            if (_nextLike <= 0f)
            {
                _nextLike = now + UnityEngine.Random.Range(2f, 3f);
            }
            else if (now >= _nextLike)
            {
                _nextLike = now + UnityEngine.Random.Range(2f, 3f);
                _likeCount += UnityEngine.Random.Range(1, 4);
                _likes.text = LikeText();
                _likePopped = now;
            }
            var bounce = (now - _likePopped) / 0.35f;
            _likePill.localScale = Vector3.one * (bounce < 1f ? 1f + 0.16f * Mathf.Sin(bounce * Mathf.PI) : 1f);
        }

        // ---- Building the fixed parts ----

        private void Build()
        {
            var canvasObject = new GameObject("MenuCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Expand: the whole 1920×1080 layout always fits, on 16:10 MacBook screens too (with
            // matching width and height halfway, a 16:10 screen cut off the sides).
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            _canvas = canvasObject.GetComponent<RectTransform>();

            // The hint bar spans the screen's bottom edge whatever its shape.
            var bar = UiKit.RoundBox(_canvas, "Footer", Alpha(_ink, 0.95f), 0f, 0f, 0f, 0f, 0f).rectTransform;
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(0f, 1080f - FooterY);

            // The mockup's 1920×1080 frame, centred; on a taller screen the extra space falls around it.
            var root = UiKit.Rect(_canvas, "Layout", 0f, 0f, 1920f, 1080f);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.SetSiblingIndex(0);

            // The list panel, and the sign hanging over it.
            var column = UiKit.Rect(root, "Column", ColumnShift, 0f, 1920f, 1080f);
            UiKit.StrokedBox(column, "List", Alpha(_ink, 0.95f), _panelEdge, Stroke, ListX, ListY, ListWidth, ListHeight, 18f);
            _list = UiKit.Rect(column, "Items", 0f, 0f, 1920f, 1080f);
            BuildSign(column);

            // forkcast, with the campaigns: shown only once Continue opens it, so the top level is
            // the sign and the list and the scene shows behind (the user's call, 2026-11-10).
            _app = UiKit.Rect(root, "Forkcast", AppShift, 0f, 1920f, 1080f);
            UiKit.RoundBox(_app, "App", _cream, AppX, AppY, AppWidth, AppHeight, 22f);
            UiKit.RoundBox(_app, "Header", _ink, AppX, AppY, AppWidth, HeaderHeight, 22f);
            UiKit.RoundBox(_app, "HeaderFoot", _ink, AppX, AppY + HeaderHeight - 24f, AppWidth, 24f, 0f);
            UiKit.Picture(_app, "Logo", _forkcastLogo, 828f, 146f, 132f, 36f);
            UiKit.RoundBox(_app, "Search", _well, 974f, 143f, 577f, 42f, 21f);
            UiKit.Picture(_app, "SearchIcon", _searchIcon, 991f, 153f, 22f, 22f);
            UiKit.TextAt(_medium, _app, "SearchText", Tr.Get("Your spots · dishes · @chefs"), 18.67f, _sand, 1022f, 171f, 500f);
            UiKit.Ellipse(_app, "Bell", _well, 1702f, 144f, 40f, 40f);
            UiKit.Picture(_app, "BellIcon", _bellIcon, 1711f, 147f, 28f, 28f);
            _header = UiKit.Rect(_app, "HeaderState", 0f, 0f, 1920f, 1080f);
            _cards = UiKit.Rect(_app, "Cards", 0f, 0f, 1920f, 1080f);
            _page = UiKit.Rect(root, "Page", AppX + AppShift, AppY, AppWidth, AppHeight);

            // The hints keep the mockup's coordinates, in a 1920×1080 frame pinned to the screen's bottom.
            _hints = UiKit.Rect(_canvas, "Hints", 0f, 0f, 1920f, 1080f);
            _hints.anchorMin = _hints.anchorMax = _hints.pivot = new Vector2(0.5f, 0f);
            _hints.anchoredPosition = Vector2.zero;
        }
    }
}
