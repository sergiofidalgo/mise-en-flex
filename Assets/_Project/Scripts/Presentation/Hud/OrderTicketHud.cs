using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Customers;
using Game.Simulation.Service;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// Each guest's own speech bubble over their head (the 2026-11-11 playtest round, replacing the
    /// dark order tickets and their patience bars): the dish's picture and its doneness word, in
    /// the doneness's colour so it matches the food tags, plus the side's picture. Patience shows
    /// as the bubble itself: it warms towards the patience colour as they lose it and wobbles when
    /// it's nearly gone. Guests queueing outside get a "…" bubble that does the same. Bubbles at a
    /// busy table step up over each other rather than overlap, and always stay on screen.
    ///
    /// A delivery order has the same bubble with the app's badge in front (decision 2026-11-12):
    /// first over its place on the delivery counter, the ring round the badge running down until the
    /// courier pulls up, then over the courier's head as they walk in and wait, warming and
    /// wobbling as their patience goes. Nobody's there to carry an edge marker for it, so off screen
    /// the bubble itself waits at the screen's edge.
    /// </summary>
    public sealed class OrderTicketHud : MonoBehaviour
    {
        private const float Height = 54f, Pad = 12f, Icon = 46f, Gap = 6f, Stroke = 3f, Tail = 14f, Radius = 18f, TextSize = 24f;
        private const string Waiting = "…";

        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [SerializeField] private CustomerDirector _director;
        [SerializeField] private ItemCatalog _catalog;
        [Tooltip("Pictures of the dishes; without one, the bubble names the dish.")]
        [SerializeField] private IngredientIcons _icons;

        [Tooltip("Metres above a customer's feet where their bubble's tail points (just over their head).")]
        [SerializeField, Min(0f)] private float _height = 1.85f;
        [Tooltip("Canvas pixels kept between two bubbles that would overlap.")]
        [SerializeField, Min(0f)] private float _apart = 6f;
        [Tooltip("Patience (0..1) below which the bubble starts warming towards the patience colour.")]
        [SerializeField, Range(0f, 1f)] private float _tintFrom = 0.6f;
        [Tooltip("How far it warms at no patience left (0 none, 1 the full patience colour).")]
        [SerializeField, Range(0f, 1f)] private float _tintStrength = 0.6f;
        [Tooltip("Patience below which the bubble wobbles, more as it runs out.")]
        [SerializeField, Range(0f, 1f)] private float _wobbleFrom = 0.35f;
        [Tooltip("Degrees it rocks at no patience left, and how many times a second.")]
        [SerializeField, Min(0f)] private float _wobbleAngle = 9f;
        [SerializeField, Min(0f)] private float _wobbleRate = 3.5f;
        [SerializeField] private Color _paper = new Color(0.99f, 0.96f, 0.89f);
        [SerializeField] private Color _ink = new Color(0.227f, 0.141f, 0.078f);

        [Header("Delivery orders (decision 2026-11-12)")]
        [SerializeField] private Game.Gameplay.KitchenCompositionRoot _root;
        [SerializeField] private CourierDirector _couriers;
        [Tooltip("Metres above a delivery counter's place where its order's bubble points.")]
        [SerializeField, Min(0f)] private float _overCounter = 0.55f;
        [Tooltip("The delivery app's colour: the couriers' lime, which is no player's.")]
        [SerializeField] private Color _app = new Color(0.55f, 0.86f, 0.22f);
        [SerializeField] private Color _appMark = Color.white;
        [Tooltip("Real seconds a new delivery bubble takes to pop up, and how far past its size it swells on the way.")]
        [SerializeField, Min(0.01f)] private float _popSeconds = 0.35f;
        [SerializeField, Min(0f)] private float _popOvershoot = 0.25f;

        private const float Badge = 40f;

        private readonly Dictionary<object, Bubble> _bubbles = new Dictionary<object, Bubble>();
        private readonly List<object> _stale = new List<object>();
        private readonly List<Bubble> _shown = new List<Bubble>();
        private readonly List<Rect> _placed = new List<Rect>();

        /// <summary>The bubbles showing now, as canvas rectangles, for tests (no two overlap; all on screen).</summary>
        public IReadOnlyList<Rect> Placed => _placed;

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null || _director == null)
                return;

            _shown.Clear();
            foreach (var pair in _director.Agents)
            {
                var customer = pair.Key;
                var bubble = BubbleFor(customer, customer.Id);
                string content = null;
                if (customer.State == CustomerState.Waiting && customer.Order != null)
                    content = KeyFor(customer.Order);
                else if (customer.State == CustomerState.Queueing)
                    content = Waiting;

                // A guest out of view has their edge marker (CustomerIndicatorHud) instead: a bubble slid
                // in from the edge would sit on it and point at nobody.
                var screen = camera.WorldToScreenPoint(pair.Value.transform.position + Vector3.up * _height);
                var feet = camera.WorldToViewportPoint(pair.Value.transform.position);
                var seen = feet.z > 0f && feet.x >= 0f && feet.x <= 1f && feet.y >= 0f && feet.y <= 1f;
                if (content == null || screen.z <= 0f || !seen)
                {
                    bubble.Target = 0f;
                    continue;
                }
                if (bubble.Content != content)
                    Draw(bubble, customer.Order, content, delivery: false);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out bubble.Anchor);
                bubble.Patience = customer.Patience;
                bubble.Target = 1f;
                _shown.Add(bubble);
            }

            ShowDeliveries(camera);
            Arrange();
            foreach (var bubble in _bubbles.Values)
                Animate(bubble);

            // Customers who have left the scene take their bubble with them; a delivery order's goes
            // with the courier who took it (or gave up).
            _stale.Clear();
            foreach (var key in _bubbles.Keys)
            {
                if (key is Customer customer ? !_director.Agents.ContainsKey(customer) : !(key is DeliveryOrder order && order.HoldsSlot))
                    _stale.Add(key);
            }
            foreach (var key in _stale)
            {
                Destroy(_bubbles[key].Root.gameObject);
                _bubbles.Remove(key);
            }
        }

        // Each open delivery order: over its place on the counter until its courier is on foot, then
        // over the courier.
        private void ShowDeliveries(Camera camera)
        {
            var services = _root != null ? _root.Services : null;
            if (services == null)
                return;
            foreach (var order in services.Service.Deliveries.Orders)
            {
                if (!order.HoldsSlot)
                    continue;
                var bubble = BubbleFor(order, order.Id);
                Vector3 world;
                if (_couriers != null && _couriers.TryGetCourier(order, out var courier))
                {
                    world = courier.transform.position + Vector3.up * _height;
                }
                else
                {
                    var place = PlaceOf(services, order.Slot);
                    if (place == null)
                    {
                        bubble.Target = 0f;
                        continue;
                    }
                    world = place.position + Vector3.up * _overCounter;
                }

                var screen = camera.WorldToScreenPoint(world);
                if (screen.z <= 0f)
                {
                    bubble.Target = 0f;
                    continue;
                }
                var content = "delivery|" + KeyFor(order.Order);
                if (bubble.Content != content)
                {
                    Draw(bubble, order.Order, content, delivery: true);
                    bubble.Born = Time.unscaledTime;
                }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out bubble.Anchor);
                // The ring is the time left to cook; the warming and the wobble are the courier's patience.
                bubble.Patience = order.State == DeliveryState.CourierWaiting ? order.Patience : 1f;
                if (bubble.Ring != null)
                    bubble.Ring.fillAmount = order.CountdownLeft;
                bubble.Target = 1f;
                _shown.Add(bubble);
            }
        }

        private static Transform PlaceOf(Game.Gameplay.KitchenServices services, int slot)
        {
            foreach (var station in services.Stations.All)
            {
                if (station is Game.Gameplay.Stations.DeliveryCounterStation counter && counter.AnchorOfSlot(slot) is Transform place)
                    return place;
            }
            return null;
        }

        // Nearest the camera first (lowest on screen), each bubble at its guest unless that overlaps
        // one already placed: then it steps up above it. Then kept inside the screen.
        private void Arrange()
        {
            _shown.Sort((a, b) => a.Anchor.y != b.Anchor.y ? a.Anchor.y.CompareTo(b.Anchor.y) : a.Anchor.x.CompareTo(b.Anchor.x));
            _placed.Clear();
            var half = _canvas.rect.size / 2f;
            // Room for the corners of a wobbling bubble too.
            var margin = (_style != null ? _style.ScreenMargin : 12f) + _wobbleAngle;
            foreach (var bubble in _shown)
            {
                var size = bubble.Size;
                var rect = new Rect(bubble.Anchor.x - size.x / 2f, bubble.Anchor.y, size.x, size.y);
                // On screen first, whatever the camera does: slide in from any edge.
                rect = Inside(rect, half, margin);
                for (var tries = 0; tries < 8; tries++)
                {
                    var moved = false;
                    foreach (var other in _placed)
                    {
                        if (!Grow(other, _apart).Overlaps(rect))
                            continue;
                        rect.y = other.yMax + _apart;
                        moved = true;
                    }
                    if (!moved)
                        break;
                }
                // Stepping up can reach the top: then it goes beside the others instead.
                if (rect.yMax > half.y - margin)
                    rect = Beside(rect, half, margin);
                _placed.Add(rect);
                bubble.Root.anchoredPosition = new Vector2(rect.center.x, rect.y);
            }
        }

        private static Rect Inside(Rect rect, Vector2 half, float margin)
        {
            rect.x = Mathf.Clamp(rect.x, -half.x + margin, half.x - margin - rect.width);
            rect.y = Mathf.Clamp(rect.y, -half.y + margin, half.y - margin - rect.height);
            return rect;
        }

        // Back down at the top edge, then right along it until it's clear (left if right runs out).
        private Rect Beside(Rect rect, Vector2 half, float margin)
        {
            rect = Inside(rect, half, margin);
            var start = rect.x;
            foreach (var direction in new[] { 1f, -1f })
            {
                rect.x = start;
                for (var tries = 0; tries < 16 && Overlapping(rect); tries++)
                    rect.x += direction * (rect.width / 2f + _apart);
                if (!Overlapping(rect) && rect.xMin >= -half.x + margin && rect.xMax <= half.x - margin)
                    return rect;
            }
            rect.x = start;
            return rect;
        }

        private bool Overlapping(Rect rect)
        {
            foreach (var other in _placed)
                if (Grow(other, _apart).Overlaps(rect))
                    return true;
            return false;
        }

        private static Rect Grow(Rect rect, float by) => new Rect(rect.x - by, rect.y - by, rect.width + 2f * by, rect.height + 2f * by);

        private void Animate(Bubble bubble)
        {
            var fade = _style != null && _style.Fade > 0f ? _style.Fade : 0.15f;
            bubble.Group.alpha = Mathf.MoveTowards(bubble.Group.alpha, bubble.Target, Time.unscaledDeltaTime / fade);
            if (bubble.Group.alpha <= 0f)
                return;

            var patience = Mathf.Clamp01(bubble.Patience);
            // Warming: none above _tintFrom, up to _tintStrength of the patience colour at zero.
            var warm = _tintFrom > 0f ? Mathf.Clamp01((_tintFrom - patience) / _tintFrom) : 0f;
            var colour = _style != null ? _style.PatienceColor(patience) : Color.red;
            var fill = Color.Lerp(_paper, colour, warm * _tintStrength);
            foreach (var image in bubble.Fills)
                image.color = fill;

            // Wobbling: rocks about its tail, harder as patience runs out; each guest out of step.
            var shake = _wobbleFrom > 0f ? Mathf.Clamp01((_wobbleFrom - patience) / _wobbleFrom) : 0f;
            var angle = shake * _wobbleAngle * Mathf.Sin((Time.unscaledTime + bubble.Phase) * _wobbleRate * 2f * Mathf.PI);
            bubble.Body.localRotation = Quaternion.Euler(0f, 0f, angle);

            // A new delivery order pops up from its tablet: out past its size and back, once.
            if (bubble.Born >= 0f)
            {
                var t = Mathf.Clamp01((Time.unscaledTime - bubble.Born) / _popSeconds);
                bubble.Body.localScale = Vector3.one * (t + _popOvershoot * Mathf.Sin(t * Mathf.PI));
                if (t >= 1f)
                    bubble.Born = -1f;
            }
        }

        private Bubble BubbleFor(object owner, int id)
        {
            if (_bubbles.TryGetValue(owner, out var bubble))
                return bubble;
            var root = new GameObject($"Bubble_{id}", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(_canvas, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0f);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            var body = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
            body.SetParent(root, false);
            body.anchorMin = body.anchorMax = new Vector2(0.5f, 0f);
            body.pivot = new Vector2(0.5f, 0f);
            body.anchoredPosition = Vector2.zero;
            bubble = new Bubble { Root = root, Body = body, Group = group, Phase = (id % 7) * 0.13f };
            _bubbles[owner] = bubble;
            return bubble;
        }

        // The bubble's parts, left to right: [dish] Doneness [+ side], or "…" for someone queueing.
        // A delivery order has the app's badge in front, with the ring that runs down round it.
        private void Draw(Bubble bubble, Order order, string content, bool delivery)
        {
            UiKit.Clear(bubble.Body);
            bubble.Fills.Clear();
            bubble.Ring = null;
            bubble.Content = content;

            var parts = new List<(Sprite Icon, string Text, Color Colour)>();
            if (content == Waiting)
            {
                parts.Add((null, Waiting, _ink));
            }
            else
            {
                var main = _icons != null ? _icons.Find(order.MainTypeId) : null;
                parts.Add(main != null ? (main, null, Color.white) : (null, Name(order.MainTypeId), _ink));
                var doneness = order.Doneness.Name;
                parts.Add((null, Tr.Get(doneness), _style != null ? OnPaper(_style.DonenessColor(doneness)) : _ink));
                if (order.SideTypeId != null)
                {
                    var side = _icons != null ? _icons.Find(order.SideTypeId) : null;
                    parts.Add((null, "+", _ink));
                    parts.Add(side != null ? (side, null, Color.white) : (null, Name(order.SideTypeId), _ink));
                }
            }

            // The outline and tail under the paper, then the contents on top.
            var widths = new float[parts.Count];
            var lead = delivery ? Badge + Gap : 0f;
            var width = Pad + lead;
            var font = _style != null ? _style.Bold : null;
            for (var i = 0; i < parts.Count; i++)
            {
                widths[i] = parts[i].Icon != null ? Icon : Measure(font, parts[i].Text);
                width += widths[i] + Gap;
            }
            width = Mathf.Max(width - Gap + Pad, Height);
            var total = Height + Tail * 0.5f;
            bubble.Body.sizeDelta = new Vector2(width, total);
            bubble.Size = new Vector2(width, total);

            var tailSide = Tail * 0.75f;
            var tailX = width / 2f - tailSide / 2f;
            var tailY = Height - tailSide / 2f - Stroke + 1f;
            Tilted(UiKit.RoundBox(bubble.Body, "TailEdge", _ink, tailX - Stroke, tailY - Stroke, tailSide + 2f * Stroke, tailSide + 2f * Stroke, 2f));
            UiKit.RoundBox(bubble.Body, "Edge", _ink, 0f, 0f, width, Height, Radius);
            bubble.Fills.Add(UiKit.RoundBox(bubble.Body, "Paper", _paper, Stroke, Stroke, width - 2f * Stroke, Height - 2f * Stroke, Radius - Stroke));
            bubble.Fills.Add(Tilted(UiKit.RoundBox(bubble.Body, "Tail", _paper, tailX, tailY, tailSide, tailSide, 1f)));

            if (delivery)
                DrawBadge(bubble);

            var x = Pad + lead;
            for (var i = 0; i < parts.Count; i++)
            {
                if (parts[i].Icon != null)
                    UiKit.Picture(bubble.Body, "Dish", parts[i].Icon, x, (Height - Icon) / 2f, Icon, Icon);
                else
                    UiKit.TextAt(font, bubble.Body, "Word", parts[i].Text, TextSize, parts[i].Colour, x, Height / 2f + TextSize * 0.36f, widths[i] + 4f);
                x += widths[i] + Gap;
            }
        }

        // The app's mark: a lime disc with the couriers' white chevron, inside a ring that empties as
        // the courier gets nearer. Placeholder until the app has a logo.
        private void DrawBadge(Bubble bubble)
        {
            var top = (Height - Badge) / 2f;
            UiKit.Ellipse(bubble.Body, "RingTrack", new Color(_ink.r, _ink.g, _ink.b, 0.18f), Pad, top, Badge, Badge);
            var ring = UiKit.Ellipse(bubble.Body, "Ring", _ink, Pad, top, Badge, Badge);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = false;
            bubble.Ring = ring;
            const float inset = 5f;
            UiKit.Ellipse(bubble.Body, "App", _app, Pad + inset, top + inset, Badge - 2f * inset, Badge - 2f * inset);
            // The chevron: two short bars leaning together.
            var centre = new Vector2(Pad + Badge / 2f, top + Badge / 2f);
            foreach (var side in new[] { -1f, 1f })
            {
                var bar = UiKit.RoundBox(bubble.Body, "Chevron", _appMark, 0f, 0f, 13f, 5f, 2.5f).rectTransform;
                bar.pivot = new Vector2(0.5f, 0.5f);
                bar.anchoredPosition = new Vector2(centre.x + side * 4.2f, -centre.y);
                bar.localRotation = Quaternion.Euler(0f, 0f, side * -42f);
            }
        }

        private float Measure(TMP_FontAsset font, string text)
        {
            var probe = UiKit.TextAt(font, _canvas, "Measure", text, TextSize, _ink, 0f, 0f, 400f);
            var width = UiKit.Width(probe);
            DestroyImmediate(probe.gameObject);
            return width;
        }

        // A square turned 45° about its own centre: the tail's point.
        private static Image Tilted(Image image)
        {
            var rect = image.rectTransform;
            var centre = rect.anchoredPosition + new Vector2(rect.sizeDelta.x / 2f, -rect.sizeDelta.y / 2f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = centre;
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return image;
        }

        // The HUD's doneness colours are tuned for dark cards; on paper they need more weight.
        private static Color OnPaper(Color colour)
        {
            Color.RGBToHSV(colour, out var h, out var s, out var v);
            return Color.HSVToRGB(h, Mathf.Min(1f, s * 1.15f + 0.1f), v * 0.78f);
        }

        private static string KeyFor(Order order) => $"{order.MainTypeId}|{order.Doneness.Name}|{order.SideTypeId}";

        private string Name(string typeId) => _catalog != null ? _catalog.DisplayName(typeId) : typeId;

        private sealed class Bubble
        {
            public RectTransform Root;
            public RectTransform Body;
            public CanvasGroup Group;
            public readonly List<Image> Fills = new List<Image>();
            public string Content;
            public Vector2 Anchor;
            public Vector2 Size;
            public float Patience = 1f;
            public float Target;
            public float Phase;
            public Image Ring;
            public float Born = -1f;
        }
    }
}
