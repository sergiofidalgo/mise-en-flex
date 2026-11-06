using System.Collections.Generic;
using Game.Gameplay;
using Game.Gameplay.Customers;
using Game.Simulation.Service;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// A marker at the edge of the screen for each customer out of view, on the side they're on: a plain
    /// dot for someone walking in or queueing, a round timer while they wait for food, a plate while they
    /// eat, and a face for a moment as they leave. Icons are drawn in code: TODO(Phase 7) real art.
    /// </summary>
    public sealed class CustomerIndicatorHud : MonoBehaviour
    {
        private const int IconResolution = 64;

        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private CustomerDirector _customers;

        [Tooltip("Canvas pixels: how big a marker is.")]
        [SerializeField, Min(8f)] private float _size = 54f;
        [Tooltip("Canvas pixels from the edge of the screen to the middle of a marker.")]
        [SerializeField, Min(0f)] private float _edgeMargin = 44f;
        [Tooltip("The same at the top of the screen, clear of the clock and money.")]
        [SerializeField, Min(0f)] private float _topMargin = 100f;
        [Tooltip("Metres above a customer's feet that have to be in view for them to count as seen.")]
        [SerializeField, Min(0f)] private float _seenHeight = 1f;
        [Tooltip("Real seconds the face shows as they leave.")]
        [SerializeField, Min(0f)] private float _faceSeconds = 2.5f;
        [Tooltip("Stars from which someone leaves happy.")]
        [SerializeField, Range(0f, 5f)] private float _happyFromStars = 4f;
        [Tooltip("Stars from which someone leaves neutral.")]
        [SerializeField, Range(0f, 5f)] private float _neutralFromStars = 2.5f;
        [SerializeField] private Color _background = new Color(0.10f, 0.11f, 0.13f, 0.9f);
        [SerializeField] private Color _someone = new Color(0.55f, 0.60f, 0.70f);
        [SerializeField] private Color _happy = new Color(0.45f, 0.85f, 0.40f);
        [SerializeField] private Color _neutral = new Color(0.96f, 0.76f, 0.25f);
        [SerializeField] private Color _unhappy = new Color(0.95f, 0.30f, 0.26f);

        private readonly Dictionary<Customer, Marker> _markers = new Dictionary<Customer, Marker>();
        private readonly List<Customer> _gone = new List<Customer>();
        private readonly List<Marker> _showing = new List<Marker>();
        private Sprite _disc;
        private Sprite _plate;
        private readonly Dictionary<CustomerMood, Sprite> _faces = new Dictionary<CustomerMood, Sprite>();

        private void Awake()
        {
            _disc = MakeSprite((x, y) => Inside(x, y, 0f, 0f, 1f));
            _plate = MakeSprite((x, y) => Inside(x, y, 0f, 0f, 0.8f) && !(Inside(x, y, 0f, 0f, 0.6f) && !Inside(x, y, 0f, 0f, 0.5f)));
            _faces[CustomerMood.Happy] = MakeFace(-0.5f, 0.8f);
            _faces[CustomerMood.Neutral] = MakeFace(-0.35f, 0f);
            _faces[CustomerMood.Unhappy] = MakeFace(-0.25f, -0.8f);
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null || _root.Services == null)
                return;

            _showing.Clear();
            foreach (var pair in _customers.Agents)
            {
                var marker = MarkerFor(pair.Key);
                var world = pair.Value.transform.position + Vector3.up * _seenHeight;
                var viewport = camera.WorldToViewportPoint(world);
                var inView = viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;

                if (inView || !Dress(marker, pair.Key))
                {
                    marker.Root.gameObject.SetActive(false);
                    continue;
                }

                marker.Root.gameObject.SetActive(true);
                marker.Position = ToEdge(viewport);
                _showing.Add(marker);
            }

            SpreadOut();
            foreach (var marker in _showing)
                marker.Root.anchoredPosition = marker.Position;

            _gone.Clear();
            foreach (var customer in _markers.Keys)
            {
                if (!_customers.Agents.ContainsKey(customer))
                    _gone.Add(customer);
            }
            foreach (var customer in _gone)
            {
                Destroy(_markers[customer].Root.gameObject);
                _markers.Remove(customer);
            }
        }

        // Sets a marker up for what this customer's doing. False if there's nothing to show any more.
        private bool Dress(Marker marker, Customer customer)
        {
            var kind = CustomerIndicator.KindOf(customer);
            marker.Timer.gameObject.SetActive(kind == CustomerIndicatorKind.WaitingForFood);
            marker.Icon.gameObject.SetActive(kind != CustomerIndicatorKind.WaitingForFood);

            switch (kind)
            {
                case CustomerIndicatorKind.WaitingForFood:
                    marker.Timer.fillAmount = customer.Patience;
                    marker.Timer.color = _style.PatienceColor(customer.Patience);
                    break;
                case CustomerIndicatorKind.Eating:
                    marker.Icon.sprite = _plate;
                    marker.Icon.color = Color.white;
                    break;
                case CustomerIndicatorKind.Leaving:
                    if (marker.LeftAt < 0f)
                        marker.LeftAt = Time.unscaledTime;
                    if (Time.unscaledTime - marker.LeftAt > _faceSeconds)
                        return false;
                    var mood = CustomerIndicator.MoodOf(customer, _happyFromStars, _neutralFromStars);
                    marker.Icon.sprite = _faces[mood];
                    marker.Icon.color = mood == CustomerMood.Happy ? _happy : mood == CustomerMood.Neutral ? _neutral : _unhappy;
                    break;
                default:
                    marker.Icon.sprite = _disc;
                    marker.Icon.color = _someone;
                    break;
            }
            return true;
        }

        // On the line from the middle of the screen towards them, where it meets the edge.
        private Vector2 ToEdge(Vector3 viewport)
        {
            var size = _canvas.rect.size;
            var away = new Vector2((viewport.x - 0.5f) * size.x, (viewport.y - 0.5f) * size.y);
            if (viewport.z < 0f)
                away = -away;

            var right = size.x / 2f - _edgeMargin;
            var top = size.y / 2f - _topMargin;
            var bottom = size.y / 2f - _edgeMargin;
            var scale = 1f;
            if (Mathf.Abs(away.x) > right)
                scale = Mathf.Min(scale, right / Mathf.Abs(away.x));
            var vertical = away.y > 0f ? top : bottom;
            if (Mathf.Abs(away.y) > vertical)
                scale = Mathf.Min(scale, vertical / Mathf.Abs(away.y));
            return away * scale;
        }

        // A group out of view would all land on one spot: they shuffle along until each can be seen.
        private void SpreadOut()
        {
            for (var pass = 0; pass < 3; pass++)
            {
                for (var i = 0; i < _showing.Count; i++)
                {
                    for (var j = i + 1; j < _showing.Count; j++)
                    {
                        var between = _showing[j].Position - _showing[i].Position;
                        if (between.magnitude >= _size)
                            continue;
                        // Along the edge they're on, so they stay at the edge.
                        var alongX = Mathf.Abs(_showing[i].Position.y) > Mathf.Abs(_showing[i].Position.x) * _canvas.rect.height / _canvas.rect.width;
                        var push = (_size - between.magnitude) / 2f + 1f;
                        var direction = alongX ? new Vector2(between.x >= 0f ? 1f : -1f, 0f) : new Vector2(0f, between.y >= 0f ? 1f : -1f);
                        _showing[i].Position -= direction * push;
                        _showing[j].Position += direction * push;
                    }
                }
            }
        }

        private Marker MarkerFor(Customer customer)
        {
            if (_markers.TryGetValue(customer, out var marker))
                return marker;

            var root = NewImage("CustomerMarker", _canvas, _disc, _background);
            root.rectTransform.anchorMin = root.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            root.rectTransform.sizeDelta = Vector2.one * _size;

            var icon = NewImage("Icon", root.transform, _disc, _someone);
            Inset(icon.rectTransform, 0.14f);

            var timer = NewImage("Timer", root.transform, _disc, Color.white);
            Inset(timer.rectTransform, 0.1f);
            timer.type = Image.Type.Filled;
            timer.fillMethod = Image.FillMethod.Radial360;
            timer.fillOrigin = (int)Image.Origin360.Top;

            marker = new Marker { Root = root.rectTransform, Icon = icon, Timer = timer, LeftAt = -1f };
            _markers[customer] = marker;
            return marker;
        }

        private static void Inset(RectTransform rect, float share)
        {
            rect.anchorMin = Vector2.one * share;
            rect.anchorMax = Vector2.one * (1f - share);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static bool Inside(float x, float y, float centreX, float centreY, float radius)
        {
            var dx = x - centreX;
            var dy = y - centreY;
            return dx * dx + dy * dy <= radius * radius;
        }

        // A disc with two eyes and a mouth cut out: a positive curve is a smile, a negative one a frown.
        private static Sprite MakeFace(float mouthY, float curve) => MakeSprite((x, y) =>
        {
            if (!Inside(x, y, 0f, 0f, 1f))
                return false;
            if (Inside(x, y, -0.35f, 0.3f, 0.14f) || Inside(x, y, 0.35f, 0.3f, 0.14f))
                return false;
            var mouth = mouthY + curve * x * x;
            return !(Mathf.Abs(x) < 0.5f && Mathf.Abs(y - mouth) < 0.09f);
        });

        private static Sprite MakeSprite(System.Func<float, float, bool> filled)
        {
            var texture = new Texture2D(IconResolution, IconResolution, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color[IconResolution * IconResolution];
            for (var row = 0; row < IconResolution; row++)
            {
                for (var column = 0; column < IconResolution; column++)
                {
                    var x = (column + 0.5f) / IconResolution * 2f - 1f;
                    var y = (row + 0.5f) / IconResolution * 2f - 1f;
                    pixels[row * IconResolution + column] = filled(x, y) ? Color.white : new Color(1f, 1f, 1f, 0f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, IconResolution, IconResolution), Vector2.one * 0.5f);
        }

        private sealed class Marker
        {
            public RectTransform Root;
            public Image Icon;
            public Image Timer;
            public float LeftAt;
            public Vector2 Position;
        }
    }
}
