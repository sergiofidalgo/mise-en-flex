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
        // Dressing cards dock in a row from the top-right corner (the user's note, 2026-11-09), so a
        // chef across the room is still easy to follow: four use about 85% of the screen's width.
        private const float Width = 400f;
        private const float PortraitHeight = 290f;
        private const float RowsHeight = 214f;
        private const float DressingHeight = PortraitHeight + RowsHeight;
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
            public Camera Portrait;        // a little camera in front of the chef, showing what they try on
            public float Height = 1.45f;   // its framing now, eased towards the section's
            public float Distance = 1.4f;
        }

        // How the portrait frames each section, from the chef's feet: the head for hats, faces and
        // accessories, the upper body for skin, the whole chef for outfits.
        private static readonly (float Height, float Distance)[] Framing =
        {
            (1.5f, 1.45f), (1.38f, 1.05f), (1.15f, 1.8f), (1.38f, 1.05f), (0.95f, 2.7f),
        };

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

        /// <summary>
        /// For a still picture outside Play mode (TailorCapture): shows dressing cards for the given
        /// chefs as play would, docked, the chefs dressed in what they're browsing, and the portrait
        /// cameras framed. The canvas draws through <paramref name="main"/>, so the picture includes
        /// it. Returns the portrait cameras, to render before the main camera.
        /// </summary>
        public IReadOnlyList<Camera> PreviewDressing(
            IReadOnlyList<(ChefWardrobe Wardrobe, Transform Chef, TailorSession Session, int Slot)> dressers, Camera main)
        {
            if (_canvas == null)
                Awake();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = main;
            _canvas.planeDistance = 1f;
            var cards = new List<Card>(_cards.Values);
            var portraits = new List<Camera>();
            for (var i = 0; i < dressers.Count && i < cards.Count; i++)
            {
                var (wardrobe, chef, session, slot) = dressers[i];
                var card = cards[i];
                card.Slot = slot;
                card.DressingEdge.color = PlayerColour(slot);
                card.Wardrobe = wardrobe;
                wardrobe.Show(session.Preview);
                Fill(card, session);
                Frame(card, chef, (int)session.Module, 10f); // a long step: settled on the framing
                Dock(card.Dressing, i);
                card.Dressing.gameObject.SetActive(true);
                portraits.Add(card.Portrait);
            }
            Canvas.ForceUpdateCanvases();
            return portraits;
        }

        private void OnDestroy()
        {
            foreach (var card in _cards.Values)
                if (card.Portrait != null && card.Portrait.targetTexture != null)
                    card.Portrait.targetTexture.Release();
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

            // The portrait: what a little camera in front of the chef sees, so what's being tried on
            // shows even when the chef is across the room.
            var texture = new RenderTexture(512, 360, 24) { name = "TailorPortrait", antiAliasing = 4 };
            var picture = UiKit.Rect(card.Dressing, "Portrait", 14f, 14f, Width - 28f, PortraitHeight - 14f).gameObject.AddComponent<RawImage>();
            picture.texture = texture;
            picture.raycastTarget = false;
            card.Portrait = new GameObject("TailorPortraitCamera").AddComponent<Camera>();
            card.Portrait.transform.SetParent(transform, false);
            card.Portrait.targetTexture = texture;
            card.Portrait.fieldOfView = 32f;
            card.Portrait.nearClipPlane = 0.1f;
            card.Portrait.farClipPlane = 30f;
            card.Portrait.clearFlags = CameraClearFlags.SolidColor;
            card.Portrait.backgroundColor = _style.CardBackground;
            card.Portrait.enabled = false;

            var rows = UiKit.Rect(card.Dressing, "Rows", 0f, PortraitHeight, Width, RowsHeight);

            // The section, between the shoulder buttons that change it, and a dot for each.
            DrawShoulder(rows, "LB", 16f, 14f);
            DrawShoulder(rows, "RB", Width - 16f - 56f, 14f);
            card.Section = UiKit.TextAt(_style.Bold, rows, "Section", "", 30f, _style.TextColor, Width / 2f, 44f, Width - 160f, HorizontalAlignmentOptions.Center);
            card.Dots = new Image[SectionNames.Length];
            for (var i = 0; i < card.Dots.Length; i++)
                card.Dots[i] = UiKit.Ellipse(rows, "Dot", Color.white, Width / 2f - 46f + i * 20f, 60f, 12f, 12f);

            // The item, between arrows; the arrows nudge when pressed.
            card.Left = UiKit.TextAt(_style.DisplayFont, rows, "Left", "‹", 44f, _style.TextColor, 26f, 124f, 30f).rectTransform;
            card.Item = UiKit.TextAt(_style.DisplayFont, rows, "Item", "", 34f, _style.TextColor, Width / 2f, 122f, Width - 110f, HorizontalAlignmentOptions.Center);
            // Long names ("Feather headdress") shrink to fit rather than being cut, never below the
            // HUD's 28 px minimum.
            card.Item.enableAutoSizing = true;
            card.Item.fontSizeMin = 28f;
            card.Item.fontSizeMax = 34f;
            card.Right = UiKit.TextAt(_style.DisplayFont, rows, "Right", "›", 44f, _style.TextColor, Width - 26f, 124f, 30f, HorizontalAlignmentOptions.Right).rectTransform;
            card.Count = UiKit.TextAt(_style.Font, rows, "Count", "", 28f, _style.TextColor, Width / 2f, 158f, 200f, HorizontalAlignmentOptions.Center);
            card.Locked = UiKit.TextAt(_style.Bold, rows, "Locked", Tr.Get("Locked: earn it first"), 28f, new Color(0.85f, 0.35f, 0.25f), Width / 2f, 158f, Width - 40f, HorizontalAlignmentOptions.Center);

            DrawBadge(rows, PromptControl.East, Width - 150f, 168f, 36f);
            UiKit.TextAt(_style.Font, rows, "Done", Tr.Get("Done"), 28f, _style.TextColor, Width - 106f, 196f, 100f);

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
            var docked = 0;
            foreach (var pair in Docking())
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
                    Frame(card, dresser.transform, (int)session.Module, dt);
                    Dock(card.Dressing, docked++);
                    card.Dressing.gameObject.SetActive(true);
                    card.Prompt.gameObject.SetActive(false);
                    continue;
                }

                card.Portrait.enabled = false;
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

            // The prompts at the ladders' feet share one row: spread so none overlap.
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

        // The cards in docking order: dressers first by player slot (Player 1 nearest the corner),
        // then the rest, whose prompts follow their chefs.
        private IEnumerable<KeyValuePair<TailorPlinth, Card>> Docking()
        {
            var list = new List<KeyValuePair<TailorPlinth, Card>>(_cards);
            list.Sort((a, b) => Key(a.Key).CompareTo(Key(b.Key)));
            return list;

            static int Key(TailorPlinth plinth) => plinth.Dresser != null ? plinth.DresserSlot : 100;
        }

        // The n-th dressing card from the top-right corner, leftwards, whole on screen.
        private void Dock(RectTransform card, int index)
        {
            var canvasWidth = ((RectTransform)_canvas.transform).sizeDelta.x;
            if (canvasWidth < 1f)
                canvasWidth = 1920f; // the scaler hasn't sized the canvas yet (a still outside Play mode)
            var x = Mathf.Max(Margin, canvasWidth - Margin - Width - index * (Width + CardGap));
            card.anchoredPosition = new Vector2(x, -Margin);
        }

        // Points the portrait camera at the chef from in front, easing to the section's framing.
        private static void Frame(Card card, Transform chef, int section, float dt)
        {
            var (height, distance) = Framing[Mathf.Clamp(section, 0, Framing.Length - 1)];
            var ease = 1f - Mathf.Exp(-10f * dt);
            card.Height = Mathf.Lerp(card.Height, height, ease);
            card.Distance = Mathf.Lerp(card.Distance, distance, ease);
            var front = Vector3.ProjectOnPlane(chef.forward, Vector3.up).normalized;
            var target = chef.position + Vector3.up * card.Height;
            card.Portrait.transform.SetPositionAndRotation(target + front * card.Distance + Vector3.up * 0.12f,
                Quaternion.LookRotation(target - (target + front * card.Distance + Vector3.up * 0.12f), Vector3.up));
            card.Portrait.enabled = true;
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
            _placed.Add(card);
            _lefts.Add(x);
            _widths.Add(width);
        }
    }
}
