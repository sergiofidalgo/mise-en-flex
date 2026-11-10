using System.Collections.Generic;
using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Saving;
using Game.Gameplay.Stations;
using Game.Presentation.Hud;
using Game.Simulation.Days;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Presentation.Menu
{
    /// <summary>
    /// The pause menu (Phase 5, milestone 6; the user's calls, 2026-11-10). A tap of Menu opens it
    /// anywhere players are: the office, Prep, service and Review (a hold of Menu readies up instead,
    /// <see cref="MenuButton"/>). It freezes the game for everyone, and only the player who opened it
    /// drives it, in their colour; everyone else's controls stand still until it closes. It holds
    /// Resume, Settings (the realism preset, the language and the five dials under Advanced), Quit to
    /// the office (from the kitchen) and Quit to the main menu (the game itself is quit from there).
    /// Leaving the kitchen mid-day loses that day, so both quits say so first.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        private enum Page
        {
            Main,
            Settings,
            ConfirmOffice,
            ConfirmQuit,
        }

        private enum Item
        {
            Resume,
            Settings,
            QuitToOffice,
            QuitGame,
        }

        private const string UiMap = "UI";

        [SerializeField] private HudStyle _style;
        [SerializeField] private RealismSettings _realism;
        [SerializeField] private CoopSettings _coop;
        [Tooltip("The kitchen's root: offers Quit to the office and knows whether today is unsaved. Empty in the office.")]
        [SerializeField] private KitchenCompositionRoot _root;
        [Tooltip("The kitchen's day loop, which stops time for the pause. Empty in the office, where the menu stops time itself.")]
        [SerializeField] private DayLoop _dayLoop;
        [SerializeField] private string _officeScene = "Office_Greybox";

        [Header("Feel")]
        [SerializeField, Range(0.1f, 1f)] private float _stickThreshold = 0.5f;
        [Tooltip("Seconds between steps while the stick is held.")]
        [SerializeField, Min(0.05f)] private float _stickRepeat = 0.2f;
        [Tooltip("Seconds Menu is held to ready up (Plan, Prep, Review); a shorter press opens this menu.")]
        [SerializeField, Min(0.1f)] private float _holdToReady = 0.6f;

        [Header("Look")]
        [Tooltip("Dims the game behind the menu. The project is in Linear colour space, where UI blends in linear light: 0.6 kept two thirds of the apparent brightness and barely read as dimmed, 0.85 keeps under half.")]
        [SerializeField] private Color _veil = new Color(0.04f, 0.04f, 0.06f, 0.85f);
        [SerializeField] private Color _rowColor = new Color(1f, 1f, 1f, 0.06f);
        [SerializeField] private Color _quietText = new Color(0.66f, 0.68f, 0.72f);
        [SerializeField] private Color _warning = new Color(0.96f, 0.76f, 0.25f);

        private readonly List<(InputActionMap Map, bool WasEnabled)> _maps = new List<(InputActionMap, bool)>();
        private SettingsModel _settings;
        private RectTransform _canvas;
        private RectTransform _card;
        private PlayerInput _owner;
        private int _ownerIndex;
        // Set only by DrawForCapture: what the kitchen would say, without a running kitchen.
        private bool? _captureUnsaved;
        private int _captureDay;
        private Page _page;
        private int _row;
        private bool _open;
        private bool _dirty;
        private int _openedFrame;
        private float _nextStep;
        private float _timeScaleBefore = 1f;
        private bool _joiningBefore;

        /// <summary>True while the menu is up.</summary>
        public bool IsOpen => _open;

        private void Start()
        {
            MenuButton.HoldSeconds = _holdToReady;
            _settings = new SettingsModel(_realism, _style);
            _settings.LanguageChanged += () => _dirty = true;
            Build();
            _canvas.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (_open)
                Close();
        }

        private void Update()
        {
            if (!_open)
            {
                ReadOpen();
                return;
            }

            // The player driving it has gone (controller lost, left): close, and a lost controller's
            // own pause takes over in service.
            if (_owner == null || PlayerPresence.IsLost(_owner))
            {
                Close();
                return;
            }

            if (Time.frameCount != _openedFrame)
                ReadInput();
            if (_open && _dirty)
            {
                _dirty = false;
                Redraw();
            }
        }

        // ---- Opening and closing ----

        private void ReadOpen()
        {
            PlayerInput opener = null;
            // Everyone's button is read every frame, so each one's tap/hold state stays current.
            foreach (var player in PlayerInput.all)
            {
                if (MenuButton.Read(player) != MenuPress.Result.Tap || opener != null || PlayerPresence.IsLost(player))
                    continue;
                opener = player;
            }
            // While a lost controller holds the service, Menu carries on without them (DayLoop).
            if (opener != null && (_dayLoop == null || !_dayLoop.ControllerPause.Paused))
                Open(opener);
        }

        private void Open(PlayerInput owner)
        {
            _open = true;
            _owner = owner;
            _ownerIndex = owner.playerIndex;
            _openedFrame = Time.frameCount;
            _page = Page.Main;
            _row = 0;
            _dirty = true;
            GamePause.MenuOpen = true;

            // Everyone's controls stand still; the opener drives the menu with the UI map.
            _maps.Clear();
            foreach (var player in PlayerInput.all)
            {
                if (player.actions == null)
                    continue;
                foreach (var map in player.actions.actionMaps)
                {
                    _maps.Add((map, map.enabled));
                    map.Disable();
                }
            }
            _owner.actions.FindActionMap(UiMap)?.Enable();

            // Nobody joins mid-pause.
            var manager = PlayerInputManager.instance;
            _joiningBefore = manager != null && manager.joiningEnabled;
            if (_joiningBefore)
                manager.DisableJoining();

            // The kitchen's day loop stops time with the same switch as a lost controller.
            if (_dayLoop == null)
            {
                _timeScaleBefore = Time.timeScale;
                Time.timeScale = 0f;
            }
            AudioListener.pause = true;
            _canvas.gameObject.SetActive(true);
            Redraw();
        }

        private void Close()
        {
            _open = false;
            GamePause.MenuOpen = false;
            foreach (var (map, wasEnabled) in _maps)
            {
                if (map == null)
                    continue;
                if (wasEnabled)
                    map.Enable();
                else
                    map.Disable();
            }
            _maps.Clear();
            var manager = PlayerInputManager.instance;
            if (_joiningBefore && manager != null)
                manager.EnableJoining();
            if (_dayLoop == null)
                Time.timeScale = _timeScaleBefore > 0f ? _timeScaleBefore : 1f;
            AudioListener.pause = false;
            // The button that closed it mustn't open it again, or ready anyone up, until it's let go.
            MenuButton.DisarmAll();
            _owner = null;
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
        }

        // ---- Input ----

        private void ReadInput()
        {
            var ui = _owner.actions.FindActionMap(UiMap);
            var stick = ui?.FindAction("Navigate")?.ReadValue<Vector2>() ?? Vector2.zero;
            var south = ui?.FindAction("Submit")?.WasPressedThisFrame() ?? false;
            var east = ui?.FindAction("Cancel")?.WasPressedThisFrame() ?? false;
            var menu = MenuPressedOnOwner();

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

            // Menu closes it from any page.
            if (menu)
            {
                Close();
                return;
            }

            switch (_page)
            {
                case Page.Main:
                    if (east)
                    {
                        Close();
                        return;
                    }
                    if (step.y != 0)
                    {
                        var items = Items();
                        _row = Mathf.Clamp(_row + step.y, 0, items.Count - 1);
                        _dirty = true;
                    }
                    if (south)
                        Choose(Items()[_row]);
                    break;
                case Page.Settings:
                    if (!_settings.Handle(step, south, east))
                    {
                        _page = Page.Main;
                        _row = Items().IndexOf(Item.Settings);
                    }
                    if (step != Vector2Int.zero || south || east)
                        _dirty = true;
                    break;
                case Page.ConfirmOffice:
                case Page.ConfirmQuit:
                    if (south)
                    {
                        if (_page == Page.ConfirmOffice)
                            QuitToOffice();
                        else
                            QuitGame();
                    }
                    else if (east)
                    {
                        _row = Items().IndexOf(_page == Page.ConfirmOffice ? Item.QuitToOffice : Item.QuitGame);
                        _page = Page.Main;
                        _dirty = true;
                    }
                    break;
            }
        }

        // Menu on the opener's own controller (Start) or Esc on the keyboard; their Gameplay map is off.
        private bool MenuPressedOnOwner()
        {
            foreach (var device in _owner.devices)
            {
                if (device is Gamepad pad && pad.startButton.wasPressedThisFrame)
                    return true;
            }
            return false;
        }

        private List<Item> Items()
        {
            var items = new List<Item> { Item.Resume, Item.Settings };
            if (_root != null)
                items.Add(Item.QuitToOffice);
            items.Add(Item.QuitGame);
            return items;
        }

        private bool UnsavedDay => _captureUnsaved ?? (_root != null && _root.HasUnsavedDay);

        private void Choose(Item item)
        {
            switch (item)
            {
                case Item.Resume:
                    Close();
                    return;
                case Item.Settings:
                    _settings.Reload();
                    _page = Page.Settings;
                    break;
                case Item.QuitToOffice:
                    if (UnsavedDay)
                        _page = Page.ConfirmOffice;
                    else
                        QuitToOffice();
                    break;
                case Item.QuitGame:
                    if (UnsavedDay)
                        _page = Page.ConfirmQuit;
                    else
                        QuitGame();
                    break;
            }
            _dirty = true;
        }

        // Back up to the boss's floor with the same players. The kitchen loads the slot's last save
        // next time, so a day left unfinished starts again (decision 2026-11-06).
        private void QuitToOffice()
        {
            PlayerRoster.CarryEveryone();
            CampaignLaunch.Continue(CampaignLaunch.Slot);
            Close();
            Time.timeScale = 1f;
            SceneManager.LoadScene(_officeScene);
        }

        // Back to the main menu (the user's call, 2026-11-10: quitting from the kitchen or the office
        // takes players to the main menu, not out of the game; Quit there closes it). The office scene
        // loads afresh and its opening starts at the menu: no campaign chosen, nobody carried over.
        private void QuitGame()
        {
            Close();
            Time.timeScale = 1f;
            PlayerRoster.Take();
            CampaignLaunch.Reset();
            Office.OpeningSequence.StartAtMenu = true;
            SceneManager.LoadScene(_officeScene);
        }

        // ---- Drawing (1920×1080, centred; the card sits mid-screen so no edge clips it) ----

        private const float CardWidth = 1040f;
        private const float CardHeight = 820f;
        private const float CardX = (1920f - CardWidth) / 2f;
        private const float CardY = (1080f - CardHeight) / 2f;
        private const float Pad = 56f;

        private void Build()
        {
            var canvasObject = new GameObject("PauseCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // over every HUD
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Expand: the whole card always fits, on 16:10 MacBook screens too.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            _canvas = canvasObject.GetComponent<RectTransform>();

            var layout = UiKit.Rect(_canvas, "Layout", 0f, 0f, 1920f, 1080f);
            layout.anchorMin = layout.anchorMax = layout.pivot = new Vector2(0.5f, 0.5f);
            layout.anchoredPosition = Vector2.zero;
            // The veil reaches well past the 1920×1080 frame, so any screen shape is dimmed edge to edge.
            UiKit.RoundBox(layout, "Veil", _veil, -1920f, -1080f, 1920f * 3f, 1080f * 3f, 0f);
            _card = UiKit.Rect(layout, "Card", 0f, 0f, 1920f, 1080f);
        }

        private Color OwnerColor => _coop != null ? _coop.GetPlayerColor(_ownerIndex) : Color.white;

        private TMP_FontAsset Words => _style.ExtraBold;
        private TMP_FontAsset Numbers => _style.DisplayFont;

        private void Redraw()
        {
            UiKit.Clear(_card);
            var owner = OwnerColor;
            // Opaque, so the border's colour doesn't show through the card.
            var fill = _style.CardBackground;
            fill.a = 1f;
            UiKit.StrokedBox(_card, "Card", fill, owner, 4f, CardX, CardY, CardWidth, CardHeight, 24f);

            var title = _page switch
            {
                Page.Settings => _settings.Title,
                Page.ConfirmOffice => Tr.Get("Quit to the office?"),
                Page.ConfirmQuit => Tr.Get("Quit to the main menu?"),
                _ => Tr.Get("Paused"),
            };
            UiKit.TextAt(Words, _card, "Title", title, 48f, _style.TextColor, CardX + Pad, CardY + 92f, CardWidth - 2f * Pad - 120f);
            // Whose menu it is, in their colour.
            UiKit.RoundBox(_card, "Owner", owner, CardX + CardWidth - Pad - 88f, CardY + 50f, 88f, 52f, 26f);
            UiKit.TextAt(Numbers, _card, "OwnerText", $"P{_ownerIndex + 1}", 32f, _style.CardBackground, CardX + CardWidth - Pad - 44f, CardY + 88f, 88f, HorizontalAlignmentOptions.Center);

            var hints = new List<(PromptControl? Control, string Label)>();
            switch (_page)
            {
                case Page.Main:
                    DrawMain();
                    hints.Add((PromptControl.DPad, Tr.Get("Choose")));
                    hints.Add((PromptControl.South, Tr.Get("Select")));
                    hints.Add((PromptControl.East, Tr.Get("Resume")));
                    break;
                case Page.Settings:
                    DrawSettings();
                    hints.Add((PromptControl.DPad, Tr.Get("Choose and change")));
                    if (_settings.Rows[_settings.Selected].Opens)
                        hints.Add((PromptControl.South, Tr.Get("Open")));
                    hints.Add((PromptControl.East, Tr.Get("Back")));
                    break;
                default:
                    DrawConfirm();
                    hints.Add((PromptControl.South, _page == Page.ConfirmOffice ? Tr.Get("Go to the office") : Tr.Get("Go to the menu")));
                    hints.Add((PromptControl.East, Tr.Get("Keep playing")));
                    break;
            }
            DrawHints(hints);
        }

        private string ItemName(Item item) => item switch
        {
            Item.Resume => Tr.Get("Resume"),
            Item.Settings => Tr.Get("Settings"),
            Item.QuitToOffice => Tr.Get("Quit to the office"),
            _ => Tr.Get("Quit to the main menu"),
        };

        private void DrawMain()
        {
            var items = Items();
            var y = CardY + 160f;
            for (var i = 0; i < items.Count; i++)
            {
                var selected = i == _row;
                UiKit.RoundBox(_card, "Row", selected ? Alpha(OwnerColor, 0.28f) : _rowColor, CardX + Pad, y, CardWidth - 2f * Pad, 92f, 16f);
                if (selected)
                    UiKit.RoundBox(_card, "Mark", OwnerColor, CardX + Pad, y, 10f, 92f, 5f);
                UiKit.TextAt(Words, _card, "Item", ItemName(items[i]), 36f, _style.TextColor, CardX + Pad + 40f, y + 60f, CardWidth - 2f * Pad - 80f);
                y += 112f;
            }
            if (UnsavedDay)
                UiKit.TextAt(Words, _card, "Unsaved", Tr.Get("Today isn't saved until Review starts."), 28f, _quietText, CardX + Pad, y + 44f, CardWidth - 2f * Pad);
        }

        private void DrawSettings()
        {
            var rows = _settings.Rows;
            var pitch = rows.Count > 4 ? 110f : 124f;
            var y = CardY + 140f;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var selected = i == _settings.Selected;
                UiKit.RoundBox(_card, "Row", selected ? Alpha(OwnerColor, 0.28f) : _rowColor, CardX + Pad, y, CardWidth - 2f * Pad, pitch - 14f, 16f);
                if (selected)
                    UiKit.RoundBox(_card, "Mark", OwnerColor, CardX + Pad, y, 10f, pitch - 14f, 5f);
                UiKit.TextAt(Words, _card, "Name", row.Name, 32f, _style.TextColor, CardX + Pad + 32f, y + 44f, 460f);
                UiKit.TextAt(Words, _card, "What", row.What, 28f, _quietText, CardX + Pad + 32f, y + 82f, CardWidth - 2f * Pad - 64f);
                var value = row.Opens ? "›" : selected ? $"‹  {row.Value}  ›" : row.Value;
                UiKit.TextAt(Numbers, _card, "Value", value, 32f, _style.TextColor, CardX + CardWidth - Pad - 32f, y + 44f, 320f, HorizontalAlignmentOptions.Right);
                y += pitch;
            }
            if (!_settings.Advanced)
                UiKit.TextAt(Words, _card, "Note", Tr.Get("For every campaign. Saved as you change them."), 28f, _quietText, CardX + Pad, y + 40f, CardWidth - 2f * Pad);
        }

        private void DrawConfirm()
        {
            var day = _captureUnsaved.HasValue ? _captureDay : _root != null ? _root.Services.Days.Day + 1 : 1;
            var body = _page == Page.ConfirmOffice
                ? Tr.Get("You'll go back up to the boss's floor.")
                : Tr.Get("The game closes.");
            UiKit.TextAt(Words, _card, "Body", body, 32f, _style.TextColor, CardX + Pad, CardY + 220f, CardWidth - 2f * Pad, lines: 3);
            UiKit.TextAt(Words, _card, "Warn", Tr.Format("Today isn't saved: Day {day} starts again from this morning.", ("day", day)), 32f, _warning, CardX + Pad, CardY + 330f, CardWidth - 2f * Pad, lines: 3);
            UiKit.TextAt(Words, _card, "Why", Tr.Get("The game saves as Review starts."), 28f, _quietText, CardX + Pad, CardY + 470f, CardWidth - 2f * Pad, lines: 2);
        }

        // Laid out from the card's right edge leftwards. Only Xbox buttons are named (the user's rule).
        private void DrawHints(List<(PromptControl? Control, string Label)> hints)
        {
            var right = CardX + CardWidth - Pad;
            var baseline = CardY + CardHeight - 44f;
            for (var i = hints.Count - 1; i >= 0; i--)
            {
                var label = UiKit.TextAt(Words, _card, "Hint", hints[i].Label, 28f, _style.TextColor, 0f, baseline, 400f);
                var width = UiKit.Width(label);
                label.rectTransform.anchoredPosition = new Vector2(right - width, label.rectTransform.anchoredPosition.y);
                right -= width + 12f;
                var control = hints[i].Control ?? PromptControl.South;
                var glyph = HudStyle.Glyph(control, true);
                var quiet = control == PromptControl.LeftStick || control == PromptControl.DPad;
                var badgeWidth = quiet ? 64f : 44f;
                var colour = quiet ? _quietText : _style.BadgeColor(control, true);
                UiKit.RoundBox(_card, "Badge", colour, right - badgeWidth, baseline - 34f, badgeWidth, 44f, 22f);
                if (control == PromptControl.DPad)
                    UiKit.DPad(_card, _style.CardBackground, right - badgeWidth / 2f, baseline - 12f, 28f);
                else
                    UiKit.TextAt(Numbers, _card, "Glyph", glyph, 28f, _style.CardBackground, right - badgeWidth / 2f, baseline - 2f, badgeWidth, HorizontalAlignmentOptions.Center);
                right -= badgeWidth + 36f;
            }
        }

        /// <summary>The pages a capture can draw.</summary>
        public enum CapturePage
        {
            Main,
            Settings,
            Advanced,
            ConfirmOffice,
        }

        /// <summary>
        /// Draws one page at once, without Play mode, for the capture tool (Tools/Game/Capture Pause
        /// Menu): as player <paramref name="ownerIndex"/> would see it, on day <paramref name="day"/>.
        /// </summary>
        public Canvas DrawForCapture(CapturePage page, int ownerIndex, bool unsavedDay, int day, int row = 0)
        {
            if (_canvas != null)
                DestroyImmediate(_canvas.gameObject);
            if (_style != null && _style.Languages != null)
                _style.Languages.Reload();
            _settings = new SettingsModel(_realism, _style);
            Build();
            _ownerIndex = ownerIndex;
            _captureUnsaved = unsavedDay;
            _captureDay = day;
            _row = row;
            switch (page)
            {
                case CapturePage.Settings:
                    _page = Page.Settings;
                    _settings.Handle(new Vector2Int(0, row), false, false);
                    break;
                case CapturePage.Advanced:
                    _page = Page.Settings;
                    _settings.Handle(new Vector2Int(0, 2), false, false); // down to Advanced
                    _settings.Handle(Vector2Int.zero, true, false);       // and open it
                    _settings.Handle(new Vector2Int(0, row), false, false);
                    break;
                case CapturePage.ConfirmOffice:
                    _page = Page.ConfirmOffice;
                    break;
                default:
                    _page = Page.Main;
                    break;
            }
            Redraw();
            _captureUnsaved = null;
            return _canvas.GetComponent<Canvas>();
        }

        private static Color Alpha(Color colour, float alpha) => new Color(colour.r, colour.g, colour.b, alpha);
    }
}
