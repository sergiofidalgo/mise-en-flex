using System.Collections.Generic;
using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Office;
using Game.Gameplay.Saving;
using Game.Gameplay.Stations;
using Game.Presentation.Hud;
using Game.Simulation.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The office's player bar (the user's notes, 2026-11-09 and 2026-11-13): one card per player
    /// who has joined, docked in a row from the top-left corner inside the office camera's reserved
    /// top band, the money card in the other corner (2026-11-14). Every card is as wide as the
    /// dressing card, so each player's place never changes width. After them, one narrow join
    /// card (JoinPill) in the next free seat's colour, instead of a card per open seat. The cards are name
    /// plates (NamePlate): chunky ink, the player's colour along the bottom, the face in a
    /// medallion breaking out over the top edge.
    /// <list type="bullet">
    /// <item>Seats nobody has joined: one see-through pill after the last card, ringed with dashes in
    /// the next seat's colour, "A Join", the A gently breathing.</item>
    /// <item>A joined player: a compact card with a still portrait of their chef's head and their
    /// number, from the moment they appear in the elevator. The portrait is taken when they join, and
    /// again when their look changes or they step off the tailor's ladder; nothing renders between.</item>
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
        private const float NumberSize = 44f;
        private const float NamedSize = 36f;
        // The dressing card.
        // Laid out as the kitchen's shop card (2026-11-11): the same width, the page between LB and
        // RB with its dots on top, the same type sizes, and B Done at the same corner.
        private const float Width = 360f;
        private const float HeaderHeight = 72f;
        private const float PortraitHeight = 248f;  // the picture keeps the portrait's 512×360 shape
        private const float RowsHeight = 140f;
        private const float DressingHeight = HeaderHeight + PortraitHeight + RowsHeight;
        private const float CompactWidth = Width; // every slot as wide as the dressing card
        private const float PromptWidth = 220f;
        private const float PromptHeight = 64f;
        private const float HeadHeight = 2.05f;
        private const float Badge = 40f;
        private const float PopTime = 0.16f;
        private const float ShoulderWidth = 50f;
        // A second still this long after joining, once the chef has settled into their first pose.
        private const float SettleDelay = 0.5f;
        // A still draws only the chef: for that one render their renderers move to this layer, which
        // nothing else uses (unnamed in the Tag Manager), so a closed lift door or a chef standing
        // in front can't hide them. Put back the same frame.
        private const int StillLayer = 31;
        private const float PortraitRadius = 16f;
        private const float StillBackdropTint = 0.22f; // the still's backdrop: the card, a touch of the player's colour

        [SerializeField] private HudStyle _style;
        [SerializeField] private CoopSettings _coop;
        [SerializeField] private TailorPlinth[] _plinths = System.Array.Empty<TailorPlinth>();
        [Tooltip("New players naming themselves: their keyboard hangs under their card (2026-11-13).")]
        [SerializeField] private JoinNaming _naming;

        private readonly List<Slot> _slots = new List<Slot>();
        private readonly Dictionary<TailorPlinth, Prompt> _prompts = new Dictionary<TailorPlinth, Prompt>();
        private readonly List<RectTransform> _placed = new List<RectTransform>();
        private readonly List<float> _lefts = new List<float>();
        private readonly List<float> _widths = new List<float>();
        private Canvas _canvas;
        private JoinPill _joinPill;
        private readonly List<(GameObject Part, int Layer)> _moved = new List<(GameObject, int)>();
        private readonly List<Renderer> _parts = new List<Renderer>();

        // For a still outside Play mode: who stands in for each slot, and their dressing if any.
        private readonly Dictionary<int, (Transform Chef, TailorSession Session)> _staged = new Dictionary<int, (Transform, TailorSession)>();
        private bool _staging;

        // For a still: who's naming, by slot.
        private readonly Dictionary<int, ChefNaming> _stagedNaming = new Dictionary<int, ChefNaming>();

        /// <summary>For a still outside Play mode: shows <paramref name="naming"/> as player <paramref name="slot"/> naming themselves.</summary>
        public void PreviewNaming(int slot, ChefNaming naming)
        {
            if (naming == null)
                _stagedNaming.Remove(slot);
            else
                _stagedNaming[slot] = naming;
            foreach (var each in _slots)
                each.KeyboardDrawn = -1;
        }

        private sealed class Prompt
        {
            public RectTransform Card;
            public Image Edge;
        }

        private sealed class Slot
        {
            public int Index;
            public RectTransform Compact;     // a joined player's card
            public TextMeshProUGUI Tag;       // "P1", small, over the name
            public TextMeshProUGUI Name;      // their name, or "P1" big until they have one
            public RectTransform Keyboard;    // naming as they join, under the card
            public int KeyboardDrawn = -1;    // the naming's version last drawn
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
            public Transform Seen;         // the chef in this slot last frame, to notice a join
            public bool Stale = true;      // the still needs taking again
            public float SettleAt = -1f;   // when the settling still is due, or -1
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
            _joinPill = new JoinPill(_style, _canvas.transform);
            foreach (var plinth in _plinths)
            {
                if (plinth == null)
                    continue;
                _prompts[plinth] = BuildPrompt();
                plinth.Stepped += direction => HandleStepped(plinth, direction);
            }
            ChefProfileFile.LookChanged += HandleLookChanged;
        }

        private void OnDestroy()
        {
            ChefProfileFile.LookChanged -= HandleLookChanged;
            foreach (var slot in _slots)
                if (slot.Portrait != null && slot.Portrait.targetTexture != null)
                    slot.Portrait.targetTexture.Release();
        }

        /// <summary>
        /// For a still outside Play mode (TailorCapture): the bar as play would show it, with
        /// <paramref name="staged"/> standing in for the joined players by slot, each dressing if
        /// they have a session. The canvas draws through <paramref name="main"/>. Portraits are drawn
        /// here, so the cameras returned (any still enabled) are normally none.
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

            // Compact: a name plate, the face in a medallion over its left end, the number beside it.
            var plate = PlateHeight;
            var top = Breakout;
            slot.Compact = UiKit.Rect(_canvas.transform, $"Player{index + 1}", 0f, 0f, CompactWidth, top + plate);
            NamePlate.Draw(_style, slot.Compact, colour, 0f, top, CompactWidth, plate);
            var face = _style.PlateFace;
            var outer = face + 2f * (_style.PlateRing + _style.PlateStroke);
            var centre = new Vector2(14f + outer / 2f, outer / 2f);
            var medallion = NamePlate.Medallion(_style, slot.Compact, colour, centre.x, centre.y, face);
            var still = UiKit.Rect(medallion, "Portrait", 0f, 0f, face, face).gameObject.AddComponent<RawImage>();
            still.texture = texture;
            still.raycastTarget = false;
            still.uvRect = new Rect(0.5f - 0.5f * 360f / 512f, 0f, 360f / 512f, 1f); // the middle square
            var column = centre.x + outer / 2f + 16f;
            // The name big in the player's colour with a small "P1" over it (the user's call,
            // 2026-11-13); until they have a name, "P1" alone, big.
            slot.Tag = UiKit.TextAt(_style.Bold, slot.Compact, "Tag", $"P{index + 1}", 20f, colour, column, top + 25f, 60f);
            slot.Name = UiKit.TextAt(_style.DisplayFont, slot.Compact, "Name", $"P{index + 1}", NumberSize, colour, column, top + plate / 2f + NumberSize * 0.36f, CompactWidth - column - 16f);
            slot.Name.enableAutoSizing = true;
            slot.Name.fontSizeMin = 24f;
            slot.Name.fontSizeMax = NumberSize;
            slot.Keyboard = UiKit.Rect(_canvas.transform, $"Player{index + 1}Keyboard", 0f, 0f, CompactWidth, 10f);
            slot.Keyboard.gameObject.SetActive(false);


            // Dressing: the portrait on top, then the controls.
            slot.Dressing = UiKit.Rect(_canvas.transform, $"Player{index + 1}Dressing", 0f, 0f, Width, DressingHeight);
            NamePlate.Draw(_style, slot.Dressing, colour, 0f, 0f, Width, DressingHeight);
            // The page on top, as the shop card's: LB, its name, RB, a dot per section.
            var head = UiKit.Rect(slot.Dressing, "Head", 0f, 0f, Width, HeaderHeight);
            DrawShoulder(head, "LB", 12f, 12f);
            DrawShoulder(head, "RB", Width - 12f - ShoulderWidth, 12f);
            slot.Section = UiKit.TextAt(_style.Bold, head, "Section", "", 30f, _style.TextColor, Width / 2f, 40f, Width - 2f * (ShoulderWidth + 20f), HorizontalAlignmentOptions.Center);
            slot.Section.enableAutoSizing = true;
            slot.Section.fontSizeMin = 24f;
            slot.Section.fontSizeMax = 30f;
            slot.Dots = new Image[SectionNames.Length];
            for (var i = 0; i < slot.Dots.Length; i++)
                slot.Dots[i] = UiKit.Ellipse(head, "Dot", Color.white, Width / 2f - (slot.Dots.Length - 1) * 10f + i * 20f - 6f, 54f, 12f, 12f);

            // The chef, the width of the card's grid.
            RoundedPicture(slot.Dressing, texture, 16f, HeaderHeight, Width - 32f, PortraitHeight - 16f);

            // The item between arrows, its place in the list (or a note when it's locked), then B Done.
            // Nothing to confirm: whatever open item is shown is worn and saved at once (the
            // user's call, 2026-11-11 playtest), so the card only offers B Done.
            var rows = UiKit.Rect(slot.Dressing, "Rows", 0f, HeaderHeight + PortraitHeight, Width, RowsHeight);
            slot.Left = UiKit.TextAt(_style.DisplayFont, rows, "Left", "‹", 40f, _style.TextColor, 16f, 34f, 26f).rectTransform;
            slot.Item = UiKit.TextAt(_style.DisplayFont, rows, "Item", "", 30f, _style.TextColor, Width / 2f, 32f, Width - 76f, HorizontalAlignmentOptions.Center);
            // Long names ("Feather headdress") shrink to fit rather than being cut, never below 24 px.
            slot.Item.enableAutoSizing = true;
            slot.Item.fontSizeMin = 24f;
            slot.Item.fontSizeMax = 30f;
            slot.Right = UiKit.TextAt(_style.DisplayFont, rows, "Right", "›", 40f, _style.TextColor, Width - 16f, 34f, 26f, HorizontalAlignmentOptions.Right).rectTransform;
            slot.Count = UiKit.TextAt(_style.Bold, rows, "Count", "", 21f, new Color(_style.TextColor.r, _style.TextColor.g, _style.TextColor.b, 0.78f), Width / 2f, 64f, 200f, HorizontalAlignmentOptions.Center);
            slot.Locked = UiKit.TextAt(_style.Bold, rows, "Locked", Tr.Get("Locked: earn it first"), 22f, new Color(0.95f, 0.30f, 0.26f), Width / 2f, 64f, Width - 24f, HorizontalAlignmentOptions.Center);
            var done = UiKit.TextAt(_style.Font, rows, "Done", Tr.Get("Done"), 24f, _style.TextColor, Width - 18f, RowsHeight - 22f, 110f, HorizontalAlignmentOptions.Right);
            DrawBadge(rows, PromptControl.East, Width - 18f - UiKit.Width(done) - 8f - 34f, RowsHeight - 48f, 34f);
            slot.Dressing.gameObject.SetActive(false);
            return slot;
        }

        // A rendered picture clipped to rounded corners: a rounded mask holding the picture.
        private static RawImage RoundedPicture(RectTransform parent, Texture texture, float x, float y, float width, float height)
        {
            var frame = UiKit.RoundBox(parent, "PortraitFrame", new Color(0f, 0f, 0f, 0.25f), x, y, width, height, PortraitRadius);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var picture = UiKit.Rect(frame.transform, "Portrait", 0f, 0f, width, height).gameObject.AddComponent<RawImage>();
            picture.texture = texture;
            picture.raycastTarget = false;
            return picture;
        }

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
            // The bar: each slot as it stands, in slot order left to right, the row ending at the
            // top-right corner. Every card is as wide, so nobody's card moves when another dresses.
            var x = Margin;
            var nextSeat = -1;
            foreach (var slot in _slots)
            {
                var (chef, session) = Find(slot.Index);
                if (session != null && chef != null)
                {
                    ShowDressing(slot, chef, session, dt);
                    Dock(slot.Dressing, ref x, Width, Breakout);
                }
                else
                {
                    LeaveDressing(slot);
                    if (ShowCompact(slot, chef))
                    {
                        Dock(slot.Compact, ref x, CompactWidth, 0f);
                        ShowName(slot, slot.Compact.anchoredPosition.x);
                    }
                    else
                    {
                        slot.Keyboard.gameObject.SetActive(false);
                        if (nextSeat < 0)
                            nextSeat = slot.Index;
                    }
                }
            }

            // One join card after the joined players, in the colour of the seat it fills next
            // (the user's pick, 2026-11-14), not a card per open seat; gone once every seat is taken.
            if (nextSeat >= 0)
                _joinPill.Show(Mathf.Min(x, CanvasWidth() - Margin - JoinPill.Width), Margin + Breakout + (PlateHeight - JoinPill.Height) / 2f, PlayerColour(nextSeat), !_staging);
            else
                _joinPill.Hide();

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
            // Live while dressing, so every try-on shows, but still only the chef: no tailor in front.
            DrawChefOnly(slot, chef);
        }

        // Stepped off the ladder: back in what's worn, which drops a locked item still being looked at.
        private void LeaveDressing(Slot slot)
        {
            slot.Dressing.gameObject.SetActive(false);
            if (slot.Shown == null)
                return;
            if (slot.Wardrobe != null)
                slot.Wardrobe.Show(ChefProfileFile.Current.Look(slot.Index));
            slot.Shown = null;
            slot.Stale = true; // the dressing card used the camera: take the face again
        }

        // The tailor saved a new look: retake that player's still.
        private void HandleLookChanged(int index)
        {
            if (index >= 0 && index < _slots.Count)
                _slots[index].Stale = true;
        }

        // The joined card, if anyone's in the seat (true); hidden for a seat nobody's in. Its portrait
        // is a still: the camera renders only on the frame a picture is taken, then stays off.
        private bool ShowCompact(Slot slot, Transform chef)
        {
            var joined = chef != null;
            slot.Compact.gameObject.SetActive(joined);
            if (!joined)
            {
                slot.Wardrobe = null;
                slot.Seen = null;
                slot.SettleAt = -1f;
                slot.Portrait.enabled = false;
                return false;
            }
            slot.Wardrobe ??= chef.GetComponentInChildren<ChefWardrobe>(true);
            var now = Time.unscaledTime;
            var take = _staging || slot.Stale;
            if (slot.Seen != chef)
            {
                // Just joined: a still at once, and one more when they've settled into their first pose.
                slot.Seen = chef;
                slot.SettleAt = now + SettleDelay;
                take = true;
            }
            else if (slot.SettleAt >= 0f && now >= slot.SettleAt)
            {
                slot.SettleAt = -1f;
                take = true;
            }
            slot.Portrait.enabled = false;
            if (take)
            {
                TakeStill(slot, chef);
                slot.Stale = false;
            }
            return true;
        }

        // Photographs the chef's face, and only the chef, into the slot's picture, right now.
        private void TakeStill(Slot slot, Transform chef)
        {
            Frame(slot, chef, FaceFraming, 10f); // straight to the face, no easing
            DrawChefOnly(slot, chef);
        }

        // Renders the slot's camera now, with only <paramref name="chef"/> in the picture.
        private void DrawChefOnly(Slot slot, Transform chef)
        {
            var camera = slot.Portrait;
            camera.cullingMask = 1 << StillLayer;
            camera.backgroundColor = Color.Lerp(_style.CardBackground, PlayerColour(slot.Index), StillBackdropTint);
            _moved.Clear();
            chef.GetComponentsInChildren(true, _parts); // no allocation: the dressing card does this every frame
            foreach (var part in _parts)
            {
                _moved.Add((part.gameObject, part.gameObject.layer));
                part.gameObject.layer = StillLayer;
            }
            // Outside Play mode (a still for the art team) shaders may still be compiling on the first draw.
            for (var pass = 0; pass < (_staging ? 2 : 1); pass++)
                Render(camera);
            foreach (var (part, layer) in _moved)
                if (part != null)
                    part.layer = layer;
            _moved.Clear();
            camera.cullingMask = ~0;
        }

        private static void Render(Camera camera)
        {
            var request = new RenderPipeline.StandardRequest { destination = camera.targetTexture };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
                camera.Render();
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

        // The next card in the bar, left to right, whole on screen. The dressing card's top lines
        // up with the plates' (below their medallions' breakout).
        private void Dock(RectTransform card, ref float x, float width, float drop)
        {
            var left = Mathf.Clamp(x, Margin, CanvasWidth() - Margin - width);
            card.anchoredPosition = new Vector2(left, -Margin - drop);
            x += width + CardGap;
        }

        // The name on the card (what's typed so far while naming), and the keyboard under it.
        private void ShowName(Slot slot, float left)
        {
            var naming = _staging
                ? (_stagedNaming.TryGetValue(slot.Index, out var staged) ? staged : null)
                : _naming != null ? _naming.For(slot.Index) : null;
            var name = naming != null ? naming.Picker.Text : ChefProfileFile.Current.Name(slot.Index);
            var named = !string.IsNullOrEmpty(name);
            var top = Breakout;
            slot.Tag.gameObject.SetActive(named);
            slot.Name.text = named ? name : $"P{slot.Index + 1}";
            // Named: the name sits lower, under its tag; unnamed: "P1" centred in the plate.
            // A little smaller under its tag, so the two lines sit clear of each other and the band.
            slot.Name.fontSizeMax = named ? NamedSize : NumberSize;
            var baseline = named ? top + PlateHeight - 16f : top + PlateHeight / 2f + NumberSize * 0.36f;
            slot.Name.rectTransform.anchoredPosition = new Vector2(slot.Name.rectTransform.anchoredPosition.x, -(baseline - NumberSize));

            slot.Keyboard.gameObject.SetActive(naming != null);
            if (naming == null)
            {
                slot.KeyboardDrawn = -1;
                return;
            }
            if (naming.Version != slot.KeyboardDrawn)
            {
                slot.KeyboardDrawn = naming.Version;
                NameKeyboard.Draw(_style, slot.Keyboard, naming, PlayerColour(slot.Index), CompactWidth);
            }
            slot.Keyboard.anchoredPosition = new Vector2(left, -(Margin + top + PlateHeight + CardGap));
        }

        private float PlateHeight => _style.PlateHeight;

        // How far the face medallion rises above its plate's top edge.
        private float Breakout => _style.PlateBreakout;

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
