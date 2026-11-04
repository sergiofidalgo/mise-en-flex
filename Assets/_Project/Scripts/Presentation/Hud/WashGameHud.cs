using Game.Gameplay.Stations;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The washing game beside the sink: a circle with a target in the middle, the point the player
    /// balances (green while it's washing well, red when it isn't), and a bar for the plate's
    /// progress. Built in code: it's a few plain shapes.
    /// </summary>
    public sealed class WashGameHud : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [SerializeField] private SinkStation _sink;

        [Tooltip("Canvas pixels: the circle's diameter.")]
        [SerializeField, Min(1f)] private float _diameter = 190f;
        [SerializeField, Min(1f)] private float _pointSize = 24f;
        [Tooltip("Canvas pixels from the sink to the circle's centre.")]
        [SerializeField] private Vector2 _offset = new Vector2(-190f, 130f);
        [Tooltip("Fraction of the circle shown as the target. Matches where washing reaches full speed.")]
        [SerializeField, Range(0f, 1f)] private float _targetRadius = 0.25f;
        [SerializeField, Min(1f)] private float _barHeight = 10f;
        [SerializeField] private Color _rim = new Color(0.10f, 0.11f, 0.13f, 0.9f);
        [SerializeField] private Color _target = new Color(0.25f, 0.45f, 0.28f, 1f);
        [SerializeField] private Color _good = new Color(0.45f, 0.85f, 0.40f);
        [SerializeField] private Color _bad = new Color(0.95f, 0.30f, 0.26f);
        [SerializeField] private Color _barColor = new Color(0.55f, 0.80f, 0.95f);

        private RectTransform _root;
        private RectTransform _point;
        private Image _pointImage;
        private RectTransform _barFill;

        private void Start() => Build();

        private void LateUpdate()
        {
            var game = _sink.Game;
            var camera = Camera.main;
            var show = game != null && camera != null;
            _root.gameObject.SetActive(show);
            if (!show)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas, camera.WorldToScreenPoint(_sink.transform.position), null, out var local);
            _root.anchoredPosition = local + _offset;

            var radius = _diameter / 2f;
            _point.anchoredPosition = new Vector2(game.Point.X, game.Point.Y) * radius;
            _pointImage.color = Color.Lerp(_bad, _good, game.Rate);
            _barFill.anchorMax = new Vector2(Mathf.Clamp01(_sink.Sink.WashProgress), 1f);
        }

        private void Build()
        {
            _root = Disc("WashGame", _canvas, _rim, _diameter);
            Disc("Target", _root, _target, _diameter * _targetRadius * 2f);
            _point = Disc("Point", _root, _good, _pointSize);
            _pointImage = _point.GetComponent<Image>();

            var bar = Plain("Bar", _root, _rim);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(_diameter, _barHeight);
            bar.anchoredPosition = new Vector2(0f, -8f);
            _barFill = Plain("Fill", bar, _barColor);
            _barFill.anchorMin = Vector2.zero;
            _barFill.anchorMax = new Vector2(0f, 1f);
            _barFill.offsetMin = _barFill.offsetMax = Vector2.zero;
            _root.gameObject.SetActive(false);
        }

        private RectTransform Disc(string name, Transform parent, Color color, float size)
        {
            var rect = Plain(name, parent, color);
            rect.GetComponent<Image>().sprite = _style.Circle;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * size;
            return rect;
        }

        private static RectTransform Plain(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return (RectTransform)go.transform;
        }
    }
}
