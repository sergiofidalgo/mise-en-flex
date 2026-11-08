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
        [SerializeField] private string _kitchenScene = "Kitchen_Greybox";

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
        private int _settingRow;
        private float _nextStep;
        private bool _dirty = true;

        private void Start()
        {
            _settings = PlayerSettings.Load(_realism);
            ReadSlots();
            _item = _lastPlayed >= 0 ? Item.Continue : Item.NewCampaign;
            Build();
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
            ReadSlots();
            _item = _lastPlayed >= 0 ? Item.Continue : Item.NewCampaign;
            _onCards = onCards;
            _card = Mathf.Clamp(card, 0, SaveSlots.Count - 1);
            _openPage = Page.None;
            Build();
            Redraw();
            return _canvas.GetComponent<Canvas>();
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
                            _item = Item.NewCampaign;
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
            if (step.x > 0)
            {
                _onCards = true;
                _card = _item == Item.NewCampaign && SaveSlots.FirstFree(_saves) >= 0 ? SaveSlots.FirstFree(_saves) : Mathf.Max(0, _lastPlayed);
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
    }
}
