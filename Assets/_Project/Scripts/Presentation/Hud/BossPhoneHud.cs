using System.Collections.Generic;
using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Debt;
using Game.Gameplay.Saving;
using Game.Simulation.Campaign;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The personal mobile, bottom right (§15.6): when the boss calls, it slides up ringing, shows
    /// his few short lines one after another, and slides away. It needs no button, so it never
    /// competes with the dashboard or the kitchen for one, and it never names a key. It sits inside
    /// the 1920×1080 layout, a margin from both edges, so no screen shape clips it.
    /// <para>
    /// Also the boss's voice in person (a bubble over his head during a morning visit) and the way
    /// out after the worst of it: back to the office when he takes the restaurant you're in, back
    /// to the start after erasure.
    /// </para>
    /// </summary>
    public sealed class BossPhoneHud : MonoBehaviour
    {
        private const float Width = 420f, Height = 178f, Margin = 32f;
        // How far the phone rises while forkcast is open: above its footer and Start button.
        private const float ForkcastLift = 112f;

        private static readonly Color Body = new Color(0.10f, 0.09f, 0.09f, 0.96f);
        private static readonly Color Screen = new Color(0.17f, 0.14f, 0.13f);
        private static readonly Color Cream = new Color(0.95f, 0.91f, 0.82f);
        private static readonly Color Sand = new Color(0.78f, 0.70f, 0.56f);
        private static readonly Color Camel = new Color(0.72f, 0.52f, 0.30f);
        private static readonly Color Ring = new Color(0.42f, 0.68f, 0.29f);
        private static readonly Color Bad = new Color(0.90f, 0.34f, 0.28f);

        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private BossVisitDirector _visit;
        [SerializeField] private Camera _camera;
        [SerializeField] private string _officeScene = "Office_Greybox";
        [Tooltip("Seconds it rings before he speaks.")]
        [SerializeField, Min(0f)] private float _ringSeconds = 1.2f;
        [Tooltip("Seconds each line stays up.")]
        [SerializeField, Min(0.5f)] private float _secondsPerLine = 2.6f;
        [Tooltip("Seconds the phone takes to slide in or out.")]
        [SerializeField, Min(0.05f)] private float _slideSeconds = 0.25f;
        [Tooltip("Seconds the erasure card stays before the game goes back to the start.")]
        [SerializeField, Min(0.5f)] private float _erasedSeconds = 4f;

        private readonly Queue<(BossCallKind Kind, IReadOnlyList<string> Lines)> _calls = new Queue<(BossCallKind, IReadOnlyList<string>)>();
        private RectTransform _canvas;
        private RectTransform _phone;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _line;
        private Image _ringDot;
        private WorldLabel _bubble;
        private CanvasGroup _ending;
        private TextMeshProUGUI _endingText;

        private IReadOnlyList<string> _lines;
        private BossCallKind _kind;
        private float _age;
        private float _shown; // 0 hidden to 1 up
        private float _leaveAt = -1f;
        private bool _leavingForOffice;

        private void Awake()
        {
            var canvasObject = new GameObject("BossPhoneCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40; // over the dashboard: a call is short and wanted
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            _canvas = (RectTransform)canvasObject.transform;

            // The 1920×1080 layout, centred: Expand keeps all of it on screen.
            var layout = UiKit.Rect(_canvas, "Layout", 0f, 0f, 1920f, 1080f);
            layout.anchorMin = layout.anchorMax = layout.pivot = new Vector2(0.5f, 0.5f);
            layout.anchoredPosition = Vector2.zero;

            _phone = UiKit.Rect(layout, "Phone", 1920f - Margin - Width, 1080f - Margin - Height, Width, Height);
            UiKit.RoundBox(_phone, "Body", Body, 0f, 0f, Width, Height, 26f);
            UiKit.RoundBox(_phone, "Screen", Screen, 10f, 10f, Width - 20f, Height - 20f, 18f);
            // His picture: a camel suit under a white hat.
            UiKit.Ellipse(_phone, "Avatar", Camel, 26f, 24f, 64f, 64f);
            UiKit.RoundBox(_phone, "Hat", Cream, 20f, 22f, 76f, 14f, 7f);
            UiKit.RoundBox(_phone, "Crown", Cream, 38f, 8f, 40f, 20f, 8f);
            UiKit.TextAt(_style.ExtraBold, _phone, "Name", Tr.Get("THE BOSS"), 22f, Cream, 106f, 46f, 200f);
            _ringDot = UiKit.Ellipse(_phone, "RingDot", Ring, Width - 44f, 30f, 16f, 16f);
            _status = UiKit.TextAt(_style.Medium, _phone, "Status", "", 18f, Sand, 106f, 74f, Width - 160f);
            _line = UiKit.TextAt(_style.Bold, _phone, "Line", "", 23f, Cream, 26f, 120f, Width - 52f, lines: 2);
            _phone.gameObject.SetActive(false);

            _bubble = WorldLabel.Create(_style, _canvas, "BossSays", new Vector2(18f, 10f), new Vector2(0.5f, 0f));
            _bubble.Font = _style.Bold;

            // The end of a campaign: a dark card over everything.
            var ending = UiKit.Rect(layout, "Erased", -400f, -400f, 2720f, 1880f);
            _ending = ending.gameObject.AddComponent<CanvasGroup>();
            _ending.alpha = 0f;
            _ending.blocksRaycasts = false;
            UiKit.RoundBox(ending, "Dark", new Color(0.05f, 0.04f, 0.04f, 0.97f), 0f, 0f, 2720f, 1880f, 0f);
            _endingText = UiKit.TextAt(_style.ExtraBold, ending, "Text", "", 54f, Cream, 400f + 960f, 400f + 520f, 1400f, HorizontalAlignmentOptions.Center, lines: 3);
        }

        private void Start()
        {
            _root.Phone.Rang += HandleRang;
            if (_visit != null)
            {
                _visit.LeftForOffice += HandleLeftForOffice;
                _visit.Erased += HandleErased;
            }
        }

        private void OnDestroy()
        {
            if (_root != null)
                _root.Phone.Rang -= HandleRang;
            if (_visit != null)
            {
                _visit.LeftForOffice -= HandleLeftForOffice;
                _visit.Erased -= HandleErased;
            }
        }

        private void HandleRang(BossCallKind kind, IReadOnlyList<string> lines) => _calls.Enqueue((kind, lines));

        private void Update()
        {
            UpdateCall();
            UpdateBubble();
            UpdateEnding();
        }

        // ---- The call ----

        private void UpdateCall()
        {
            if (_lines == null && _calls.Count > 0)
            {
                (_kind, _lines) = _calls.Dequeue();
                _age = 0f;
                _phone.gameObject.SetActive(true);
            }
            var callLength = _lines != null ? _ringSeconds + _lines.Count * _secondsPerLine : 0f;
            var up = _lines != null && _age < callLength;
            _shown = Mathf.MoveTowards(_shown, up ? 1f : 0f, Time.unscaledDeltaTime / _slideSeconds);
            var eased = _shown * _shown * (3f - 2f * _shown);
            // Over forkcast it rings above the footer, clear of the Start button (2026-11-10).
            var lift = _root.Services != null && _root.Services.Forkcast.IsOpen ? ForkcastLift : 0f;
            _phone.anchoredPosition = new Vector2(1920f - Margin - Width, -(1080f - Margin - Height - lift) - (1f - eased) * (Height + Margin + 20f + lift));

            if (_lines == null)
            {
                if (_shown <= 0f)
                    _phone.gameObject.SetActive(false);
                return;
            }
            _age += Time.unscaledDeltaTime;
            if (_age < _ringSeconds)
            {
                // Ringing: the dot pulses and the phone shakes a little.
                _status.text = Tr.Get("Incoming call…");
                _line.text = "";
                _ringDot.color = Color.Lerp(Ring, Cream, 0.5f + 0.5f * Mathf.Sin(_age * 18f));
                _phone.anchoredPosition += new Vector2(Mathf.Sin(_age * 60f) * 3f, 0f);
            }
            else if (_age < callLength)
            {
                var index = Mathf.Min(_lines.Count - 1, (int)((_age - _ringSeconds) / _secondsPerLine));
                _status.text = StatusFor(_kind);
                _line.text = Tr.Get(_lines[index]);
                _ringDot.color = _kind == BossCallKind.Missed || _kind == BossCallKind.Near ? Bad : Ring;
            }
            else if (_shown <= 0f)
            {
                _lines = null; // hung up: the next call, if any, rings next frame
            }
        }

        private static string StatusFor(BossCallKind kind)
        {
            switch (kind)
            {
                case BossCallKind.Near: return Tr.Get("Collection tonight");
                case BossCallKind.Missed: return Tr.Get("Collection missed");
                case BossCallKind.Paid:
                case BossCallKind.BackInGoodBooks: return Tr.Get("Collection paid");
                default: return "";
            }
        }

        // ---- In person ----

        private void UpdateBubble()
        {
            var boss = _visit != null ? _visit.BossAgent : null;
            var saying = _visit != null ? _visit.Saying : "";
            if (boss == null || string.IsNullOrEmpty(saying) || _camera == null)
            {
                _bubble.Hide();
                return;
            }
            _bubble.Show(Tr.Get(saying), boss.transform.position + Vector3.up * 2.45f, _camera, _canvas);
        }

        // ---- The way out ----

        private void HandleLeftForOffice()
        {
            _leavingForOffice = true;
            _endingText.text = Tr.Get("He took the restaurant.\nBack to the office.");
            _leaveAt = Time.unscaledTime + _erasedSeconds * 0.6f;
        }

        private void HandleErased()
        {
            _leavingForOffice = false;
            _endingText.text = Tr.Get("Erased.\nYour cookbook stays in the boss's office for the next chef.");
            _leaveAt = Time.unscaledTime + _erasedSeconds;
        }

        private void UpdateEnding()
        {
            if (_leaveAt < 0f)
                return;
            _ending.alpha = Mathf.MoveTowards(_ending.alpha, 1f, Time.unscaledDeltaTime / 0.6f);
            if (Time.unscaledTime < _leaveAt)
                return;
            _leaveAt = -1f;
            Time.timeScale = 1f;
            if (_leavingForOffice)
            {
                // The same players carry on in the office, in the same campaign.
                PlayerRoster.CarryEveryone();
                CampaignLaunch.Continue(CampaignLaunch.Slot);
            }
            else
            {
                // The campaign is gone: back to the start, as quitting to the menu does.
                PlayerRoster.Take();
                CampaignLaunch.Reset();
                Office.OpeningSequence.StartAtMenu = true;
            }
            SceneManager.LoadScene(_officeScene);
        }
    }
}
