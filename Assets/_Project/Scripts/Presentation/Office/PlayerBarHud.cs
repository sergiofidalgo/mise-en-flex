using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Office;
using Game.Gameplay.Saving;
using Game.Gameplay.Stations;
using Game.Presentation.Hud;
using Game.Simulation.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The office's player bar (the user's notes, 2026-11-09): one card per player slot, always
    /// there, docked in a row from the top-left corner inside the office camera's reserved top band.
    /// <list type="bullet">
    /// <item>A slot nobody has joined: a translucent card, "A Join", with that slot's number.</item>
    /// <item>A joined player: a compact card with a live portrait of their chef's head (redrawn a few
    /// times a second, to keep four portraits cheap) and their number, from the moment they appear
    /// in the elevator.</item>
    /// <item>A player dressing at the tailor: their card grows into the dressing card: a portrait
    /// framed on what's being changed, the section between LB and RB, the item between arrows with
    /// its place in the list (or a note when it's locked), and B Done. Every press answers at once:
    /// the item pops, its arrow nudges, a new section pops, and the chef bounces as they try
    /// something on. The chef shows what's being browsed, and goes back to what they wear when
    /// they step off.</item>
    /// </list>
    /// At the foot of each ladder, "A Climb up" sits over the chef standing there (spread apart if
    /// several). Everything stays whole on screen and only ever names Xbox controls.
    /// </summary>
    public sealed class PlayerBarHud : MonoBehaviour
    {
        private const float Margin = 24f;
        private const float CardGap = 12f;
        // The compact card: small enough that the bar sits in the office camera's reserved top
        // band (15% of the screen), so chefs never walk under it.
        private const float CompactWidth = 210f;
        private const float CompactHeight = 108f;
        private const float EmptyOpacity = 0.4f;
        // The dressing card.
        private const float Width = 340f;
        private const float PortraitHeight = 230f;
        private const float RowsHeight = 196f;
        private const float DressingHeight = PortraitHeight + RowsHeight;
        private const float PromptWidth = 220f;
        private const float PromptHeight = 64f;
        private const float HeadHeight = 2.05f;
        private const float Badge = 40f;
        private const float PopTime = 0.16f;
        private const float ShoulderWidth = 50f;
        // Compact portraits redraw this many times a second; a dressing one every frame.
        private const float CompactRefresh = 6f;

        [SerializeField] private HudStyle _style;
        [SerializeField] private CoopSettings _coop;
        [SerializeField] private TailorPlinth[] _plinths = System.Array.Empty<TailorPlinth>();

        private readonly List<Slot> _slots = new List<Slot>();
        private readonly Dictionary<TailorPlinth, Prompt> _prompts = new Dictionary<TailorPlinth, Prompt>();
        private readonly List<RectTransform> _placed = new List<RectTransform>();
        private readonly List<float> _lefts = new List<float>();
        private readonly List<float> _widths = new List<float>();
        private Canvas _canvas;

        // For a still outside Play mode: who stands in for each slot, and their dressing if any.
        private readonly Dictionary<int, (Transform Chef, TailorSession Session)> _staged = new Dictionary<int, (Transform, TailorSession)>();
        private bool _staging;

        private sealed class Prompt
        {
            public RectTransform Card;
            public Image Edge;
        }

        private sealed class Slot
        {
            public int Index;
            public RectTransform Compact;
            public CanvasGroup CompactGroup;
            public RawImage CompactPortrait;
            public GameObject JoinHint;
            public RectTransform Dressing;
            public TextMeshProUGUI Section;
            public Image[] Dots;
            public TextMeshProUGUI Item;
            public RectTransform Left;
            public RectTransform Right;
            public TextMeshProUGUI Count;
            public TextMeshProUGUI Locked;
            public Camera Portrait;        // a little camera in front of the chef, for both cards
            public float Height = 1.38f;   // its framing now, eased towards the wanted one
            public float Distance = 1.05f;
            public float NextRefresh;
            public ChefWardrobe Wardrobe;
            public ChefLook Shown;         // what the chef shows while dressing
            public string Text;
            public float ItemPop = 1f;     // 0 just changed, 1 settled
            public float SectionPop = 1f;
            public int Nudge;              // the arrow last pressed: -1, 0 or 1
        }

        // How the portrait frames each section, from the chef's feet: the head for hats, faces and
        // accessories, the upper body for skin, the whole chef for outfits. Compact cards use the face's.
        private static readonly (float Height, float Distance)[] Framing =
        {
            (1.5f, 1.45f), (1.38f, 1.05f), (1.15f, 1.8f), (1.38f, 1.05f), (0.95f, 2.7f),
        };
        private const int FaceFraming = 1;

        private static readonly string[] SectionNames = { "Hat", "Face", "Skin", "Accessory", "Outfit" };

        private void Awake()
        {
            if (_canvas != null)
                return;
            var canvasObject = new GameObject("PlayerBar", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 20;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var slots = _coop != null ? Mathf.Max(1, _coop.MaxPlayers) : 4;
            for (var i = 0; i < slots; i++)
                _slots.Add(BuildSlot(i));
            foreach (var plinth in _plinths)
            {
                if (plinth == null)
                    continue;
                _prompts[plinth] = BuildPrompt();
                plinth.Stepped += direction => HandleStepped(plinth, direction);
            }
        }

        private void OnDestroy()
        {
            foreach (var slot in _slots)
                if (slot.Portrait != null && slot.Portrait.targetTexture != null)
                    slot.Portrait.targetTexture.Release();
        }

        /// <summary>
        /// For a still outside Play mode (TailorCapture): the bar as play would show it, with
        /// <paramref name="staged"/> standing in for the joined players by slot, each dressing if
        /// they have a session. The canvas draws through <paramref name="main"/>. Returns the
        /// portrait cameras to render before the main camera.
        /// </summary>
        public IReadOnlyList<Camera> PreviewBar(IReadOnlyDictionary<int, (Transform Chef, TailorSession Session)> staged, Camera main)
        {
            Awake();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = main;
            _canvas.planeDistance = 1f;
            _staged.Clear();
            foreach (var pair in staged)
                _staged[pair.Key] = pair.Value;
            _staging = true;
            Layout(main, 10f); // a long step: everything settled
            Canvas.ForceUpdateCanvases();
            var portraits = new List<Camera>();
            foreach (var slot in _slots)
                if (slot.Portrait.enabled)
                    portraits.Add(slot.Portrait);
            return portraits;
        }

        private void LateUpdate()
        {
            if (!_staging)
                Layout(Camera.main, Time.unscaledDeltaTime);
        }

        // ---- Building ----------------------------------------------------------------------------

        private Prompt BuildPrompt()
        {
            var prompt = new Prompt { Card = UiKit.Rect(_canvas.transform, "ClimbPrompt", 0f, 0f, PromptWidth, PromptHeight) };
            UiKit.StrokedBox(prompt.Card, "Back", _style.CardBackground, Color.white, 4f, 0f, 0f, PromptWidth, PromptHeight, 18f);
            prompt.Edge = prompt.Card.Find("BackEdge").GetComponent<Image>();
            DrawBadge(prompt.Card, PromptControl.South, 16f, 12f, Badge);
            UiKit.TextAt(_style.Bold, prompt.Card, "Label", Tr.Get("Climb up"), 28f, _style.TextColor, 68f, 43f, PromptWidth - 82f);
            prompt.Card.gameObject.SetActive(false);
            return prompt;
        }

        private Slot BuildSlot(int index)
        {
            var colour = PlayerColour(index);
            var slot = new Slot { Index = index };

            // The portrait camera and its picture, shared by the compact and the dressing card.
            var texture = new RenderTexture(512, 360, 24) { name = $"PlayerPortrait{index + 1}", antiAliasing = 4 };
            slot.Portrait = new GameObject($"PlayerPortraitCamera{index + 1}").AddComponent<Camera>();
            slot.Portrait.transform.SetParent(transform, false);
            slot.Portrait.targetTexture = texture;
            slot.Portrait.fieldOfView = 32f;
            slot.Portrait.nearClipPlane = 0.1f;
            slot.Portrait.farClipPlane = 30f;
            slot.Portrait.clearFlags = CameraClearFlags.SolidColor;
            slot.Portrait.backgroundColor = _style.CardBackground;
            slot.Portrait.enabled = false;

            // Compact: the head in a square on the left, the player's number, and "A Join" while empty.
            var square = CompactHeight - 24f;
            slot.Compact = UiKit.Rect(_canvas.transform, $"Player{index + 1}", 0f, 0f, CompactWidth, CompactHeight);
            slot.CompactGroup = slot.Compact.gameObject.AddComponent<CanvasGroup>();
            UiKit.StrokedBox(slot.Compact, "Back", _style.CardBackground, colour, 4f, 0f, 0f, CompactWidth, CompactHeight, 18f);
            UiKit.RoundBox(slot.Compact, "PortraitBack", new Color(0f, 0f, 0f, 0.25f), 12f, 12f, square, square, 10f);
            slot.CompactPortrait = UiKit.Rect(slot.Compact, "Portrait", 12f, 12f, square, square).gameObject.AddComponent<RawImage>();
            slot.CompactPortrait.texture = texture;
            slot.CompactPortrait.uvRect = new Rect(0.5f - 0.5f * 360f / 512f, 0f, 360f / 512f, 1f); // the middle square
            slot.CompactPortrait.raycastTarget = false;
            UiKit.TextAt(_style.DisplayFont, slot.Compact, "Number", $"P{index + 1}", 34f, colour, CompactHeight + 4f, 46f, 80f);
            var hint = UiKit.Rect(slot.Compact, "JoinHint", 0f, 0f, CompactWidth, CompactHeight);
            DrawBadge(hint, PromptControl.South, CompactHeight + 4f, 58f, 34f);
            UiKit.TextAt(_style.Bold, hint, "Join", Tr.Get("Join"), 28f, _style.TextColor, CompactHeight + 46f, 86f, 60f);
            slot.JoinHint = hint.gameObject;

            // Dressing: the portrait on top, then the controls.
            slot.Dressing = UiKit.Rect(_canvas.transform, $"Player{index + 1}Dressing", 0f, 0f, Width, DressingHeight);
            UiKit.StrokedBox(slot.Dressing, "Back", _style.CardBackground, colour, 4f, 0f, 0f, Width, DressingHeight, 22f);
            var picture = UiKit.Rect(slot.Dressing, "Portrait", 14f, 14f, Width - 28f, PortraitHeight - 14f).gameObject.AddComponent<RawImage>();
            picture.texture = texture;
            picture.raycastTarget = false;
            var rows = UiKit.Rect(slot.Dressing, "Rows", 0f, PortraitHeight, Width, RowsHeight);
            DrawShoulder(rows, "LB", 12f, 12f);
            DrawShoulder(rows, "RB", Width - 12f - ShoulderWidth, 12f);
            slot.Section = UiKit.TextAt(_style.Bold, rows, "Section", "", 30f, _style.TextColor, Width / 2f, 40f, Width - 2f * (ShoulderWidth + 20f), HorizontalAlignmentOptions.Center);
            slot.Section.enableAutoSizing = true;
            slot.Section.fontSizeMin = 28f;
            slot.Section.fontSizeMax = 30f;
            slot.Dots = new Image[SectionNames.Length];
            for (var i = 0; i < slot.Dots.Length; i++)
                slot.Dots[i] = UiKit.Ellipse(rows, "Dot", Color.white, Width / 2f - 46f + i * 20f, 54f, 12f, 12f);
            slot.Left = UiKit.TextAt(_style.DisplayFont, rows, "Left", "‹", 44f, _style.TextColor, 14f, 112f, 26f).rectTransform;
            slot.Item = UiKit.TextAt(_style.DisplayFont, rows, "Item", "", 34f, _style.TextColor, Width / 2f, 110f, Width - 70f, HorizontalAlignmentOptions.Center);
            // Long names ("Feather headdress") shrink to fit rather than being cut, never below 28 px.
            slot.Item.enableAutoSizing = true;
            slot.Item.fontSizeMin = 28f;
            slot.Item.fontSizeMax = 34f;
            slot.Right = UiKit.TextAt(_style.DisplayFont, rows, "Right", "›", 44f, _style.TextColor, Width - 14f, 112f, 26f, HorizontalAlignmentOptions.Right).rectTransform;
            slot.Count = UiKit.TextAt(_style.Font, rows, "Count", "", 28f, _style.TextColor, Width / 2f, 144f, 200f, HorizontalAlignmentOptions.Center);
            slot.Locked = UiKit.TextAt(_style.Bold, rows, "Locked", Tr.Get("Locked: earn it first"), 28f, new Color(0.85f, 0.35f, 0.25f), Width / 2f, 144f, Width - 24f, HorizontalAlignmentOptions.Center);
            DrawBadge(rows, PromptControl.East, Width - 128f, 152f, 34f);
            UiKit.TextAt(_style.Font, rows, "Done", Tr.Get("Done"), 28f, _style.TextColor, Width - 86f, 179f, 80f);
            slot.Dressing.gameObject.SetActive(false);
            return slot;
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
            UiKit.Box(_style, parent, label, _style.BadgeColor(PromptControl.Triggers, true), x, y, ShoulderWidth, 34f);
            UiKit.TextAt(_style.DisplayFont, parent, label + "Glyph", label, 20f, Color.white, x + ShoulderWidth / 2f, y + 24f, ShoulderWidth, HorizontalAlignmentOptions.Center);
        }

        // ---- Each frame ---------------------------------------------------------------------------

        private void HandleStepped(TailorPlinth plinth, int direction)
        {
            var index = plinth.DresserSlot;
            if (index < 0 || index >= _slots.Count)
                return;
            var slot = _slots[index];
            if (direction == 0)
                slot.SectionPop = 0f;
            else
            {
                slot.ItemPop = 0f;
                slot.Nudge = direction;
                slot.Wardrobe?.TryOn();
            }
        }

        private void Layout(Camera camera, float dt)
        {
            // The bar: each slot as it stands, laid left to right from the corner in slot order.
            var x = Margin;
            foreach (var slot in _slots)
            {
                var (chef, session) = Find(slot.Index);
                if (session != null && chef != null)
                {
                    ShowDressing(slot, chef, session, dt);
                    Dock(slot.Dressing, ref x, Width);
                }
                else
                {
                    LeaveDressing(slot);
                    ShowCompact(slot, chef, dt);
                    Dock(slot.Compact, ref x, CompactWidth);
                }
            }

            // The climb prompts at the ladders' feet, over the chef standing there.
            _placed.Clear();
            _lefts.Clear();
            _widths.Clear();
            foreach (var pair in _prompts)
            {
                var standing = _staging ? null : pair.Key.Standing;
                var prompt = pair.Value;
                prompt.Card.gameObject.SetActive(standing != null);
                if (standing == null)
                    continue;
                prompt.Edge.color = PlayerColour(standing.playerIndex);
                Place(prompt.Card, camera, standing.transform.position, PromptWidth, PromptHeight);
            }
            // They share one row (the ladders do): spread so none overlap.
            if (_placed.Count < 2)
                return;
            HudRow.Spread(_lefts, _widths, CardGap, Margin, CanvasWidth() - Margin);
            for (var i = 0; i < _placed.Count; i++)
                _placed[i].anchoredPosition = new Vector2(_lefts[i], _placed[i].anchoredPosition.y);
        }

        // Who's in a slot: their chef, and their dressing if they're on a ladder.
        private (Transform Chef, TailorSession Session) Find(int index)
        {
            if (_staging)
                return _staged.TryGetValue(index, out var staged) ? staged : (null, null);
            foreach (var plinth in _plinths)
                if (plinth != null && plinth.Dresser != null && plinth.DresserSlot == index)
                    return (plinth.Dresser.transform, plinth.Session);
            foreach (var player in PlayerInput.all)
                if (player.playerIndex == index)
                    return (player.transform, null);
            return (null, null);
        }

        private void ShowDressing(Slot slot, Transform chef, TailorSession session, float dt)
        {
            slot.Compact.gameObject.SetActive(false);
            slot.Dressing.gameObject.SetActive(true);
            slot.Wardrobe ??= chef.GetComponentInChildren<ChefWardrobe>(true);
            if (slot.Wardrobe != null && (slot.Shown == null || !slot.Shown.SameAs(session.Preview)))
            {
                slot.Shown = session.Preview.Copy();
                slot.Wardrobe.Show(slot.Shown);
            }
            Fill(slot, session);
            Animate(slot, dt);
            Frame(slot, chef, (int)session.Module, dt);
            slot.Portrait.enabled = true;
        }

        // Stepped off the ladder: back in what's worn, which drops a locked item still being looked at.
        private void LeaveDressing(Slot slot)
        {
            slot.Dressing.gameObject.SetActive(false);
            if (slot.Shown != null && slot.Wardrobe != null)
                slot.Wardrobe.Show(ChefProfileFile.Current.Look(slot.Index));
            slot.Shown = null;
        }

        private void ShowCompact(Slot slot, Transform chef, float dt)
        {
            slot.Compact.gameObject.SetActive(true);
            var joined = chef != null;
            slot.CompactGroup.alpha = joined ? 1f : EmptyOpacity; // the user's call: empty slots translucent
            slot.JoinHint.SetActive(!joined);
            slot.CompactPortrait.enabled = joined;
            if (!joined)
            {
                slot.Wardrobe = null;
                slot.Portrait.enabled = false;
                return;
            }
            slot.Wardrobe ??= chef.GetComponentInChildren<ChefWardrobe>(true);
            Frame(slot, chef, FaceFraming, dt);
            // Redrawn a few times a second: four live heads at full rate would cost too much.
            var now = Time.unscaledTime;
            var redraw = _staging || now >= slot.NextRefresh;
            if (redraw)
                slot.NextRefresh = now + 1f / CompactRefresh;
            slot.Portrait.enabled = redraw;
        }

        private void Fill(Slot slot, TailorSession session)
        {
            var section = (int)session.Module;
            var name = session.Module == WardrobeModule.Skin
                ? Tr.Format("Tone {n}", ("n", session.ItemIndex + 1))
                : Tr.Get(session.Item.Name);
            var text = $"{section}|{name}|{session.ItemIndex}|{session.ItemLocked}";
            if (text == slot.Text)
                return;
            slot.Text = text;
            slot.Section.text = Tr.Get(SectionNames[section]);
            var dim = new Color(_style.TextColor.r, _style.TextColor.g, _style.TextColor.b, 0.25f);
            for (var i = 0; i < slot.Dots.Length; i++)
                slot.Dots[i].color = i == section ? PlayerColour(slot.Index) : dim;
            slot.Item.text = name;
            slot.Item.alpha = session.ItemLocked ? _style.UnavailableOpacity : 1f;
            slot.Count.text = $"{session.ItemIndex + 1} / {session.ItemCount}";
            slot.Count.gameObject.SetActive(!session.ItemLocked);
            slot.Locked.gameObject.SetActive(session.ItemLocked);
        }

        // Quick pops so every press shows: the item swells and settles, its arrow kicks out, a new
        // section swells. Short (0.16 s) so they never hold the next press back.
        private static void Animate(Slot slot, float dt)
        {
            slot.ItemPop = Mathf.Min(1f, slot.ItemPop + dt / PopTime);
            slot.SectionPop = Mathf.Min(1f, slot.SectionPop + dt / PopTime);
            slot.Item.rectTransform.localScale = Vector3.one * (1f + 0.18f * Bump(slot.ItemPop));
            slot.Section.rectTransform.localScale = Vector3.one * (1f + 0.18f * Bump(slot.SectionPop));
            slot.Left.localScale = Vector3.one * (slot.Nudge < 0 ? 1f + 0.35f * Bump(slot.ItemPop) : 1f);
            slot.Right.localScale = Vector3.one * (slot.Nudge > 0 ? 1f + 0.35f * Bump(slot.ItemPop) : 1f);
        }

        // 0 → 1 → 0 over the pop, fast in, eased out.
        private static float Bump(float t) => t >= 1f ? 0f : Mathf.Sin(Mathf.PI * Mathf.Sqrt(t));

        // Points the portrait camera at the chef from in front, easing to a framing.
        private static void Frame(Slot slot, Transform chef, int framing, float dt)
        {
            var (height, distance) = Framing[Mathf.Clamp(framing, 0, Framing.Length - 1)];
            var ease = 1f - Mathf.Exp(-10f * dt);
            slot.Height = Mathf.Lerp(slot.Height, height, ease);
            slot.Distance = Mathf.Lerp(slot.Distance, distance, ease);
            var front = Vector3.ProjectOnPlane(chef.forward, Vector3.up).normalized;
            var target = chef.position + Vector3.up * slot.Height;
            var from = target + front * slot.Distance + Vector3.up * 0.12f;
            slot.Portrait.transform.SetPositionAndRotation(from, Quaternion.LookRotation(target - from, Vector3.up));
        }

        // The next card in the bar, top left onwards, whole on screen.
        private void Dock(RectTransform card, ref float x, float width)
        {
            var left = Mathf.Min(x, CanvasWidth() - Margin - width);
            card.anchoredPosition = new Vector2(left, -Margin);
            x += width + CardGap;
        }

        private float CanvasWidth()
        {
            var width = ((RectTransform)_canvas.transform).sizeDelta.x;
            return width < 1f ? 1920f : width; // the scaler hasn't sized it yet (a still outside Play mode)
        }

        private Color PlayerColour(int slot) => _coop != null && slot >= 0 ? _coop.GetPlayerColor(slot) : Color.white;

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
