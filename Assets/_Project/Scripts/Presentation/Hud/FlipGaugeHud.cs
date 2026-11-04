using System.Collections.Generic;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Simulation.Cooking;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// A gauge under each pan whose food hasn't had its first flip: a needle moves left to right
    /// with the browning of the side facing down, through a green band towards burnt. It appears
    /// when the food touches the pan and goes as soon as it's flipped; from then on the player
    /// reads the doneness themselves (decision 2026-11-03). Built in code: it's a few plain bars.
    /// </summary>
    public sealed class FlipGaugeHud : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private KitchenCompositionRoot _root;

        [Tooltip("Canvas pixels: the gauge's size.")]
        [SerializeField] private Vector2 _size = new Vector2(170f, 16f);
        [Tooltip("Canvas pixels from the food to the gauge's centre; negative is down, under the doneness tag.")]
        [SerializeField] private float _offsetY = -66f;
        [SerializeField, Min(1f)] private float _needleWidth = 5f;
        [Tooltip("Gauge size while the needle is in the green band, so it catches the eye.")]
        [SerializeField, Min(1f)] private float _goodScale = 1.15f;
        [SerializeField] private Color _track = new Color(0.10f, 0.11f, 0.13f, 0.9f);
        [SerializeField] private Color _good = new Color(0.45f, 0.85f, 0.40f);
        [SerializeField] private Color _burning = new Color(0.86f, 0.25f, 0.22f);
        [SerializeField] private Color _needle = Color.white;
        [Tooltip("Where the red end of the gauge starts, as browning.")]
        [SerializeField, Min(0f)] private float _burningFrom = 1.6f;

        private readonly Dictionary<HobStation, Gauge> _gauges = new Dictionary<HobStation, Gauge>();

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null || _root.Services == null)
                return;

            var settings = _root.Services.Tuning.FlipGauge;
            foreach (var hob in _root.Services.Stations.All.OfType<HobStation>())
            {
                var gauge = GaugeFor(hob);
                var pan = hob.Hob?.Pan;
                var food = pan != null && pan.Contents.Count > 0 ? pan.Contents[0] : null;
                if (food == null || !FlipGauge.Shows(food) || !_root.Services.ItemViews.TryGetView(food, out var view))
                {
                    gauge.Root.gameObject.SetActive(false);
                    continue;
                }

                var screen = camera.WorldToScreenPoint(view.transform.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out var local);
                gauge.Root.gameObject.SetActive(screen.z > 0f);
                gauge.Root.anchoredPosition = local + new Vector2(0f, _offsetY);

                var properties = food.Properties;
                SetSpan(gauge.Good, FlipGauge.ToPosition(settings.GoodFrom, properties), FlipGauge.ToPosition(settings.GoodTo, properties));
                SetSpan(gauge.Burning, FlipGauge.ToPosition(_burningFrom, properties), 1f);

                var position = FlipGauge.Position(food);
                gauge.Needle.anchorMin = new Vector2(position, 0f);
                gauge.Needle.anchorMax = new Vector2(position, 1f);
                gauge.Root.localScale = Vector3.one * (FlipGauge.IsInGoodBand(food, settings) ? _goodScale : 1f);
            }
        }

        private static void SetSpan(RectTransform bar, float from, float to)
        {
            bar.anchorMin = new Vector2(from, 0f);
            bar.anchorMax = new Vector2(to, 1f);
        }

        private Gauge GaugeFor(HobStation hob)
        {
            if (_gauges.TryGetValue(hob, out var gauge))
                return gauge;

            var root = Bar("FlipGauge", _canvas, _track);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = _size;
            gauge = new Gauge
            {
                Root = root,
                Good = Bar("Good", root, _good),
                Burning = Bar("Burning", root, _burning),
                Needle = Bar("Needle", root, _needle),
            };
            gauge.Needle.sizeDelta = new Vector2(_needleWidth, 0f);
            root.gameObject.SetActive(false);
            _gauges[hob] = gauge;
            return gauge;
        }

        private static RectTransform Bar(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private sealed class Gauge
        {
            public RectTransform Root;
            public RectTransform Good;
            public RectTransform Burning;
            public RectTransform Needle;
        }
    }
}
