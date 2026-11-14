using Game.Presentation.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The campaign's money, always in the top-right corner (decisions 2026-11-10, 2026-11-13 and
    /// 2026-11-14: top right, the players top left): in the office and
    /// in every kitchen, through every phase. A name plate like the office's player cards, which
    /// sit in the other corner: ink outline, a gold band, a coin medallion breaking out over the
    /// top edge, and the amount in the display face.
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
        // The player bar's distance from the edges; a name plate like the player cards (2026-11-13).
        private const float Margin = 24f;
        private const float MinWidth = 240f;
        private const float Pad = 22f;
        private const float CoinSize = 52f;    // the coin medallion's inside
        private const float CoinGap = 14f;
        private const int SortingOrder = 15;   // over the kitchen HUD, under the dashboard, phone and pause

        [SerializeField] private HudStyle _style;
        [SerializeField] private PauseMenu _pause;

        private Canvas _canvas;
        private RectTransform _card;
        private CanvasGroup _group;
        private RectTransform _plate;          // the plate, redrawn when the width changes
        private float _drawnWidth = -1f;
        private TextMeshProUGUI _amount;
        private MoneyTicker _ticker;
        private float _width = MinWidth;

        /// <summary>The card's width now, so what docks under it can match (Prep's Rearrange).</summary>
        public float Width => _width;

        /// <summary>How far down the card reaches from its top margin, its coin's breakout included.</summary>
        public float CardHeight => Breakout + Height;

        /// <summary>False: the card sits in the top-right corner (the user's call, 2026-11-14), so what docks under it goes right too.</summary>
        public bool OnLeft => false;
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
            var top = Breakout;
            _card = UiKit.Rect(_canvas.transform, "Money", 0f, 0f, MinWidth, top + height);
            _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(1f, 1f);
            _card.anchoredPosition = new Vector2(-Margin, -Margin);
            _group = _card.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _plate = UiKit.Rect(_card, "Plate", 0f, 0f, MinWidth, top + height);

            var fontSize = _style != null ? _style.MoneyFontSize : 44f;
            var font = _style != null ? _style.DisplayFont : null;
            // Right-aligned, so the last digits stay put while the number rolls.
            var baseline = top + height / 2f + fontSize * 0.36f;
            _amount = UiKit.TextAt(font, _card, "Amount", "", fontSize, TextColour, MinWidth - Pad, baseline, MinWidth - 2f * Pad - CoinOuter - CoinGap, HorizontalAlignmentOptions.Right);
            _amount.rectTransform.anchorMin = _amount.rectTransform.anchorMax = new Vector2(1f, 1f);
            _amount.rectTransform.pivot = new Vector2(1f, 1f);
            _amount.rectTransform.anchoredPosition = new Vector2(-Pad, -(baseline - fontSize));
        }

        // The plate and its coin medallion over the left end, breaking out over the top edge.
        private void DrawPlate(float width)
        {
            UiKit.Clear(_plate);
            _plate.sizeDelta = new Vector2(width, Breakout + Height);
            NamePlate.Draw(_style, _plate, Edge, 0f, Breakout, width, Height);
            var cx = 14f + CoinOuter / 2f;
            var coin = NamePlate.Medallion(_style, _plate, Edge, cx, CoinOuter / 2f, CoinSize);
            // The coin's face: gold, a darker disc in the middle, the HUD's flat toon shapes.
            UiKit.Ellipse(coin, "Gold", Color.Lerp(Edge, Color.white, 0.25f), 0f, 0f, CoinSize, CoinSize);
            UiKit.Ellipse(coin, "CoinFace", Color.Lerp(Edge, Color.black, 0.15f), CoinSize * 0.25f, CoinSize * 0.25f, CoinSize * 0.5f, CoinSize * 0.5f);
            _amount.transform.SetAsLastSibling();
        }

        private float Breakout => _style != null ? _style.PlateBreakout : 22f;
        private float CoinOuter => CoinSize + 2f * ((_style != null ? _style.PlateRing : 4f) + (_style != null ? _style.PlateStroke : 5f));

        private float Height => _style != null ? _style.PlateHeight : 76f;
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
                _ticker.Set(balance.Value, _source.TakePaidOut());
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
            var needed = Mathf.Max(MinWidth, Pad + 14f + CoinOuter + CoinGap + Mathf.Max(UiKit.Width(_amount), MeasureTarget()));
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
            width = Mathf.Round(width);
            _card.sizeDelta = new Vector2(width, Breakout + Height);
            if (!Mathf.Approximately(width, _drawnWidth))
            {
                _drawnWidth = width;
                DrawPlate(width);
            }
            _amount.rectTransform.sizeDelta = new Vector2(width - Pad - 14f - CoinOuter - CoinGap, _amount.rectTransform.sizeDelta.y);
        }

        private void Fade(bool shown, float dt)
        {
            var fade = _style != null ? _style.Fade : 0.12f;
            var goal = shown ? 1f : 0f;
            _group.alpha = fade <= 0f ? goal : Mathf.MoveTowards(_group.alpha, goal, dt / fade);
        }
    }
}
