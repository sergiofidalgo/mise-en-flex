using Game.Gameplay;
using Game.Gameplay.Stations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// Two small cards around the one campaign (decisions 2026-11-08). At launch, with a campaign
    /// saved: Continue or New campaign, and New asks again since the old one is replaced. When
    /// quitting with an unsaved day: quitting loses today, so it asks first. Read from any controller
    /// (or the keyboard's face-button keys) because nobody has joined yet at launch. The boss's office
    /// replaces the launch card in Phase 6.
    /// </summary>
    public sealed class CampaignCardHud : MonoBehaviour
    {
        private enum Card
        {
            None,
            Launch,
            ConfirmNew,
            ConfirmQuit,
        }

        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenCompositionRoot _root;

        [SerializeField, Min(8f)] private float _fontSize = 28f;
        [SerializeField] private Vector2 _padding = new Vector2(40f, 28f);

        [Header("Text")]
        [SerializeField] private string _continueText = "Continue · Day {0}";
        [SerializeField] private string _newText = "New campaign";
        [SerializeField] private string _confirmNewText = "Start a new campaign?\nThe saved restaurant will be replaced.";
        [SerializeField] private string _confirmNewYes = "Start over";
        [SerializeField] private string _back = "Back";
        [SerializeField] private string _confirmQuitText = "Quit now?\nToday isn't saved: the game saves as Review starts.";
        [SerializeField] private string _confirmQuitYes = "Quit";
        [SerializeField] private string _stay = "Keep playing";

        private WorldLabel _label;
        private Card _card;
        private bool _quitConfirmed;

        private void Start()
        {
            _label = WorldLabel.Create(_style, _canvas, "CampaignCard", _padding, new Vector2(0.5f, 0.5f));
            _label.FontSize = _fontSize;
            _card = _root.AwaitingLaunchChoice ? Card.Launch : Card.None;
            Application.wantsToQuit += AllowQuit;
        }

        private void OnDestroy() => Application.wantsToQuit -= AllowQuit;

        // Quitting mid-day loses that day, so players are told first (decision 2026-11-06).
        private bool AllowQuit()
        {
            if (_quitConfirmed || !_root.HasUnsavedDay)
                return true;
            _card = Card.ConfirmQuit;
            return false;
        }

        private void Update()
        {
            switch (_card)
            {
                case Card.Launch:
                    _label.ShowAt($"{Badge(PromptControl.South)} {string.Format(_continueText, _root.SavedDay)}     {Badge(PromptControl.North)} {_newText}", Vector2.zero);
                    if (Pressed(South))
                        _root.ChooseContinue();
                    else if (Pressed(North))
                        _card = Card.ConfirmNew;
                    break;
                case Card.ConfirmNew:
                    Show(_confirmNewText, $"{Badge(PromptControl.South)} {_confirmNewYes}     {Badge(PromptControl.East)} {_back}");
                    if (Pressed(South))
                    {
                        _root.ChooseNewCampaign();
                        _card = Card.None;
                    }
                    else if (Pressed(East))
                    {
                        _card = Card.Launch;
                    }
                    break;
                case Card.ConfirmQuit:
                    Show(_confirmQuitText, $"{Badge(PromptControl.South)} {_confirmQuitYes}     {Badge(PromptControl.East)} {_stay}");
                    if (Pressed(South))
                    {
                        _quitConfirmed = true;
                        Application.Quit();
                    }
                    else if (Pressed(East))
                    {
                        _card = Card.None;
                    }
                    break;
                default:
                    _label.Hide();
                    break;
            }
        }

        private void Show(string heading, string choices) => _label.ShowAt($"{heading}\n\n{choices}", Vector2.zero);

        // Hints only ever name the Xbox buttons (the user's standing rule).
        private string Badge(PromptControl control) => HudStyle.Colored(HudStyle.Glyph(control, true), _style.BadgeColor(control, true));

        private enum Face
        {
            South,
            East,
            North,
        }

        private const Face South = Face.South;
        private const Face East = Face.East;
        private const Face North = Face.North;

        // Any controller, or the keyboard keys that stand in for the face buttons (X, D, W).
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
            var key = face == Face.South ? keyboard.xKey : face == Face.East ? keyboard.dKey : keyboard.wKey;
            return key.wasPressedThisFrame;
        }
    }
}
