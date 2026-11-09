using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Office;
using Game.Gameplay.Saving;
using Game.Gameplay.Stations;
using Game.Presentation.Hud;
using Game.Simulation.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The tailor's cards (decision 2026-11-09), one over each player at their plinth, in the house
    /// HUD style. Standing on it: "A Dress up". Dressing: what's being changed (with a dot per
    /// module), the item with arrows either side and its place in the list, a note when it's locked,
    /// and the controls. Also dresses the chef in what's being browsed, and back in what they wear
    /// when they step off. Cards stay fully on screen and only ever show Xbox controls.
    /// </summary>
    public sealed class TailorCardHud : MonoBehaviour
    {
        private const float Width = 380f;
        private const float DressingHeight = 196f;
        private const float PromptWidth = 220f;
        private const float PromptHeight = 64f;
        private const float Margin = 24f;
        private const float HeadHeight = 2.05f;

        [SerializeField] private HudStyle _style;
        [SerializeField] private CoopSettings _coop;
        [SerializeField] private TailorPlinth[] _plinths = System.Array.Empty<TailorPlinth>();

        private readonly Dictionary<TailorPlinth, Card> _cards = new Dictionary<TailorPlinth, Card>();
        private Canvas _canvas;

        private sealed class Card
        {
            public RectTransform Dressing;
            public RectTransform Prompt;
            public TextMeshProUGUI Module;
            public Image[] Dots;
            public TextMeshProUGUI Item;
            public TextMeshProUGUI Count;
            public TextMeshProUGUI Locked;
            public ChefWardrobe Wardrobe;
            public int Slot = -1;
            public Image PromptEdge;
            public Image DressingEdge;
            public ChefLook Shown;
            public string Text;
        }

        private static readonly string[] ModuleNames = { "Hat", "Face", "Skin", "Accessory", "Outfit" };

        private void Awake()
        {
            var canvasObject = new GameObject("TailorCards", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 20;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            foreach (var plinth in _plinths)
                if (plinth != null)
                    _cards[plinth] = Build(plinth);
        }

        private Card Build(TailorPlinth plinth)
        {
            var colour = Color.white; // set to the player's colour as they stand or dress (any player, any ladder)
            var card = new Card();

            card.Prompt = UiKit.Rect(_canvas.transform, "Prompt", 0f, 0f, PromptWidth, PromptHeight);
            UiKit.StrokedBox(card.Prompt, "Back", _style.CardBackground, colour, 4f, 0f, 0f, PromptWidth, PromptHeight, 18f);
            card.PromptEdge = card.Prompt.Find("BackEdge").GetComponent<Image>();
            Badge(card.Prompt, PromptControl.South, 16f, 14f);
            UiKit.TextAt(_style.Bold, card.Prompt, "Label", Tr.Get("Climb up"), 28f, _style.TextColor, 70f, 43f, PromptWidth - 84f);

            card.Dressing = UiKit.Rect(_canvas.transform, "Dressing", 0f, 0f, Width, DressingHeight);
            UiKit.StrokedBox(card.Dressing, "Back", _style.CardBackground, colour, 4f, 0f, 0f, Width, DressingHeight, 22f);
            card.DressingEdge = card.Dressing.Find("BackEdge").GetComponent<Image>();
            card.Module = UiKit.TextAt(_style.Bold, card.Dressing, "Module", "", 28f, _style.TextColor, 22f, 40f, 200f);
            card.Dots = new Image[ModuleNames.Length];
            for (var i = 0; i < card.Dots.Length; i++)
                card.Dots[i] = UiKit.Ellipse(card.Dressing, "Dot", Color.white, Width - 22f - (ModuleNames.Length - i) * 20f, 24f, 12f, 12f);
            UiKit.TextAt(_style.DisplayFont, card.Dressing, "Left", "‹", 40f, _style.TextColor, 22f, 94f, 30f);
            card.Item = UiKit.TextAt(_style.DisplayFont, card.Dressing, "Item", "", 34f, _style.TextColor, Width / 2f, 94f, Width - 110f, HorizontalAlignmentOptions.Center);
            UiKit.TextAt(_style.DisplayFont, card.Dressing, "Right", "›", 40f, _style.TextColor, Width - 22f, 94f, 30f, HorizontalAlignmentOptions.Right);
            card.Count = UiKit.TextAt(_style.Font, card.Dressing, "Count", "", 28f, _style.TextColor, Width / 2f, 128f, 200f, HorizontalAlignmentOptions.Center);
            card.Locked = UiKit.TextAt(_style.Bold, card.Dressing, "Locked", Tr.Get("Locked: earn it first"), 28f, new Color(0.85f, 0.35f, 0.25f), Width / 2f, 128f, Width - 40f, HorizontalAlignmentOptions.Center);
            Badge(card.Dressing, PromptControl.LeftStick, 22f, 146f);
            UiKit.TextAt(_style.Font, card.Dressing, "Browse", Tr.Get("Browse"), 28f, _style.TextColor, 70f, 177f, 120f);
            Badge(card.Dressing, PromptControl.East, Width / 2f + 20f, 146f);
            UiKit.TextAt(_style.Font, card.Dressing, "Done", Tr.Get("Done"), 28f, _style.TextColor, Width / 2f + 68f, 177f, 120f);

            card.Prompt.gameObject.SetActive(false);
            card.Dressing.gameObject.SetActive(false);
            return card;
        }

        // A round Xbox badge, as in the prompts (hints only ever name Xbox controls).
        private void Badge(RectTransform parent, PromptControl control, float x, float y)
        {
            var size = 38f;
            UiKit.Box(_style, parent, "Badge", _style.BadgeColor(control, true), x, y, size, size, circle: true);
            UiKit.TextAt(_style.DisplayFont, parent, "Glyph", HudStyle.Glyph(control, true), control == PromptControl.LeftStick ? 17f : 23f,
                Color.white, x + size / 2f, y + size * 0.68f, size, HorizontalAlignmentOptions.Center);
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            foreach (var pair in _cards)
            {
                var plinth = pair.Key;
                var card = pair.Value;
                var session = plinth.Session;
                var dresser = plinth.Dresser;

                if (session != null && dresser != null)
                {
                    card.Slot = plinth.DresserSlot;
                    card.DressingEdge.color = PlayerColour(card.Slot);
                    card.Wardrobe ??= dresser.GetComponentInChildren<ChefWardrobe>();
                    if (card.Wardrobe != null && (card.Shown == null || !card.Shown.SameAs(session.Preview)))
                    {
                        card.Shown = session.Preview.Copy();
                        card.Wardrobe.Show(card.Shown);
                    }
                    Fill(card, session);
                    Place(card.Dressing, camera, dresser.transform.position, Width, DressingHeight);
                    card.Dressing.gameObject.SetActive(true);
                    card.Prompt.gameObject.SetActive(false);
                    continue;
                }

                // Stepped off: back in what's worn, which drops a locked item still being looked at.
                if (card.Shown != null && card.Wardrobe != null && card.Slot >= 0)
                    card.Wardrobe.Show(ChefProfileFile.Current.Look(card.Slot));
                card.Shown = null;
                card.Wardrobe = null;
                card.Dressing.gameObject.SetActive(false);
                var standing = plinth.Standing;
                card.Prompt.gameObject.SetActive(standing != null);
                if (standing != null)
                {
                    card.PromptEdge.color = PlayerColour(standing.playerIndex);
                    Place(card.Prompt, camera, standing.transform.position, PromptWidth, PromptHeight);
                }
            }
        }

        private Color PlayerColour(int slot) => _coop != null && slot >= 0 ? _coop.GetPlayerColor(slot) : Color.white;

        private void Fill(Card card, TailorSession session)
        {
            var module = (int)session.Module;
            var item = session.Item;
            var name = session.Module == WardrobeModule.Skin
                ? Tr.Format("Tone {n}", ("n", session.ItemIndex + 1))
                : Tr.Get(item.Name);
            var text = $"{module}|{name}|{session.ItemIndex}|{session.ItemLocked}";
            if (text == card.Text)
                return;
            card.Text = text;
            card.Module.text = Tr.Get(ModuleNames[module]);
            for (var i = 0; i < card.Dots.Length; i++)
                card.Dots[i].color = i == module ? _style.TextColor : new Color(_style.TextColor.r, _style.TextColor.g, _style.TextColor.b, 0.25f);
            card.Item.text = name;
            card.Item.alpha = session.ItemLocked ? _style.UnavailableOpacity : 1f;
            card.Count.text = $"{session.ItemIndex + 1} / {session.ItemCount}";
            card.Count.gameObject.SetActive(!session.ItemLocked);
            card.Locked.gameObject.SetActive(session.ItemLocked);
        }

        // Above the chef's head, kept whole on screen (the HUD rule: nothing clipped by an edge).
        private void Place(RectTransform card, Camera camera, Vector3 feet, float width, float height)
        {
            if (camera == null)
                return;
            var screen = camera.WorldToScreenPoint(feet + Vector3.up * HeadHeight);
            var scale = _canvas.scaleFactor;
            var canvasSize = ((RectTransform)_canvas.transform).sizeDelta;
            var x = Mathf.Clamp(screen.x / scale - width / 2f, Margin, canvasSize.x - width - Margin);
            var y = Mathf.Clamp(canvasSize.y - screen.y / scale - height - 12f, Margin, canvasSize.y - height - Margin);
            card.anchoredPosition = new Vector2(x, -y);
        }
    }
}
