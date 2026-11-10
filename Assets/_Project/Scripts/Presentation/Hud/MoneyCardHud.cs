using Game.Presentation.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The campaign's money, always in the top-right corner (decision 2026-11-10): in the office and
    /// in every kitchen, through every phase. It mirrors the office's player cards on the right: the
    /// same height, rounding, outline width and display face, with a coin and the amount.
    /// <list type="bullet">
    /// <item>Money in: the number rolls up in green and settles back to cream.</item>
    /// <item>Money out: the new number at once, red, with a short gentle shake.</item>
    /// <item>Below zero, owed to the boss: a minus sign, in red.</item>
    /// </list>
    /// During service it counts each payment as it's made (what this service has taken so far, on
    /// top of the balance), although the books bank it as Review starts. It hides while a full-screen
    /// page is up (the Review dashboard, the pause menu, the miniature's panel) and catches up when
    /// it's back, so a change made behind them still plays. Built in code on its own canvas, inside
    /// the screen's margin at any resolution or aspect.
    /// </summary>
    public sealed class MoneyCardHud : MonoBehaviour
    {
        // The player bar's: the same distance from the edges and the same outline and corners.
        private const float Margin = 24f;
        private const float Stroke = 4f;
        private const float Radius = 18f;
        private const float MinWidth = 210f;   // a player card's width
        private const float Pad = 22f;
        private const float CoinSize = 48f;
        private const float CoinGap = 16f;
        private const int SortingOrder = 15;   // over the kitchen HUD, under the dashboard, phone and pause

        [SerializeField] private HudStyle _style;
        [SerializeField] private PauseMenu _pause;

        private Canvas _canvas;
        private RectTransform _card;
        private CanvasGroup _group;
        private Image _edge;
        private Image _back;
        private TextMeshProUGUI _amount;
        private MoneyTicker _ticker;
        private float _width = MinWidth;
        private bool _known;          // a balance has been shown
        private IMoneySource _source; // the kitchen's or the office's, on the same object

        private void Awake()
        {
            if (_canvas != null)
                return;
            _ticker = new MoneyTicker(_style != null ? _style.MoneyTicker : MoneyTickerSettings.Default);
            _source = GetComponent<IMoneySource>();
            Build();
        }

        private void Build()
        {
            var canvasObject = new GameObject("MoneyCard", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var height = Height;
            _card = UiKit.Rect(_canvas.transform, "Money", 0f, 0f, MinWidth, height);
            _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(1f, 1f);
            _card.anchoredPosition = new Vector2(-Margin, -Margin);
            _group = _card.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var background = _style != null ? _style.CardBackground : new Color(0.07f, 0.08f, 0.10f, 0.92f);
            _back = UiKit.StrokedBox(_card, "Back", background, Edge, Stroke, 0f, 0f, MinWidth, height, Radius);
            _edge = _card.Find("BackEdge").GetComponent<Image>();

            // The coin: a gold disc with a darker rim and a lighter face, the HUD's flat toon shapes.
            var coinY = (height - CoinSize) / 2f;
            UiKit.StrokedCircle(_card, "Coin", Color.Lerp(Edge, Color.white, 0.25f), Color.Lerp(Edge, Color.black, 0.3f), 4f, Pad, coinY, CoinSize);
            UiKit.Ellipse(_card, "CoinFace", Color.Lerp(Edge, Color.black, 0.15f), Pad + CoinSize * 0.25f, coinY + CoinSize * 0.25f, CoinSize * 0.5f, CoinSize * 0.5f);

            var fontSize = _style != null ? _style.MoneyFontSize : 44f;
            var font = _style != null ? _style.DisplayFont : null;
            // Right-aligned, so the last digits stay put while the number rolls.
            var right = MinWidth - Pad;
            var baseline = height / 2f + fontSize * 0.36f;
            _amount = UiKit.TextAt(font, _card, "Amount", "", fontSize, TextColour, right, baseline, MinWidth - 2f * Pad - CoinSize - CoinGap, HorizontalAlignmentOptions.Right);
            _amount.rectTransform.anchorMin = _amount.rectTransform.anchorMax = new Vector2(1f, 1f);
            _amount.rectTransform.pivot = new Vector2(1f, 1f);
            _amount.rectTransform.anchoredPosition = new Vector2(-Pad, -(baseline - fontSize));
        }

        private float Height => _style != null ? _style.MoneyCardHeight : 108f;
        private Color Edge => _style != null ? _style.MoneyEdge : new Color(0.96f, 0.76f, 0.25f);
        private Color TextColour => _style != null ? _style.TextColor : Color.white;
        private Color Gain => _style != null ? _style.MoneyGain : Color.green;
        private Color Loss => _style != null ? _style.MoneyLoss : Color.red;

        private void LateUpdate()
        {
            if (_canvas == null)
                return;
            var dt = Time.unscaledDeltaTime;
            var reset = false;
            var balance = _source?.Read(out reset);
            var shown = balance.HasValue && _canvas.enabled && !(_pause != null && _pause.IsOpen) && !_source.Covered;
            Fade(shown, dt);
            if (!shown)
                return; // a change made now plays when it's back

            if (!_known || reset)
            {
                _ticker.Snap(balance.Value);
                _known = true;
            }
            else
            {
                _ticker.Set(balance.Value);
            }
            _ticker.Tick(dt);
            Draw();
        }

        /// <summary>
        /// Draws the card at once outside Play mode, for a still: <paramref name="balance"/>, or a
        /// change from <paramref name="from"/> caught <paramref name="seconds"/> into its animation.
        /// </summary>
        public void Preview(float balance, float? from = null, float seconds = 0f)
        {
            Awake();
            _ticker.Snap(from ?? balance);
            if (from.HasValue)
            {
                _ticker.Set(balance);
                _ticker.Tick(seconds);
            }
            _group.alpha = 1f;
            Draw();
            Canvas.ForceUpdateCanvases();
        }

        private void Draw()
        {
            var text = MoneyTicker.Format(_ticker.Shown);
            // The normal colour is cream, or red while owed to the boss; a change tints it green
            // (in) or red (out), fading back.
            var resting = _ticker.Target < 0f ? Loss : TextColour;
            var change = _ticker.Direction == MoneyChange.Up ? Gain : Loss;
            _amount.color = Color.Lerp(resting, change, _ticker.Tint);
            _amount.text = text;

            // As wide as the amount needs, never narrower than a player card. While it's moving it
            // only grows, so a roll through 999 → 1,000 doesn't make the card twitch.
            var needed = Mathf.Max(MinWidth, Pad * 2f + CoinSize + CoinGap + Mathf.Max(UiKit.Width(_amount), MeasureTarget()));
            var canvasWidth = ((RectTransform)_canvas.transform).rect.width;
            if (canvasWidth > 1f)
                needed = Mathf.Min(needed, canvasWidth - 2f * Margin); // whole on screen, always
            _width = _ticker.Animating ? Mathf.Max(_width, needed) : needed;
            Resize(_width);
            _card.anchoredPosition = new Vector2(-Margin + _ticker.Offset, -Margin);
        }

        // The width of the figure the card is heading to, so it's sized for where the roll ends.
        private float MeasureTarget() =>
            _amount.GetPreferredValues(MoneyTicker.Format(_ticker.Target), 10000f, 10000f).x;

        private void Resize(float width)
        {
            var height = Height;
            _card.sizeDelta = new Vector2(width, height);
            var half = Stroke / 2f;
            _edge.rectTransform.sizeDelta = new Vector2(width + Stroke, height + Stroke);
            _back.rectTransform.sizeDelta = new Vector2(width - Stroke, height - Stroke);
            _edge.rectTransform.anchoredPosition = new Vector2(-half, half);
            _back.rectTransform.anchoredPosition = new Vector2(half, -half);
            _amount.rectTransform.sizeDelta = new Vector2(width - 2f * Pad - CoinSize - CoinGap, _amount.rectTransform.sizeDelta.y);
        }

        private void Fade(bool shown, float dt)
        {
            var fade = _style != null ? _style.Fade : 0.12f;
            var goal = shown ? 1f : 0f;
            _group.alpha = fade <= 0f ? goal : Mathf.MoveTowards(_group.alpha, goal, dt / fade);
        }
    }
}
