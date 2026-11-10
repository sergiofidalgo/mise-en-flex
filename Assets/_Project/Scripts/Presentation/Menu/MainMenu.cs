using System.Linq;
using System;
using Game.Data;
using Game.Gameplay.Saving;
using Game.Presentation.Hud;
using Game.Simulation.Saving;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game.Presentation.Menu
{
    /// <summary>
    /// The main menu (decision 2026-11-08, after the user's mockup; drawn to the Art Director's
    /// main-menu v11 and new-player mockups). On the left are the sign and the list: Continue, New
    /// campaign, Free kitchen (later), Settings, Credits and Quit. On the right, forkcast shows the
    /// three campaign slots as cards. Any controller (or the keyboard's stand-in keys) drives it,
    /// because nobody has joined yet. It's built in code, over the 3D night city. This file holds
    /// the state and input; MainMenu.Drawing.cs draws it.
    /// </summary>
    public sealed partial class MainMenu : MonoBehaviour
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
        [Tooltip("The kitchen for a restaurant without one of its own (the grill's), and for a new campaign.")]
        [SerializeField] private string _kitchenScene = "Kitchen_Greybox";
        [Tooltip("Every restaurant: continuing opens the current one's own kitchen (Phase 6, milestone 3).")]
        [SerializeField] private MiniatureConfig _restaurants;
        [Tooltip("Start hidden and ignoring input, for a scene that shows the menu when it's ready (the office's opening, docs/opening-flow.md).")]
        [SerializeField] private bool _startHidden;

        [Header("Feel")]
        [SerializeField, Range(0.1f, 1f)] private float _stickThreshold = 0.5f;
        [Tooltip("Seconds between steps while the stick is held.")]
        [SerializeField, Min(0.05f)] private float _stickRepeat = 0.22f;

        [Header("Text")]
        [SerializeField, TextArea] private string _credits = "Mise en Flex\n\nA local co-op restaurant game.\n\nFull credits to come.";

        private CampaignSave[] _saves;
        private int _lastPlayed;
        private PlayerSettings _settings;

        private bool _onCards;
        private Item _item;
        private int _card;
        private Page _openPage;
        private SettingsModel _settingsPage;
        private float _nextStep;
        private bool _dirty = true;
        private bool _shown = true;
        private CanvasGroup _group;

        /// <summary>
        /// Raised with the save slot and whether it's continuing, when a scene takes over what happens
        /// after a campaign is chosen (the office's opening climbs to the floor). With no listener, the
        /// menu loads the kitchen as before.
        /// </summary>
        public event System.Action<int, bool> Launching;

        /// <summary>The controller (or keyboard) that last pressed a button in the menu: the opening joins it as player 1.</summary>
        public static InputDevice LastDevice { get; private set; }

        private void Start()
        {
            _settings = PlayerSettings.Load(_realism);
            ApplyLanguage();
            ReadSlots();
            _item = _lastPlayed >= 0 ? Item.Continue : Item.NewCampaign;
            Build();
            if (_startHidden)
                SetShown(false, 0f);
        }

        /// <summary>
        /// Shows or hides the menu: <paramref name="alpha"/> fades it, and while hidden it ignores input.
        /// </summary>
        public void SetShown(bool shown, float alpha)
        {
            _shown = shown;
            if (_canvas == null)
                return;
            if (_group == null)
                _group = _canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = alpha;
            _group.blocksRaycasts = shown;
        }

        /// <summary>
        /// Draws the menu at once, without Play mode, for the capture tool: reads the slots from
        /// wherever <see cref="SaveFile.DirectoryOverride"/> points and focuses as asked.
        /// </summary>
        public Canvas DrawForCapture(bool onCards = false, int card = 0)
        {
            if (_canvas != null)
                DestroyImmediate(_canvas.gameObject);
            _settings = PlayerSettings.Load(_realism);
            ApplyLanguage();
            ReadSlots();
            _item = _lastPlayed >= 0 ? Item.Continue : Item.NewCampaign;
            _onCards = onCards;
            _card = Mathf.Clamp(card, 0, SaveSlots.Count - 1);
            _openPage = Page.None;
            Build();
            Redraw();
            return _canvas.GetComponent<Canvas>();
        }

        // The saved language, for names, money and every string (decision 2026-11-09).
        private void ApplyLanguage()
        {
            if (_style.Languages != null)
                _style.Languages.Load();
            Game.Data.Locale.Language = string.IsNullOrEmpty(_settings.Language) ? "en" : _settings.Language;
        }

        private void ReadSlots()
        {
            _saves = SaveSlots.ReadAll();
            _lastPlayed = SaveSlots.LastPlayed(_saves);
            // Reviews saved as English sentences are traced to their templates, to show in any language.
            foreach (var save in _saves)
            {
                foreach (var restaurant in save?.Restaurants ?? new System.Collections.Generic.List<RestaurantSave>())
                    Game.Simulation.Service.ReviewTextRecovery.Recover(restaurant.Reviews, _service.StyleVoice(), _service.GenericVoice());
            }
            _dirty = true;
        }

        private void Update()
        {
            if (!_shown)
                return;
            AnimateSign();
            ReadInput();
            if (!_dirty)
                return;
            _dirty = false;
            Redraw();
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
                        if (!IsAvailable(_item))
                        {
                            // The last campaign went: nothing to continue, so back to the list.
                            _item = Item.NewCampaign;
                            _onCards = false;
                        }
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
                    _card = Mathf.Clamp(_card + step.x, 0, SaveSlots.Count - 1);
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
                    Launch(_card, _saves[_card] != null);
                }
                return;
            }

            if (step.y != 0)
            {
                var items = (Item[])Enum.GetValues(typeof(Item));
                var index = Array.IndexOf(items, _item);
                // Skip what can't be chosen (Continue with no campaign, Free kitchen until it's built).
                var next = index + step.y;
                while (next >= 0 && next < items.Length && !IsAvailable(items[next]))
                    next += step.y;
                if (next >= 0 && next < items.Length)
                    _item = items[next];
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
                    // The campaigns are a page of their own (the user's call, 2026-11-10), so the top
                    // level leaves the scene behind it in view: Continue opens them on the last played.
                    if (_lastPlayed >= 0)
                    {
                        _onCards = true;
                        _card = _lastPlayed;
                        _dirty = true;
                    }
                    break;
                case Item.NewCampaign:
                    var free = SaveSlots.FirstFree(_saves);
                    if (free < 0)
                        Open(Page.SlotsFull);
                    else
                        Launch(free, false);
                    break;
                case Item.Settings:
                    // The same settings as the pause menu (decision 2026-11-10).
                    _settingsPage ??= NewSettingsPage();
                    _settingsPage.Reload();
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
            if (Launching != null)
            {
                _shown = false;
                Launching(slot, continuing);
                return;
            }
            SceneManager.LoadScene(continuing ? CampaignLaunch.KitchenScene(slot, _restaurants, _kitchenScene) : _kitchenScene);
        }

        private void Open(Page page)
        {
            _openPage = page;
            _dirty = true;
        }

        private SettingsModel NewSettingsPage()
        {
            var page = new SettingsModel(_realism, _style);
            page.LanguageChanged += () =>
            {
                _settings = page.Settings;
                ApplyLanguage();
                ReadSlots(); // reviews and names in the new language
            };
            return page;
        }

        // The shared settings rows, adjusted with the stick and saved at once.
        private void SettingsInput(Vector2Int step)
        {
            var south = Pressed(Face.South);
            var east = Pressed(Face.East);
            if (!_settingsPage.Handle(step, south, east))
            {
                _settings = _settingsPage.Settings;
                Open(Page.None);
                return;
            }
            if (step != Vector2Int.zero || south || east)
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
                {
                    LastDevice = pad;
                    return true;
                }
            }
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;
            var pressed = face switch
            {
                Face.South => keyboard.xKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame,
                Face.East => keyboard.dKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame,
                _ => keyboard.wKey.wasPressedThisFrame,
            };
            if (pressed)
                LastDevice = keyboard;
            return pressed;
        }

        private static Vector2 Stick()
        {
            foreach (var pad in Gamepad.all)
            {
                var value = pad.dpad.ReadValue(); // not the stick: too loose for a list (2026-11-10)
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
    }
}
