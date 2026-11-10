using Game.Data;
using Game.Presentation.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The giant screen over the restaurant's door in the opening (the user's choice, 2026-11-10:
    /// the game is about social media and reviews, and the street should say so): forkcast's live
    /// page for the boss's restaurant. Its rating and stars, a count of hearts ticking up, and
    /// reviews from made-up handles swapping every few seconds. A canvas in the world, sized to the
    /// screen's frame, drawn with the house UI kit; it glows as a screen does, whatever the street's light.
    /// </summary>
    public sealed class FeedScreen : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.13f, 0.09f, 0.08f);
        private static readonly Color Cream = new Color(0.96f, 0.92f, 0.84f);
        private static readonly Color Gold = new Color(1f, 0.74f, 0.18f);
        private static readonly Color Pink = new Color(0.98f, 0.33f, 0.52f);
        private static readonly Color Live = new Color(0.9f, 0.22f, 0.2f);

        // Canvas pixels; the builder sizes the canvas's transform so this fills the screen's frame.
        public const float Width = 1152f, Height = 648f;

        [SerializeField] private HudStyle _style;
        [SerializeField] private Sprite _logo;
        [Tooltip("Seconds each review stays up.")]
        [SerializeField, Min(1f)] private float _reviewSeconds = 3.5f;
        [Tooltip("Hearts at the start, and how many arrive each second on average.")]
        [SerializeField] private int _hearts = 12400;
        [SerializeField, Min(0f)] private float _heartsPerSecond = 9f;

        private static readonly (string Handle, string Line, int Stars, Color Avatar)[] Reviews =
        {
            ("@ines.eats", "Chef's kiss, no notes.", 5, new Color(0.45f, 0.75f, 0.5f)),
            ("@forkyeah", "Worth every minute of the queue.", 5, new Color(0.95f, 0.6f, 0.3f)),
            ("@crumb.daily", "The steak. Just the steak.", 5, new Color(0.5f, 0.6f, 0.95f)),
            ("@table4two", "Loud, bright, perfect.", 4, new Color(0.9f, 0.45f, 0.6f)),
            ("@saucy.sam", "I filmed the whole plate.", 5, new Color(0.7f, 0.5f, 0.9f)),
        };

        private RectTransform _layout;
        private RectTransform _review;
        private TextMeshProUGUI _count;
        private RectTransform _heart;
        private Image _liveDot;
        private int _shown = -1;
        private float _heartsNow;

        private void Awake()
        {
            if (_layout == null)
                Build();
        }

        /// <summary>Draws the screen at once, without Play mode, for the capture tool.</summary>
        public void BuildForCapture()
        {
            if (_layout != null)
                DestroyImmediate(_layout.parent.gameObject);
            Build();
        }

        private void Update()
        {
            // A new review every few seconds.
            var index = Mathf.FloorToInt(Time.time / _reviewSeconds) % Reviews.Length;
            if (index != _shown)
                ShowReview(index);
            // It slides up into place.
            var age = Mathf.Repeat(Time.time, _reviewSeconds);
            var settle = 1f - Mathf.Clamp01(age / 0.35f);
            _review.anchoredPosition = new Vector2(_reviewAt.x, _reviewAt.y - settle * settle * 40f);

            // Hearts keep coming; the heart beats.
            _heartsNow += _heartsPerSecond * Time.deltaTime * (0.6f + 0.8f * Mathf.PerlinNoise(Time.time * 0.7f, 3f));
            _count.text = Count(_hearts + Mathf.FloorToInt(_heartsNow));
            var beat = 1f + 0.12f * Mathf.Max(0f, Mathf.Sin(Time.time * 6f));
            _heart.localScale = Vector3.one * beat;
            _liveDot.color = new Color(1f, 1f, 1f, Mathf.Repeat(Time.time, 1.2f) < 0.7f ? 1f : 0.25f);
        }

        // 12,431 as "12.4k", the way feeds count.
        private static string Count(int hearts) =>
            hearts >= 1000 ? (hearts / 100 / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "k" : hearts.ToString();

        private Vector2 _reviewAt;

        private void Build()
        {
            var canvasObject = new GameObject("FeedCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 2f;
            var root = canvasObject.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(Width, Height);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            _layout = UiKit.Rect(root, "Layout", 0f, 0f, Width, Height);
            _layout.anchorMin = _layout.anchorMax = _layout.pivot = new Vector2(0.5f, 0.5f);
            _layout.anchoredPosition = Vector2.zero;
            UiKit.RoundBox(_layout, "Back", Ink, 0f, 0f, Width, Height, 0f);

            // The header: forkcast, and LIVE.
            if (_logo != null)
                UiKit.Picture(_layout, "Logo", _logo, 40f, 34f, 264f, 72f);
            UiKit.RoundBox(_layout, "Live", Live, Width - 232f, 38f, 192f, 64f, 32f);
            _liveDot = UiKit.Ellipse(_layout, "LiveDot", Color.white, Width - 210f, 58f, 24f, 24f);
            UiKit.TextAt(_style.Bold, _layout, "LiveText", Tr.Get("LIVE"), 38f, Color.white, Width - 172f, 84f, 130f);

            // The restaurant: its name, its rating in stars, the hearts.
            UiKit.TextAt(_style.Bold, _layout, "Name", "Mise en Flex", 84f, Cream, 44f, 226f, 700f);
            UiKit.TextAt(_style.Bold, _layout, "Rating", "4.8", 132f, Gold, 44f, 372f, 260f);
            UiKit.StarRow(_layout, 300f, 276f, 74f, 84f, 5f, Gold, new Color(1f, 1f, 1f, 0.2f));
            UiKit.RoundBox(_layout, "HeartsPill", Pink, 760f, 150f, 348f, 108f, 54f);
            _heart = UiKit.Rect(_layout, "Heart", 786f, 172f, 64f, 64f);
            _heart.pivot = new Vector2(0.5f, 0.5f);
            _heart.anchoredPosition = new Vector2(818f, -204f);
            Heart(_heart, 64f, Color.white);
            _count = UiKit.TextAt(_style.Bold, _layout, "Hearts", Count(_hearts), 64f, Color.white, 866f, 228f, 240f);

            // The review of the moment: a paper card with an avatar, a handle, stars and a line.
            _reviewAt = new Vector2(44f, -(Height - 210f));
            _review = UiKit.Rect(_layout, "Review", 44f, Height - 210f, Width - 88f, 170f);
            ShowReview(0);
        }

        private void ShowReview(int index)
        {
            _shown = index;
            UiKit.Clear(_review);
            var (handle, line, stars, avatar) = Reviews[index];
            var width = _review.sizeDelta.x;
            UiKit.RoundBox(_review, "Card", Cream, 0f, 0f, width, 170f, 30f);
            UiKit.Ellipse(_review, "Avatar", avatar, 26f, 26f, 118f, 118f);
            UiKit.TextAt(_style.Bold, _review, "Handle", handle, 44f, Ink, 170f, 70f, 420f);
            UiKit.StarRow(_review, width - 330f, 30f, 50f, 58f, stars, Gold, new Color(0f, 0f, 0f, 0.15f));
            UiKit.TextAt(_style.Medium, _review, "Line", "“" + Tr.Get(line) + "”", 46f, Ink, 170f, 134f, width - 200f);
        }

        // A heart from two discs and a diamond, in the kit's shapes.
        private static void Heart(RectTransform parent, float size, Color colour)
        {
            var r = size * 0.3f;
            UiKit.Ellipse(parent, "L", colour, size * 0.5f - r * 1.6f, size * 0.12f, r * 2f, r * 2f);
            UiKit.Ellipse(parent, "R", colour, size * 0.5f - r * 0.4f, size * 0.12f, r * 2f, r * 2f);
            var point = UiKit.RoundBox(parent, "Point", colour, 0f, 0f, size * 0.52f, size * 0.52f, 4f).rectTransform;
            point.pivot = new Vector2(0.5f, 0.5f);
            point.anchoredPosition = new Vector2(size * 0.5f, -size * 0.56f);
            point.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
    }
}
