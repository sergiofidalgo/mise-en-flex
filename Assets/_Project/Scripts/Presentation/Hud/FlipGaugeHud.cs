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
    /// One bar under each pan with food that needs flipping. Until the first flip a needle moves with
    /// the browning of the side facing down, through a green band towards burnt. After it, the bar shows
    /// the stages of doneness, with the needle following the middle of the food. Either way it pulses
    /// red when the side in the pan is about to burn (decisions 2026-11-03 and 06). Built in code:
    /// it's a few plain bars.
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
        [Tooltip("The stages after the first flip, in order: raw, rare, medium, well done.")]
        [SerializeField] private Color[] _stageColors =
        {
            new Color(0.62f, 0.55f, 0.85f),
            new Color(0.95f, 0.25f, 0.28f),
            new Color(1.00f, 0.62f, 0.60f),
            new Color(0.66f, 0.62f, 0.58f),
        };
        [Tooltip("Times per second the bar pulses when something's about to burn.")]
        [SerializeField, Min(0f)] private float _warningPulses = 4f;
        [Tooltip("Bar size at the top of a pulse.")]
        [SerializeField, Min(1f)] private float _warningScale = 1.25f;

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
                var stages = food != null && FlipGauge.ShowsStages(food);
                if (food == null || !(stages || FlipGauge.Shows(food)) || !_root.Services.ItemViews.TryGetView(food, out var view))
                {
                    gauge.Root.gameObject.SetActive(false);
                    continue;
                }

                var screen = camera.WorldToScreenPoint(view.transform.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out var local);
                gauge.Root.gameObject.SetActive(screen.z > 0f);
                gauge.Root.anchoredPosition = local + new Vector2(0f, _offsetY);

                var properties = food.Properties;
                gauge.Good.gameObject.SetActive(!stages);
                gauge.Burning.gameObject.SetActive(!stages);
                if (!stages)
                {
                    SetSpan(gauge.Good, FlipGauge.ToPosition(settings.GoodFrom, properties), FlipGauge.ToPosition(settings.GoodTo, properties));
                    SetSpan(gauge.Burning, FlipGauge.ToPosition(_burningFrom, properties), 1f);
                }

                var stageCount = FlipGauge.StageCount(properties);
                for (var i = 0; i < gauge.Stages.Length; i++)
                {
                    var show = stages && i < stageCount;
                    gauge.Stages[i].gameObject.SetActive(show);
                    if (show)
                        SetSpan(gauge.Stages[i], i / (float)stageCount, (i + 1) / (float)stageCount);
                }

                var position = stages ? FlipGauge.StagePosition(food) : FlipGauge.Position(food);
                gauge.Needle.anchorMin = new Vector2(position, 0f);
                gauge.Needle.anchorMax = new Vector2(position, 1f);

                var warning = FlipGauge.IsAboutToBurn(food, settings);
                gauge.Warning.gameObject.SetActive(warning);
                var pulse = warning ? (Mathf.Sin(Time.unscaledTime * _warningPulses * Mathf.PI * 2f) + 1f) / 2f : 0f;
                if (warning)
                    gauge.WarningImage.color = new Color(_burning.r, _burning.g, _burning.b, Mathf.Lerp(0.35f, 0.9f, pulse));
                var scale = warning ? Mathf.Lerp(1f, _warningScale, pulse)
                    : !stages && FlipGauge.IsInGoodBand(food, settings) ? _goodScale : 1f;
                gauge.Root.localScale = Vector3.one * scale;
                gauge.Needle.SetAsLastSibling();
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
                Stages = new RectTransform[_stageColors.Length],
                Needle = Bar("Needle", root, _needle),
            };
            for (var i = 0; i < gauge.Stages.Length; i++)
                gauge.Stages[i] = Bar($"Stage_{i + 1}", root, _stageColors[i]);
            gauge.Warning = Bar("Warning", root, _burning);
            gauge.Warning.anchorMin = Vector2.zero;
            gauge.Warning.anchorMax = Vector2.one;
            gauge.WarningImage = gauge.Warning.GetComponent<Image>();
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
            public RectTransform[] Stages;
            public RectTransform Warning;
            public Image WarningImage;
        }
    }
}
