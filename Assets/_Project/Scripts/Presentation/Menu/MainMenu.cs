using System;
using System.IO;
using System.Linq;
using Game.Data;
using Game.Gameplay.Saving;
using Game.Presentation.Hud;
using Game.Simulation.Saving;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Presentation.Menu
{
    /// <summary>
    /// The main menu (decision 2026-11-08, after the user's mockup). On the left are the sign and the
    /// list: Continue, New campaign, Free kitchen (later), Settings, Credits and Quit. On the right,
    /// forkcast shows the three campaign slots as cards. Any controller (or the keyboard's stand-in
    /// keys) drives it, because nobody has joined yet. It's built in code, over the 3D night city.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        private enum Item
        {
            Continue,
            NewCampaign,
            FreeKitchen,
            Settings,
            Credits,
            Quit,
        }

        private enum Page
        {
            None,
            Settings,
            Credits,
            ConfirmDelete,
            SlotsFull,
        }

        [SerializeField] private HudStyle _style;
        [SerializeField] private CoopSettings _coop;
        [SerializeField] private RealismSettings _realism;
        [SerializeField] private ServiceConfig _service;
        [SerializeField] private string _kitchenScene = "Kitchen_Greybox";

        [Header("Look")]
        [SerializeField] private Color _night = new Color(0.10f, 0.08f, 0.07f, 0.94f);
        [SerializeField] private Color _paper = new Color(0.97f, 0.93f, 0.85f);
        [SerializeField] private Color _ink = new Color(0.16f, 0.12f, 0.09f);
        [SerializeField] private Color _muted = new Color(0.47f, 0.40f, 0.33f);
        [SerializeField] private Color _gold = new Color(0.87f, 0.69f, 0.30f);
        [SerializeField] private Color _good = new Color(0.24f, 0.60f, 0.47f);
        [SerializeField] private Color _soft = new Color(0.92f, 0.86f, 0.75f);
        [SerializeField] private Color _alarm = new Color(0.96f, 0.82f, 0.76f);
        [SerializeField] private Color _alarmText = new Color(0.75f, 0.30f, 0.12f);

        [Header("Feel")]
        [SerializeField, Range(0.1f, 1f)] private float _stickThreshold = 0.5f;
        [SerializeField, Min(0.05f)] private float _stickRepeat = 0.22f;

        [Header("Text")]
        [SerializeField, TextArea] private string _credits = "Mise en Flex\n\nA local co-op restaurant game.\n\nFull credits to come.";

        private CampaignSave[] _saves;
        private int _lastPlayed;
        private PlayerSettings _settings;

        private RectTransform _canvas;
        private RectTransform _list;
        private RectTransform _cards;
        private RectTransform _page;
        private RectTransform _sticker;
        private TextMeshProUGUI _slotsPill;
        private TextMeshProUGUI _hints;

        private bool _onCards;
        private Item _item;
        private int _card;
        private Page _openPage;
        private int _settingRow;
        private Vector2 _lastStick;
        private float _nextStep;
        private bool _dirty = true;

        private void Start()
        {
            _settings = PlayerSettings.Load(_realism);
            ReadSlots();
            _item = _lastPlayed >= 0 ? Item.Continue : Item.NewCampaign;
            Build();
        }

        private void ReadSlots()
        {
            _saves = SaveSlots.ReadAll();
            _lastPlayed = SaveSlots.LastPlayed(_saves);
            _dirty = true;
        }

        private void Update()
        {
            ReadInput();
            if (!_dirty)
                return;
            _dirty = false;
            DrawList();
            DrawCards();
            DrawPage();
            DrawSticker();
            _hints.text = Hints();
        }

        // ---- Input ----

        private void ReadInput()
        {
            var stick = Stick();
            var step = Vector2Int.zero;
            if (stick.magnitude < _stickThreshold)
            {
                _nextStep = 0f;
            }
            else if (Time.unscaledTime >= _nextStep)
            {
                _nextStep = Time.unscaledTime + _stickRepeat;
                step = Mathf.Abs(stick.x) > Mathf.Abs(stick.y)
                    ? new Vector2Int(stick.x > 0f ? 1 : -1, 0)
                    : new Vector2Int(0, stick.y > 0f ? -1 : 1);
            }

            switch (_openPage)
            {
                case Page.Settings:
                    SettingsInput(step);
                    return;
                case Page.Credits:
                case Page.SlotsFull:
                    if (Pressed(Face.South) || Pressed(Face.East))
                        Open(Page.None);
                    return;
                case Page.ConfirmDelete:
                    if (Pressed(Face.South))
                    {
                        SaveSlots.Delete(_card);
                        ReadSlots();
                        Open(Page.None);
                    }
                    else if (Pressed(Face.East))
                    {
                        Open(Page.None);
                    }
                    return;
            }

            if (_onCards)
            {
                if (step.x != 0)
                {
                    var next = _card + step.x;
                    if (next < 0)
                        _onCards = false;
                    else
                        _card = Mathf.Min(next, SaveSlots.Count - 1);
                    _dirty = true;
                }
                if (Pressed(Face.East))
                {
                    _onCards = false;
                    _dirty = true;
                }
                else if (Pressed(Face.North) && _saves[_card] != null)
                {
                    Open(Page.ConfirmDelete);
                }
                else if (Pressed(Face.South))
                {
                    if (_saves[_card] != null)
                        Launch(_card, true);
                    else
                        Launch(_card, false);
                }
                return;
            }

            if (step.y != 0)
            {
                var items = (Item[])Enum.GetValues(typeof(Item));
                var index = Array.IndexOf(items, _item);
                do
                    index = Mathf.Clamp(index + step.y, 0, items.Length - 1);
                while (!IsAvailable(items[index]) && index > 0 && index < items.Length - 1);
                if (IsAvailable(items[index]))
                    _item = items[index];
                _dirty = true;
            }
            if (step.x > 0)
            {
                _onCards = true;
                _card = Mathf.Max(0, _lastPlayed);
                _dirty = true;
            }
            if (Pressed(Face.South))
                Choose(_item);
        }

        private bool IsAvailable(Item item) => item switch
        {
            Item.Continue => _lastPlayed >= 0,
            Item.FreeKitchen => false, // a sandbox for later (decision 2026-11-08)
            _ => true,
        };

        private void Choose(Item item)
        {
            switch (item)
            {
                case Item.Continue:
                    if (_lastPlayed >= 0)
                        Launch(_lastPlayed, true);
                    break;
                case Item.NewCampaign:
                    var free = SaveSlots.FirstFree(_saves);
                    if (free < 0)
                        Open(Page.SlotsFull);
                    else
                        Launch(free, false);
                    break;
                case Item.Settings:
                    _settingRow = 0;
                    Open(Page.Settings);
                    break;
                case Item.Credits:
                    Open(Page.Credits);
                    break;
                case Item.Quit:
                    Application.Quit();
                    break;
            }
        }

        private void Launch(int slot, bool continuing)
        {
            if (continuing)
                CampaignLaunch.Continue(slot);
            else
                CampaignLaunch.StartNew(slot);
            SceneManager.LoadScene(_kitchenScene);
        }

        private void Open(Page page)
        {
            _openPage = page;
            _dirty = true;
        }

        // The three dials that exist, adjusted with the stick and saved at once.
        private void SettingsInput(Vector2Int step)
        {
            if (Pressed(Face.East) || Pressed(Face.South))
            {
                Open(Page.None);
                return;
            }
            if (step.y != 0)
            {
                _settingRow = Mathf.Clamp(_settingRow + step.y, 0, 2);
                _dirty = true;
            }
            if (step.x == 0)
                return;
            switch (_settingRow)
            {
                case 0:
                    _settings.CookingPrecision = Mathf.Clamp01(Mathf.Round((_settings.CookingPrecision + step.x * 0.1f) * 10f) / 10f);
                    break;
                case 1:
                    _settings.Spoilage = Mathf.Clamp(Mathf.Round((_settings.Spoilage + step.x * 0.25f) * 4f) / 4f, 0f, 3f);
                    break;
                default:
                    _settings.Patience = Mathf.Clamp(Mathf.Round((_settings.Patience + step.x * 0.25f) * 4f) / 4f, 0.25f, 3f);
                    break;
            }
            _settings.Save();
            _dirty = true;
        }

        private enum Face
        {
            South,
            East,
            North,
        }

        private static bool Pressed(Face face)
        {
            foreach (var pad in Gamepad.all)
            {
                var button = face == Face.South ? pad.buttonSouth : face == Face.East ? pad.buttonEast : pad.buttonNorth;
                if (button.wasPressedThisFrame)
                    return true;
            }
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;
            return face switch
            {
                Face.South => keyboard.xKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame,
                Face.East => keyboard.dKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame,
                _ => keyboard.wKey.wasPressedThisFrame,
            };
        }

        private static Vector2 Stick()
        {
            foreach (var pad in Gamepad.all)
            {
                var value = pad.leftStick.ReadValue() + pad.dpad.ReadValue();
                if (value.sqrMagnitude > 0.01f)
                    return value;
            }
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector2.zero;
            return new Vector2(
                (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.downArrowKey.isPressed ? 1f : 0f));
        }

        // ---- Drawing ----

        private string Money(float amount) =>
            (amount < 0f ? "−" : "") + _service.Currency + Mathf.Abs(amount).ToString("#,0");

        private static string PlayTime(float seconds)
        {
            var minutes = Mathf.FloorToInt(seconds / 60f);
            return minutes >= 60 ? $"{minutes / 60} h {minutes % 60} min" : $"{minutes} min";
        }

        private static string WhenPlayed(long ticks)
        {
            if (ticks <= 0)
                return "";
            var when = new DateTime(ticks);
            var days = (DateTime.Now.Date - when.Date).Days;
            return days switch
            {
                0 => "Played today",
                1 => "Played yesterday",
                _ => $"Played {when:d MMM}",
            };
        }

        private static ReviewRecord BestReview(CampaignSave save) =>
            save?.Reviews?.Where(r => !string.IsNullOrEmpty(r.Line)).OrderByDescending(r => r.RatedStars).ThenByDescending(r => r.Day).FirstOrDefault();

        private void DrawList()
        {
            UiKit.Clear(_list);
            var y = 24f;
            foreach (Item item in Enum.GetValues(typeof(Item)))
            {
                var selected = !_onCards && _openPage == Page.None && item == _item;
                var available = IsAvailable(item);
                if (item == Item.Settings)
                {
                    UiKit.Box(_style, _list, "Rule", new Color(1f, 1f, 1f, 0.12f), 40f, y + 4f, 540f, 2f, rounded: false);
                    y += 18f;
                }

                if (item == Item.Continue)
                {
                    var height = 108f;
                    var card = UiKit.Box(_style, _list, "Continue", selected ? _paper : new Color(1f, 1f, 1f, 0.06f), 16f, y, 598f, height);
                    if (selected)
                        UiKit.Outline(_style, card, _gold, 4f);
                    var title = available ? "Continue" : HudStyle.Colored("Continue", _muted);
                    var sub = available ? $"{Name(_saves[_lastPlayed])} · Day {_saves[_lastPlayed].Day + 1}" : "No campaign yet";
                    if (selected)
                        Badge(card, "A", 32f, 32f, 44f);
                    UiKit.Text(_style, card, "Title", title, 42f, selected ? _ink : _paper, FontStyles.Bold, 92f, 14f, 480f, 52f);
                    UiKit.Text(_style, card, "Sub", sub, 24f, selected ? _muted : _soft, FontStyles.Normal, 92f, 64f, 480f, 34f);
                    y += height + 14f;
                    continue;
                }

                var label = item switch
                {
                    Item.NewCampaign => "New campaign",
                    Item.FreeKitchen => "Free kitchen",
                    _ => item.ToString(),
                };
                if (selected)
                    UiKit.Box(_style, _list, "Selected", _paper, 16f, y - 4f, 598f, 62f);
                var colour = selected ? _ink : available ? _paper : new Color(_paper.r, _paper.g, _paper.b, 0.35f);
                if (selected)
                    Badge(_list, "A", 32f, y + 6f, 40f);
                UiKit.Text(_style, _list, label, label, 38f, colour, FontStyles.Bold, selected ? 88f : 44f, y, 400f, 54f);
                if (item == Item.FreeKitchen)
                    UiKit.Text(_style, _list, "Later", "coming later", 20f, new Color(_paper.r, _paper.g, _paper.b, 0.35f), FontStyles.Italic, 300f, y + 16f, 240f, 30f);
                y += 64f;
            }
        }

        private string Name(CampaignSave save) =>
            string.IsNullOrEmpty(save?.RestaurantName) ? _service.RestaurantName : save.RestaurantName;

        private void DrawCards()
        {
            UiKit.Clear(_cards);
            var used = _saves.Count(s => s != null);
            _slotsPill.text = $"{used} of {SaveSlots.Count} slots";
            for (var i = 0; i < SaveSlots.Count; i++)
            {
                var x = 24f + i * 332f;
                var selected = _onCards && _openPage == Page.None && _card == i;
                if (_saves[i] == null)
                    EmptyCard(i, x, selected);
                else
                    SlotCard(i, _saves[i], x, selected);
            }
        }

        private void SlotCard(int slot, CampaignSave save, float x, bool selected)
        {
            const float width = 310f;
            const float height = 660f;
            if (selected)
                UiKit.Box(_style, _cards, "Selected", _gold, x - 6f, 18f, width + 12f, height + 12f);
            var card = UiKit.Box(_style, _cards, $"Slot{slot + 1}", _paper, x, 24f, width, height);

            var photo = UiKit.Box(_style, card, "Photo", _soft, 0f, 0f, width, 250f, rounded: false);
            var picture = LoadPhoto(slot);
            if (picture != null)
            {
                var image = new GameObject("Image", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(photo, false);
                UiKit.Fill(image.rectTransform);
                image.texture = picture;
                // Crop the wide close-time photo to the card's shape, around its middle.
                var aspect = width / 250f / ((float)picture.width / picture.height);
                image.uvRect = new UnityEngine.Rect((1f - aspect) / 2f, 0f, aspect, 1f);
            }
            if (slot == _lastPlayed)
                Tag(card, "LAST PLAYED", _good, _paper, 12f, 14f);
            Tag(card, $"Slot {slot + 1}", new Color(0.1f, 0.07f, 0.05f, 0.75f), _paper, width - 92f, 14f);

            UiKit.Text(_style, card, "Name", Name(save), 32f, _ink, FontStyles.Bold, 20f, 266f, width - 40f, 42f);
            var rating = ReviewReplies(save);
            var line = UiKit.Text(_style, card, "Rating", rating.HasValue ? $"<b>{rating.Value:0.0}</b>" : HudStyle.Colored("No reviews", _muted), 24f, _ink, FontStyles.Normal, 20f, 312f, 80f, 32f);
            if (rating.HasValue)
            {
                UiKit.Stars(card, 20f + line.preferredWidth + 10f, 316f, 20f, rating.Value, _gold, _muted);
                UiKit.Text(_style, card, "Count", $"({save.Reviews.Count})", 22f, _muted, FontStyles.Normal, 20f + line.preferredWidth + 10f + 5f * 22f + 8f, 312f, 90f, 32f);
            }
            var style = string.IsNullOrEmpty(save.StyleName) ? _service.StyleName : save.StyleName;
            var area = string.IsNullOrEmpty(save.Area) ? _service.Area : save.Area;
            UiKit.Text(_style, card, "Info", $"€€ · {style} · {area}", 21f, _muted, FontStyles.Normal, 20f, 348f, width - 40f, 30f);

            var chipX = Chip(card, $"Day {save.Day + 1}", _alarm.r > 0 ? new Color(0.85f, 0.93f, 0.85f) : _soft, _good, 20f, 388f);
            if (save.Balance < 0f)
                Chip(card, $"Owes {Money(-save.Balance)}", _alarm, _alarmText, chipX + 8f, 388f);

            UiKit.Box(_style, card, "Rule", _soft, 20f, 436f, width - 40f, 2f, rounded: false);
            UiKit.Text(_style, card, "BalanceTitle", "BALANCE", 16f, _muted, FontStyles.Bold, 20f, 448f, 130f, 24f).characterSpacing = 4f;
            UiKit.Text(_style, card, "Balance", Money(save.Balance), 30f, save.Balance < 0f ? _alarmText : _ink, FontStyles.Bold, 20f, 470f, 140f, 40f);
            UiKit.Text(_style, card, "CooksTitle", "COOKS", 16f, _muted, FontStyles.Bold, 160f, 448f, 130f, 24f).characterSpacing = 4f;
            for (var c = 0; c < 4; c++)
            {
                var cooked = save.Cooks != null && save.Cooks.Contains(c);
                var dot = UiKit.Box(_style, card, "Cook", cooked ? _coop.GetPlayerColor(c) : new Color(_muted.r, _muted.g, _muted.b, 0.25f), 160f + c * 30f, 480f, 22f, 22f, circle: true);
                if (cooked)
                    UiKit.Outline(_style, dot, _ink, 2f, circle: true);
            }

            UiKit.Box(_style, card, "Rule2", _soft, 20f, 520f, width - 40f, 2f, rounded: false);
            var best = BestReview(save);
            if (best != null)
            {
                UiKit.Box(_style, card, "Avatar", Color.HSVToRGB(Mathf.Abs(best.Guest.GetHashCode() % 360) / 360f, 0.3f, 0.75f), 20f, 536f, 34f, 38f, circle: true);
                UiKit.Text(_style, card, "Guest", $"<b>{best.Guest}</b>", 19f, _ink, FontStyles.Normal, 64f, 532f, 220f, 26f);
                UiKit.Text(_style, card, "Quote", $"“{best.Line}”", 19f, _ink, FontStyles.Italic, 64f, 558f, width - 84f, 52f);
            }

            UiKit.Text(_style, card, "Played", WhenPlayed(save.LastPlayedTicks), 18f, _muted, FontStyles.Normal, 20f, 624f, 170f, 26f);
            UiKit.Text(_style, card, "Time", PlayTime(save.PlayedSeconds), 18f, _muted, FontStyles.Normal, 170f, 624f, width - 190f, 26f).alignment = TextAlignmentOptions.TopRight;
        }

        private static float? ReviewReplies(CampaignSave save) => Game.Simulation.Service.ReviewReplies.Rating(save.Reviews);

        private void EmptyCard(int slot, float x, bool selected)
        {
            const float width = 310f;
            const float height = 660f;
            var card = UiKit.Box(_style, _cards, $"Free{slot + 1}", new Color(1f, 1f, 1f, 0.02f), x, 24f, width, height);
            UiKit.Outline(_style, card, selected ? _gold : new Color(_muted.r, _muted.g, _muted.b, 0.5f), selected ? 5f : 3f);
            UiKit.Text(_style, card, "Title", "Open a new restaurant", 28f, _gold, FontStyles.Bold, 10f, 300f, width - 20f, 40f).alignment = TextAlignmentOptions.Top;
            UiKit.Text(_style, card, "Free", $"Slot {slot + 1} is free", 20f, _muted, FontStyles.Italic, 10f, 346f, width - 20f, 30f).alignment = TextAlignmentOptions.Top;
            if (selected)
            {
                Badge(card, "A", width / 2f - 60f, 392f, 36f);
                UiKit.Text(_style, card, "Start", "Start", 24f, _muted, FontStyles.Normal, width / 2f - 14f, 394f, 100f, 34f);
            }
        }

        private Texture2D LoadPhoto(int slot)
        {
            var path = SaveSlots.PhotoPath(slot);
            if (!File.Exists(path))
                return null;
            var texture = new Texture2D(2, 2);
            return texture.LoadImage(File.ReadAllBytes(path)) ? texture : null;
        }

        private void DrawPage()
        {
            UiKit.Clear(_page);
            _page.gameObject.SetActive(_openPage != Page.None);
            if (_openPage == Page.None)
                return;

            var panel = UiKit.Box(_style, _page, "Panel", _paper, 0f, 0f, 1024f, 788f);
            switch (_openPage)
            {
                case Page.Settings:
                    UiKit.Text(_style, panel, "Title", "Settings", 44f, _ink, FontStyles.Bold, 48f, 40f, 800f, 60f);
                    UiKit.Text(_style, panel, "Note", "For every campaign. Saved as you change them.", 22f, _muted, FontStyles.Italic, 48f, 104f, 800f, 32f);
                    SettingRow(panel, 0, "Cooking precision", $"{_settings.CookingPrecision * 100f:0}%", "how narrow the doneness windows are", 170f);
                    SettingRow(panel, 1, "Spoilage", $"×{_settings.Spoilage:0.##}", "how fast food loses freshness", 290f);
                    SettingRow(panel, 2, "Customer patience", $"×{_settings.Patience:0.##}", "how long guests wait", 410f);
                    UiKit.Text(_style, panel, "Later", "Presets and changing the controls come later.", 20f, _muted, FontStyles.Italic, 48f, 540f, 800f, 30f);
                    break;
                case Page.Credits:
                    UiKit.Text(_style, panel, "Title", "Credits", 44f, _ink, FontStyles.Bold, 48f, 40f, 800f, 60f);
                    UiKit.Text(_style, panel, "Body", _credits, 28f, _ink, FontStyles.Normal, 48f, 120f, 900f, 500f);
                    break;
                case Page.ConfirmDelete:
                    UiKit.Text(_style, panel, "Title", $"Delete {Name(_saves[_card])}?", 44f, _ink, FontStyles.Bold, 48f, 260f, 920f, 60f);
                    UiKit.Text(_style, panel, "Body", $"Slot {_card + 1}: Day {_saves[_card].Day + 1}, {Money(_saves[_card].Balance)}. This can't be undone.", 26f, _muted, FontStyles.Normal, 48f, 330f, 920f, 80f);
                    break;
                case Page.SlotsFull:
                    UiKit.Text(_style, panel, "Title", "All three slots are taken", 44f, _ink, FontStyles.Bold, 48f, 260f, 920f, 60f);
                    UiKit.Text(_style, panel, "Body", "Delete one first: pick its card and press Y.", 26f, _muted, FontStyles.Normal, 48f, 330f, 920f, 80f);
                    break;
            }
        }

        private void SettingRow(RectTransform panel, int row, string name, string value, string what, float y)
        {
            var selected = _settingRow == row;
            if (selected)
                UiKit.Box(_style, panel, "Selected", _soft, 32f, y - 10f, 960f, 100f);
            UiKit.Text(_style, panel, "Name", name, 32f, _ink, FontStyles.Bold, 56f, y, 520f, 44f);
            UiKit.Text(_style, panel, "What", what, 21f, _muted, FontStyles.Normal, 56f, y + 46f, 520f, 30f);
            UiKit.Text(_style, panel, "Value", selected ? $"‹  {value}  ›" : value, 34f, _ink, FontStyles.Bold, 640f, y + 14f, 320f, 50f).alignment = TextAlignmentOptions.TopRight;
        }

        private void DrawSticker()
        {
            UiKit.Clear(_sticker);
            var best = _lastPlayed >= 0 ? BestReview(_saves[_lastPlayed]) : null;
            _sticker.gameObject.SetActive(best != null);
            if (best == null)
                return;
            var card = UiKit.Box(_style, _sticker, "Sticker", _paper, 0f, 0f, 300f, 92f);
            UiKit.Box(_style, card, "Avatar", Color.HSVToRGB(Mathf.Abs(best.Guest.GetHashCode() % 360) / 360f, 0.3f, 0.75f), 16f, 20f, 50f, 50f, circle: true);
            UiKit.Text(_style, card, "Guest", $"<b>{best.Guest}</b>", 20f, _muted, FontStyles.Normal, 78f, 12f, 120f, 28f);
            UiKit.Stars(card, 190f, 16f, 16f, best.RatedStars, _gold, _muted);
            UiKit.Text(_style, card, "Quote", best.Line, 22f, _ink, FontStyles.Bold, 78f, 42f, 210f, 44f);
        }

        private string Hints()
        {
            switch (_openPage)
            {
                case Page.Settings:
                    return $"{Key("LS")} Choose and change     {Key("B")} Back";
                case Page.ConfirmDelete:
                    return $"{Key("A")} Delete     {Key("B")} Keep it";
                case Page.Credits:
                case Page.SlotsFull:
                    return $"{Key("B")} Back";
            }
            var hints = $"{Key("LS")} Browse     {Key("A")} Select     ";
            if (_onCards)
            {
                hints += $"{Key("B")} Back     ";
                if (_saves[_card] != null)
                    hints += $"{Key("Y")} Delete slot";
            }
            return hints.TrimEnd();
        }

        // Only the Xbox names, never keyboard keys (the user's standing rule).
        private string Key(string button) => $"<b>{HudStyle.Colored($"[{button}]", _gold)}</b>";

        private void Badge(RectTransform parent, string button, float x, float y, float size)
        {
            var colour = button == "A" ? _good : button == "B" ? _alarmText : _gold;
            var badge = UiKit.Box(_style, parent, button, colour, x, y, size, size, circle: true);
            UiKit.Text(_style, badge, "Letter", button, size * 0.55f, _paper, FontStyles.Bold, 0f, 0f, size, size).alignment = TextAlignmentOptions.Center;
        }

        private void Tag(RectTransform parent, string text, Color back, Color fore, float x, float y)
        {
            var tag = UiKit.Box(_style, parent, text, back, x, y, 0f, 30f);
            var label = UiKit.Text(_style, tag, "Text", text, 16f, fore, FontStyles.Bold, 10f, 0f, 200f, 30f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.characterSpacing = 2f;
            tag.sizeDelta = new Vector2(label.preferredWidth + 20f, 30f);
        }

        private float Chip(RectTransform parent, string text, Color back, Color fore, float x, float y)
        {
            var chip = UiKit.Box(_style, parent, text, back, x, y, 0f, 34f);
            var label = UiKit.Text(_style, chip, "Text", text, 19f, fore, FontStyles.Bold, 12f, 0f, 240f, 34f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            chip.sizeDelta = new Vector2(label.preferredWidth + 24f, 34f);
            return x + chip.sizeDelta.x;
        }

        // ---- Building ----

        private void Build()
        {
            var canvasObject = new GameObject("MenuCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvas = canvasObject.GetComponent<RectTransform>();

            // A fixed 1920×1080 layout, scaled to fit inside the screen (HUD never clipped).
            var root = UiKit.Rect(_canvas, "Layout", 0f, 0f, 1920f, 1080f);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;

            // The sign.
            var sign = UiKit.Box(_style, root, "Sign", _night, 100f, 130f, 630f, 260f);
            UiKit.Outline(_style, sign, _gold, 5f);
            UiKit.Text(_style, sign, "Mise", "Mise en", 64f, _gold, FontStyles.Italic | FontStyles.Bold, 70f, 22f, 420f, 80f).transform.localRotation = Quaternion.Euler(0f, 0f, 4f);
            UiKit.Text(_style, sign, "Flex", "FLEX", 150f, _paper, FontStyles.Bold, 96f, 76f, 520f, 170f);
            var rec = UiKit.Text(_style, sign, "Rec", HudStyle.Colored("● ", new Color(0.9f, 0.25f, 0.3f)) + "REC", 22f, new Color(0.9f, 0.25f, 0.3f), FontStyles.Bold, 500f, 26f, 100f, 30f);
            rec.characterSpacing = 4f;

            _sticker = UiKit.Rect(root, "StickerHolder", 470f, 352f, 300f, 92f);
            _sticker.localRotation = Quaternion.Euler(0f, 0f, 4f);

            var listPanel = UiKit.Box(_style, root, "List", _night, 100f, 436f, 630f, 480f);
            _list = UiKit.Rect(listPanel, "Items", 0f, 0f, 630f, 480f);

            // forkcast: the slots.
            var app = UiKit.Box(_style, root, "App", _paper, 800f, 128f, 1024f, 788f);
            var header = UiKit.Box(_style, app, "Header", _night, 0f, 0f, 1024f, 74f, rounded: false);
            UiKit.Box(_style, header, "Mark", _gold, 30f, 21f, 32f, 32f, circle: true);
            UiKit.Box(_style, header, "MarkHole", _night, 35f, 26f, 22f, 22f, circle: true);
            UiKit.Box(_style, header, "MarkDot", _gold, 41f, 32f, 10f, 10f, circle: true);
            UiKit.Text(_style, header, "Logo", "forkcast", 30f, _soft, FontStyles.Bold, 72f, 0f, 160f, 74f).alignment = TextAlignmentOptions.MidlineLeft;
            var search = UiKit.Box(_style, header, "Search", new Color(1f, 1f, 1f, 0.08f), 250f, 14f, 520f, 46f);
            UiKit.Text(_style, search, "Text", "Your spots · dishes · @chefs", 22f, _soft, FontStyles.Normal, 24f, 0f, 480f, 46f).alignment = TextAlignmentOptions.MidlineLeft;
            var pill = UiKit.Box(_style, header, "Slots", new Color(1f, 1f, 1f, 0.08f), 800f, 14f, 196f, 46f);
            _slotsPill = UiKit.Text(_style, pill, "Text", "", 22f, _paper, FontStyles.Bold, 0f, 0f, 196f, 46f);
            _slotsPill.alignment = TextAlignmentOptions.Center;
            _cards = UiKit.Rect(app, "Cards", 0f, 74f, 1024f, 714f);

            _page = UiKit.Rect(root, "Page", 800f, 128f, 1024f, 788f);

            var footer = UiKit.Box(_style, root, "Footer", _night, 0f, 1016f, 1920f, 64f, rounded: false);
            _hints = UiKit.Text(_style, footer, "Hints", "", 26f, _paper, FontStyles.Normal, 40f, 0f, 1840f, 64f);
            _hints.alignment = TextAlignmentOptions.MidlineRight;

            var margin = _style.ScreenMargin * 2f;
            var scale = Mathf.Min(1f, (1920f - margin) / 1920f, (1080f - margin) / 1080f);
            root.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
