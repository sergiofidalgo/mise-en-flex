using Game.Gameplay;
using Game.Presentation.Menu;
using Game.Simulation.Days;
using Game.Simulation.Service;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The kitchen's day card, bottom left (decision 2026-11-10, after the user's request): "Day 3"
    /// and under it the phase and the game clock, "Service · 18:42". The money card's sibling but
    /// quieter: shorter, a little see-through, a thin sand outline and softer text, and nothing
    /// moves but the minutes. Last orders and Closed show in the notice amber. It hides with the
    /// money card behind the Review dashboard and the pause menu. Built in code on its own canvas,
    /// inside the screen's margin at any resolution or aspect; the boss's phone is bottom right.
    /// </summary>
    public sealed class DayCardHud : MonoBehaviour
    {
        private const float Margin = 24f;      // the money card's and the player bar's
        private const float Radius = 18f;
        private const float MinWidth = 210f;
        private const float Pad = 20f;
        private const float LineGap = 6f;
        private const int SortingOrder = 15;

        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private PauseMenu _pause;

        private Canvas _canvas;
        private RectTransform _card;
        private CanvasGroup _group;
        private Image _edge;
        private Image _back;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _line;
        private string _shown;

        private void Awake()
        {
            if (_canvas == null)
                Build();
        }

        private void Build()
        {
            var canvasObject = new GameObject("DayCard", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var height = _style.DayCardHeight;
            _card = UiKit.Rect(_canvas.transform, "Day", 0f, 0f, MinWidth, height);
            _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(0f, 0f);
            _card.anchoredPosition = new Vector2(Margin, Margin);
            _group = _card.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var fill = _style.CardBackground;
            fill.a *= _style.DayCardOpacity;
            _back = UiKit.StrokedBox(_card, "Back", fill, _style.DayCardEdge, _style.DayCardStroke, 0f, 0f, MinWidth, height, Radius);
            _edge = _card.Find("BackEdge").GetComponent<Image>();

            // Two lines centred in the card as a block: the title's cap height, a gap, the line's.
            var block = _style.DayTitleSize * 0.72f + LineGap + _style.DayLineSize * 0.72f;
            var top = (height - block) / 2f;
            _title = UiKit.TextAt(_style.DisplayFont, _card, "Title", "", _style.DayTitleSize, _style.DayText, Pad, top + _style.DayTitleSize * 0.72f, 600f);
            _line = UiKit.TextAt(_style.Bold, _card, "Line", "", _style.DayLineSize, _style.DayText, Pad, top + block, 600f);
        }

        private void LateUpdate()
        {
            if (_canvas == null)
                return;
            var services = _root != null ? _root.Services : null;
            var shown = services != null && services.Days != null && services.Days.Phase != DayPhase.Review && !(_pause != null && _pause.IsOpen);
            var fade = _style.Fade;
            var goal = shown ? 1f : 0f;
            _group.alpha = fade <= 0f ? goal : Mathf.MoveTowards(_group.alpha, goal, Time.unscaledDeltaTime / fade);
            if (!shown)
                return;
            var service = services.Service;
            Draw(services.Days.Day, services.Days.Phase, service != null ? service.Phase : ServicePhase.Open, services.ServiceConfig.OpensAt, service != null ? service.Elapsed : 0f);
        }

        /// <summary>Draws the card at once outside Play mode, for a still.</summary>
        public void Preview(int day, DayPhase phase, ServicePhase service, float opensAtHour, float elapsedSeconds)
        {
            Awake();
            _group.alpha = 1f;
            Draw(day, phase, service, opensAtHour, elapsedSeconds);
            Canvas.ForceUpdateCanvases();
        }

        private void Draw(int day, DayPhase phase, ServicePhase service, float opensAtHour, float elapsedSeconds)
        {
            var title = DayCardText.Title(day);
            var (line, notice) = DayCardText.Line(phase, service, opensAtHour, elapsedSeconds);
            var key = title + "\n" + line + notice;
            if (key == _shown)
                return; // the text changes once a game minute, so the layout does too
            _shown = key;
            _title.text = title;
            _line.text = line;
            _line.color = notice ? _style.DayNotice : _style.DayText;

            var width = Mathf.Max(MinWidth, Pad * 2f + Mathf.Max(UiKit.Width(_title), UiKit.Width(_line)));
            var canvasWidth = ((RectTransform)_canvas.transform).rect.width;
            if (canvasWidth > 1f)
                width = Mathf.Min(width, canvasWidth - 2f * Margin); // whole on screen, always
            var height = _style.DayCardHeight;
            var stroke = _style.DayCardStroke;
            _card.sizeDelta = new Vector2(width, height);
            _edge.rectTransform.sizeDelta = new Vector2(width + stroke, height + stroke);
            _back.rectTransform.sizeDelta = new Vector2(width - stroke, height - stroke);
            _title.rectTransform.sizeDelta = new Vector2(width - 2f * Pad, _title.rectTransform.sizeDelta.y);
            _line.rectTransform.sizeDelta = new Vector2(width - 2f * Pad, _line.rectTransform.sizeDelta.y);
        }
    }
}
