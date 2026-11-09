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
    /// The tailor's cards (decision 2026-11-09), one over each chef on a ladder, in the house HUD
    /// style with the player's colour on the edge. At the foot of a ladder: "A Climb up". Dressing:
    /// the section between LB and RB badges with a dot per section, the item between arrows with
    /// its place in the list (or a note when it's locked), and B Done. Every press answers at once
    /// (the user found the first version sluggish): the item pops, its arrow nudges, a new section
    /// pops, and the chef bounces as they try something on. Also dresses the chef in what's being
    /// browsed, and back in what they wear when they step off. Cards stay whole on screen, never
    /// overlap (four chefs on neighbouring ladders spread their cards apart) and only ever name Xbox
    /// controls.
    /// </summary>
    public sealed class TailorCardHud : MonoBehaviour
    {
        private const float Width = 440f;
        private const float DressingHeight = 214f;
        private const float PromptWidth = 220f;
        private const float PromptHeight = 64f;
        private const float Margin = 24f;
        private const float HeadHeight = 2.05f;
        private const float Badge = 40f;
        private const float PopTime = 0.16f;
        private const float CardGap = 12f;

        [SerializeField] private HudStyle _style;
        [SerializeField] private CoopSettings _coop;
        [SerializeField] private TailorPlinth[] _plinths = System.Array.Empty<TailorPlinth>();

        private readonly Dictionary<TailorPlinth, Card> _cards = new Dictionary<TailorPlinth, Card>();
        private readonly List<RectTransform> _placed = new List<RectTransform>();
        private readonly List<float> _lefts = new List<float>();
        private readonly List<float> _widths = new List<float>();
        private Canvas _canvas;

        private sealed class Card
        {
            public RectTransform Dressing;
            public RectTransform Prompt;
            public Image PromptEdge;
            public Image DressingEdge;
            public TextMeshProUGUI Section;
            public Image[] Dots;
            public TextMeshProUGUI Item;
            public RectTransform Left;
            public RectTransform Right;
            public TextMeshProUGUI Count;
            public TextMeshProUGUI Locked;
            public ChefWardrobe Wardrobe;
            public ChefLook Shown;
            public int Slot = -1;
            public string Text;
            public float ItemPop = 1f;     // 0 just changed, 1 settled
            public float SectionPop = 1f;
            public int Nudge;              // the arrow last pressed: -1, 0 or 1
        }

        private static readonly string[] SectionNames = { "Hat", "Face", "Skin", "Accessory", "Outfit" };

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
            {
                if (plinth == null)
                    continue;
                var card = Build();
                _cards[plinth] = card;
                plinth.Stepped += direction => HandleStepped(card, direction);
            }
        }

        private Card Build()
        {
            var card = new Card();

            card.Prompt = UiKit.Rect(_canvas.transform, "Prompt", 0f, 0f, PromptWidth, PromptHeight);
            UiKit.StrokedBox(card.Prompt, "Back", _style.CardBackground, Color.white, 4f, 0f, 0f, PromptWidth, PromptHeight, 18f);
            card.PromptEdge = card.Prompt.Find("BackEdge").GetComponent<Image>();
            DrawBadge(card.Prompt, PromptControl.South, 16f, 12f, Badge);
            UiKit.TextAt(_style.Bold, card.Prompt, "Label", Tr.Get("Climb up"), 28f, _style.TextColor, 68f, 43f, PromptWidth - 82f);

            card.Dressing = UiKit.Rect(_canvas.transform, "Dressing", 0f, 0f, Width, DressingHeight);
            UiKit.StrokedBox(card.Dressing, "Back", _style.CardBackground, Color.white, 4f, 0f, 0f, Width, DressingHeight, 22f);
            card.DressingEdge = card.Dressing.Find("BackEdge").GetComponent<Image>();

            // The section, between the shoulder buttons that change it, and a dot for each.
            DrawShoulder(card.Dressing, "LB", 16f, 14f);
            DrawShoulder(card.Dressing, "RB", Width - 16f - 56f, 14f);
            card.Section = UiKit.TextAt(_style.Bold, card.Dressing, "Section", "", 30f, _style.TextColor, Width / 2f, 44f, Width - 160f, HorizontalAlignmentOptions.Center);
            card.Dots = new Image[SectionNames.Length];
            for (var i = 0; i < card.Dots.Length; i++)
                card.Dots[i] = UiKit.Ellipse(card.Dressing, "Dot", Color.white, Width / 2f - 46f + i * 20f, 60f, 12f, 12f);

            // The item, between arrows; the arrows nudge when pressed.
            card.Left = UiKit.TextAt(_style.DisplayFont, card.Dressing, "Left", "‹", 44f, _style.TextColor, 26f, 124f, 30f).rectTransform;
            card.Item = UiKit.TextAt(_style.DisplayFont, card.Dressing, "Item", "", 34f, _style.TextColor, Width / 2f, 122f, Width - 110f, HorizontalAlignmentOptions.Center);
            card.Right = UiKit.TextAt(_style.DisplayFont, card.Dressing, "Right", "›", 44f, _style.TextColor, Width - 26f, 124f, 30f, HorizontalAlignmentOptions.Right).rectTransform;
            card.Count = UiKit.TextAt(_style.Font, card.Dressing, "Count", "", 28f, _style.TextColor, Width / 2f, 158f, 200f, HorizontalAlignmentOptions.Center);
            card.Locked = UiKit.TextAt(_style.Bold, card.Dressing, "Locked", Tr.Get("Locked: earn it first"), 28f, new Color(0.85f, 0.35f, 0.25f), Width / 2f, 158f, Width - 40f, HorizontalAlignmentOptions.Center);

            DrawBadge(card.Dressing, PromptControl.East, Width - 150f, 168f, 36f);
            UiKit.TextAt(_style.Font, card.Dressing, "Done", Tr.Get("Done"), 28f, _style.TextColor, Width - 106f, 196f, 100f);

            card.Prompt.gameObject.SetActive(false);
            card.Dressing.gameObject.SetActive(false);
            return card;
        }

        // A round Xbox face-button badge, as in the prompts.
        private void DrawBadge(RectTransform parent, PromptControl control, float x, float y, float size)
        {
            UiKit.Box(_style, parent, "Badge", _style.BadgeColor(control, true), x, y, size, size, circle: true);
            UiKit.TextAt(_style.DisplayFont, parent, "Glyph", HudStyle.Glyph(control, true), size * 0.58f,
                Color.white, x + size / 2f, y + size * 0.7f, size, HorizontalAlignmentOptions.Center);
        }

        // A bumper badge: a pill, in the triggers' colour, as the dashboard's tab hints.
        private void DrawShoulder(RectTransform parent, string label, float x, float y)
        {
            UiKit.Box(_style, parent, label, _style.BadgeColor(PromptControl.Triggers, true), x, y, 56f, 36f);
            UiKit.TextAt(_style.DisplayFont, parent, label + "Glyph", label, 20f, Color.white, x + 28f, y + 25f, 56f, HorizontalAlignmentOptions.Center);
        }

        private void HandleStepped(Card card, int direction)
        {
            if (direction == 0)
                card.SectionPop = 0f;
            else
            {
                card.ItemPop = 0f;
                card.Nudge = direction;
                card.Wardrobe?.TryOn();
            }
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            var dt = Time.unscaledDeltaTime;
            _placed.Clear();
            _lefts.Clear();
            _widths.Clear();
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
                    Animate(card, dt);
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

            // The ladders stand in one row, so their cards share one: spread so none overlap.
            if (_placed.Count < 2)
                return;
            var canvasWidth = ((RectTransform)_canvas.transform).sizeDelta.x;
            HudRow.Spread(_lefts, _widths, CardGap, Margin, canvasWidth - Margin);
            for (var i = 0; i < _placed.Count; i++)
                _placed[i].anchoredPosition = new Vector2(_lefts[i], _placed[i].anchoredPosition.y);
        }

        private Color PlayerColour(int slot) => _coop != null && slot >= 0 ? _coop.GetPlayerColor(slot) : Color.white;

        private void Fill(Card card, TailorSession session)
        {
            var section = (int)session.Module;
            var name = session.Module == WardrobeModule.Skin
                ? Tr.Format("Tone {n}", ("n", session.ItemIndex + 1))
                : Tr.Get(session.Item.Name);
            var text = $"{section}|{name}|{session.ItemIndex}|{session.ItemLocked}";
            if (text == card.Text)
                return;
            card.Text = text;
            card.Section.text = Tr.Get(SectionNames[section]);
            var dim = new Color(_style.TextColor.r, _style.TextColor.g, _style.TextColor.b, 0.25f);
            for (var i = 0; i < card.Dots.Length; i++)
                card.Dots[i].color = i == section ? PlayerColour(card.Slot) : dim;
            card.Item.text = name;
            card.Item.alpha = session.ItemLocked ? _style.UnavailableOpacity : 1f;
            card.Count.text = $"{session.ItemIndex + 1} / {session.ItemCount}";
            card.Count.gameObject.SetActive(!session.ItemLocked);
            card.Locked.gameObject.SetActive(session.ItemLocked);
        }

        // Quick pops so every press shows: the item swells and settles, its arrow kicks out, a new
        // section swells. Short (0.16 s) so they never hold the next press back.
        private static void Animate(Card card, float dt)
        {
            card.ItemPop = Mathf.Min(1f, card.ItemPop + dt / PopTime);
            card.SectionPop = Mathf.Min(1f, card.SectionPop + dt / PopTime);
            card.Item.rectTransform.localScale = Vector3.one * (1f + 0.18f * Bump(card.ItemPop));
            card.Section.rectTransform.localScale = Vector3.one * (1f + 0.18f * Bump(card.SectionPop));
            card.Left.localScale = Vector3.one * (card.Nudge < 0 ? 1f + 0.35f * Bump(card.ItemPop) : 1f);
            card.Right.localScale = Vector3.one * (card.Nudge > 0 ? 1f + 0.35f * Bump(card.ItemPop) : 1f);
        }

        // 0 → 1 → 0 over the pop, fast in, eased out.
        private static float Bump(float t) => t >= 1f ? 0f : Mathf.Sin(Mathf.PI * Mathf.Sqrt(t));

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
            _placed.Add(card);
            _lefts.Add(x);
            _widths.Add(width);
        }
    }
}
