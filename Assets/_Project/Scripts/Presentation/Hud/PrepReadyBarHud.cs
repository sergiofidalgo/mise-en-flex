using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Gameplay;
using Game.Presentation.Menu;
using Game.Simulation.Days;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The kitchen's ready bar in Prep (the user's design, 2026-11-11): the forkcast dashboard's
    /// footer bar (ReadyLanes) along the bottom of the screen, one segment per joined cook. A cook
    /// still prepping reads "Y Hold to start service" in their segment, which fills in their colour
    /// as they hold Y; once ready it reads "B Player N READY", B taking it back, as on the dashboard. Hidden
    /// outside Prep and behind the dashboard and the pause menu. Built in code on its own canvas.
    /// </summary>
    public sealed class PrepReadyBarHud : MonoBehaviour
    {
        private const float Height = 62f; // the dashboard footer's
        private const int SortingOrder = 14;

        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private DayLoop _dayLoop;
        [SerializeField] private CoopSettings _settings;
        [SerializeField] private PauseMenu _pause;
        [SerializeField] private Color _light = new Color(1f, 0.973f, 0.925f);
        [SerializeField] private Color _ink = new Color(0.169f, 0.129f, 0.106f);

        private Canvas _canvas;
        private RectTransform _bar;
        private CanvasGroup _group;
        private ReadyLanes _lanes;
        private string _shown;
        private readonly List<int> _players = new List<int>();

        /// <summary>How many segments it shows, for tests.</summary>
        public int Segments => _lanes != null ? _lanes.Count : 0;

        private void Awake()
        {
            if (_canvas == null)
                Build();
        }

        private void Build()
        {
            var canvasObject = new GameObject("PrepReadyBar", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Flush with the screen's bottom and sides, the full width at any aspect.
            _bar = UiKit.Rect(_canvas.transform, "Bar", 0f, 0f, 1920f, Height);
            _bar.anchorMin = new Vector2(0f, 0f);
            _bar.anchorMax = new Vector2(1f, 0f);
            _bar.pivot = new Vector2(0.5f, 0f);
            _bar.anchoredPosition = Vector2.zero;
            _bar.sizeDelta = new Vector2(0f, Height);
            _group = _bar.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        private ReadyLanes Lanes()
        {
            var width = ((RectTransform)_canvas.transform).rect.width;
            if (width < 1f)
                width = 1920f;
            if (_lanes != null && Mathf.Abs(_laneWidth - width) < 0.5f)
                return _lanes;
            _laneWidth = width;
            UiKit.Clear(_bar);
            var holder = UiKit.Rect(_bar, "Lanes", 0f, 0f, width, Height);
            _lanes = new ReadyLanes(holder, width, Height, new ReadyLanes.Look
            {
                ReadyFont = _style.ExtraBold,
                WaitingFont = _style.Bold,
                BadgeFont = _style.Bold,
                FontSize = 21f,
                Light = _light,
                Ink = _ink,
            });
            _shown = null;
            return _lanes;
        }

        private float _laneWidth;

        private void LateUpdate()
        {
            if (_canvas == null)
                return;
            var services = _root != null ? _root.Services : null;
            var shown = services != null && services.Days != null && services.Days.Phase == DayPhase.Prep
                && !services.Forkcast.IsOpen && !(_pause != null && _pause.IsOpen) && PlayerInput.all.Count > 0;
            var fade = _style.Fade;
            var goal = shown ? 1f : 0f;
            _group.alpha = fade <= 0f ? goal : Mathf.MoveTowards(_group.alpha, goal, Time.unscaledDeltaTime / fade);
            if (!shown)
                return;
            Draw(Time.unscaledDeltaTime);
        }

        private void Draw(float dt)
        {
            _players.Clear();
            _players.AddRange(PlayerInput.all.Where(p => !PlayerPresence.IsLost(p)).Select(p => p.playerIndex).OrderBy(i => i));
            var lanes = Lanes();
            lanes.Build(_players, _settings.GetPlayerColor);
            var state = string.Join(",", _players.Select(p => $"{p}:{_dayLoop.Ready.IsReady(p)}")) + "|" + Locale.Language;
            if (state != _shown)
            {
                _shown = state;
                var y = new ReadyLanes.Glyph("Y", _style.BadgeColor(Game.Gameplay.Stations.PromptControl.North, true));
            var b = new ReadyLanes.Glyph("B", _style.BadgeColor(Game.Gameplay.Stations.PromptControl.East, true));
                lanes.Label(p => _dayLoop.Ready.IsReady(p), (player, ready) => ready
                    ? (Tr.Format("Player {player} READY", ("player", player + 1)), b)
                    : (Tr.Get("Hold to start service"), y));
            }
            lanes.Tick(dt, p => _dayLoop.Ready.IsReady(p), p => p >= 0 ? _dayLoop.ReadyHoldProgress(p) : 0f);
        }

        /// <summary>Draws the bar at once outside Play mode, for a still: these players, these ready, these holding.</summary>
        public Canvas Preview(IReadOnlyList<int> players, IReadOnlyCollection<int> ready, IReadOnlyDictionary<int, float> holding)
        {
            Awake();
            _group.alpha = 1f;
            var lanes = Lanes();
            lanes.Build(players, _settings.GetPlayerColor);
            var y = new ReadyLanes.Glyph("Y", _style.BadgeColor(Game.Gameplay.Stations.PromptControl.North, true));
            var b = new ReadyLanes.Glyph("B", _style.BadgeColor(Game.Gameplay.Stations.PromptControl.East, true));
            lanes.Label(ready.Contains, (player, isReady) => isReady
                ? (Tr.Format("Player {player} READY", ("player", player + 1)), b)
                : (Tr.Get("Hold to start service"), y));
            // Long enough for the fills to settle where they're held.
            for (var i = 0; i < 60; i++)
                lanes.Tick(0.1f, ready.Contains, p => holding.TryGetValue(p, out var held) ? held : 0f);
            Canvas.ForceUpdateCanvases();
            return _canvas;
        }
    }
}
