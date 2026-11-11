using Game.Data;
using Game.Gameplay;
using Game.Simulation.Days;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The screen transitions into and out of service (the 2026-11-11 playtest round, look approved
    /// from mockups the same day):
    /// <list type="bullet">
    /// <item>Into service, a 4-3-2-1 in a wooden badge at the top while the chefs keep working
    /// (<see cref="ServiceCountdown"/>), then a "Service!" band sweeps across as the doors open.</item>
    /// <item>Out of it, a wooden roller shutter drops with a "Closing time" sign. The dashboard rises
    /// over it, and the shutter goes out of focus behind it for the whole summary.</item>
    /// <item>Into the next morning's Prep, the dashboard drops away, the shutter comes back into focus
    /// with "Day N" on its sign, and rolls up on the kitchen.</item>
    /// </list>
    /// Its canvases sit over the kitchen HUD and under the dashboard (20); the phone (40) and the
    /// pause menu (1000) stay over everything. Timings are real seconds: they're presentation, and
    /// carry on while the game is paused.
    /// </summary>
    public sealed class DayTransitions : MonoBehaviour
    {
        public enum Stage
        {
            /// <summary>No shutter: the kitchen is in view.</summary>
            Open,
            /// <summary>The shutter dropping, then the dashboard rising over it.</summary>
            Closing,
            /// <summary>Down and blurred behind the dashboard.</summary>
            Shut,
            /// <summary>The morning: the dashboard dropping away, then the shutter rolling up.</summary>
            Opening,
        }

        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private HudStyle _style;
        [Tooltip("The end-of-day dashboard, which rises over the shutter.")]
        [SerializeField] private ReviewDashboard _dashboard;
        [SerializeField] private Shader _shutterShader;

        [Header("Palette (art bible)")]
        [SerializeField] private Color _ink = new Color32(0x3A, 0x24, 0x14, 0xFF);
        [SerializeField] private Color _wood = new Color32(0x5C, 0x3A, 0x21, 0xFF);
        [SerializeField] private Color _woodMid = new Color32(0x8B, 0x5A, 0x33, 0xFF);
        [SerializeField] private Color _cream = new Color32(0xEA, 0xDF, 0xC6, 0xFF);
        [SerializeField] private Color _orange = new Color32(0xE8, 0x87, 0x3A, 0xFF);
        [SerializeField] private Color _warn = new Color32(0xE0, 0xB3, 0x41, 0xFF);
        [SerializeField] private Color _bad = new Color32(0xD9, 0x49, 0x3B, 0xFF);

        [Header("Countdown")]
        [Tooltip("Each number pops in this much bigger, then settles.")]
        [SerializeField, Min(1f)] private float _numberPop = 1.35f;
        [SerializeField, Min(0.01f)] private float _numberSettle = 0.18f;

        [Header("Service! band")]
        [SerializeField, Min(0.01f)] private float _bandIn = 0.28f;
        [SerializeField, Min(0f)] private float _bandHold = 0.75f;
        [SerializeField, Min(0.01f)] private float _bandOut = 0.25f;

        [Header("Closing")]
        [Tooltip("Seconds for the shutter to drop, with a small bounce as it lands.")]
        [SerializeField, Min(0.01f)] private float _drop = 0.55f;
        [Tooltip("Seconds the shut sign swings before the dashboard rises.")]
        [SerializeField, Min(0f)] private float _signHold = 0.4f;
        [Tooltip("Seconds for the dashboard to rise while the shutter blurs.")]
        [SerializeField, Min(0.01f)] private float _rise = 0.4f;
        [Tooltip("How dark the shutter gets behind the dashboard (the dashboard's own tint adds to it).")]
        [SerializeField, Range(0f, 1f)] private float _shutDim = 0.35f;

        [Header("Morning")]
        [Tooltip("Seconds for the dashboard to drop away while the shutter comes into focus.")]
        [SerializeField, Min(0.01f)] private float _sharpen = 0.35f;
        [Tooltip("Seconds the day's sign shows before the shutter rolls up.")]
        [SerializeField, Min(0f)] private float _dayHold = 0.35f;
        [SerializeField, Min(0.01f)] private float _rollUp = 0.6f;

        private const float Reference = 1080f;
        private const float SlatHeight = 54f;
        private const float BadgeSize = 300f;
        private const float BadgeTop = 110f;
        private const float BandHeight = 230f;
        private const float BandTilt = 3.5f;

        private static readonly int BlurId = Shader.PropertyToID("_Blur");
        private static readonly int DimId = Shader.PropertyToID("_Dim");
        private static readonly int SlatsId = Shader.PropertyToID("_Slats");

        private RectTransform _cueCanvas;
        private RectTransform _shutterCanvas;
        private RectTransform _cardCanvas;

        private CanvasGroup _badge;
        private Image _ringFill;
        private TextMeshProUGUI _number;
        private int _shownNumber;
        private float _numberAge;

        private RectTransform _band;
        private TextMeshProUGUI _bandTitle;
        private float _bandAge = -1f;

        private RectTransform _shutter;
        private Material _slats;
        private RectTransform _sign;
        private CanvasGroup _signGroup;
        private TextMeshProUGUI _signText;
        private float _swingAge;

        private Stage _stage = Stage.Open;
        private float _stageAge;
        private float _blur;
        private RectTransform _leaving; // the dashboard's copy, dropping away in the morning

        /// <summary>Where the shutter is, for tests and captures.</summary>
        public Stage Current => _stage;

        /// <summary>Whether the 4-3-2-1 is on screen, and its number.</summary>
        public bool CountdownShowing => _badge != null && _badge.gameObject.activeSelf;
        public int CountdownNumber => _shownNumber;

        /// <summary>Whether the "Service!" band is on screen.</summary>
        public bool ServiceBandShowing => _band != null && _band.gameObject.activeSelf;

        /// <summary>What the shutter's sign says now.</summary>
        public string SignText => _signText != null ? _signText.text : "";

        private KitchenServices Services => _root != null ? _root.Services : null;

        private void Start()
        {
            Build();
            if (Services == null)
                return;
            Services.Forkcast.Changed += HandleForkcastChanged;
            Services.Countdown.Finished += HandleDoorsOpen;
            // A kitchen that loads on the dashboard has the shutter already down behind it.
            if (Services.Forkcast.IsOpen)
                SetShut();
        }

        private void OnDestroy()
        {
            if (Services != null)
            {
                Services.Forkcast.Changed -= HandleForkcastChanged;
                Services.Countdown.Finished -= HandleDoorsOpen;
            }
            if (_slats != null)
                Destroy(_slats);
        }

        private void HandleForkcastChanged(bool open)
        {
            if (open)
            {
                // From a service: the shutter comes down. Otherwise (a load) it's simply down.
                if (Services.Days.Phase == DayPhase.Review)
                    Begin(Stage.Closing);
                else
                    SetShut();
                return;
            }
            Begin(Stage.Opening);
        }

        private void HandleDoorsOpen()
        {
            if (Services.Days.Phase != DayPhase.Service)
                return;
            _bandAge = 0f;
            _band.gameObject.SetActive(true);
        }

        private void Begin(Stage stage)
        {
            _stage = stage;
            _stageAge = 0f;
            _swingAge = 0f;
            _shutter.gameObject.SetActive(true);
            if (stage == Stage.Closing)
            {
                _signText.text = Tr.Get("Closing time");
                _signText.color = _bad;
                _signGroup.alpha = 1f;
                _blur = 0f;
                SetDashboard(0f, -Reference);
            }
            else if (stage == Stage.Opening)
            {
                _signText.text = Tr.Format("Day {day}", ("day", Services.Days.Day));
                _signText.color = _ink;
                _signGroup.alpha = 0f;
                CopyLeavingDashboard();
            }
        }

        private void SetShut()
        {
            _stage = Stage.Shut;
            _stageAge = 0f;
            _blur = 1f;
            _shutter.gameObject.SetActive(true);
            _shutter.anchoredPosition = Vector2.zero;
            _signGroup.alpha = 0f;
            SetDashboard(1f, 0f);
        }

        private void Update()
        {
            if (Services == null || _shutter == null)
                return;
            var dt = Time.unscaledDeltaTime;
            UpdateCountdown(dt);
            UpdateBand(dt);
            UpdateShutter(dt);
        }

        // The dashboard draws the kitchen's photo, blurred, behind itself; while the shutter stands
        // behind it, that would hide the shutter (its tint stays, darkening the shutter a little more).
        private void LateUpdate()
        {
            if (_stage != Stage.Open && Backdrop is { } backdrop)
                backdrop.enabled = false;
        }

        // ---- The countdown and the band ----

        private void UpdateCountdown(float dt)
        {
            var countdown = Services.Countdown;
            var showing = countdown.Running && Services.Days.Phase == DayPhase.Service;
            if (showing != _badge.gameObject.activeSelf)
            {
                _badge.gameObject.SetActive(showing);
                _badge.alpha = 0f;
                _shownNumber = 0;
            }
            if (!showing)
                return;
            _badge.alpha = Mathf.MoveTowards(_badge.alpha, 1f, dt / 0.15f);
            if (countdown.Shown != _shownNumber)
            {
                _shownNumber = countdown.Shown;
                _number.text = _shownNumber.ToString();
                // The last second in the timers-running-low yellow.
                _number.color = _shownNumber <= 1 ? _warn : _cream;
                _numberAge = 0f;
            }
            _numberAge += dt;
            var settle = Mathf.Clamp01(_numberAge / _numberSettle);
            var scale = Mathf.Lerp(_numberPop, 1f, 1f - (1f - settle) * (1f - settle));
            _number.rectTransform.localScale = new Vector3(scale, scale, 1f);
            // The ring runs down through each second.
            _ringFill.fillAmount = 1f - countdown.StepProgress;
        }

        private void UpdateBand(float dt)
        {
            if (_bandAge < 0f)
                return;
            _bandAge += dt;
            var width = _cueCanvas.rect.width + 600f;
            float x;
            if (_bandAge < _bandIn)
                x = Mathf.Lerp(-width, 0f, EaseOutBack(_bandAge / _bandIn, 1.2f));
            else if (_bandAge < _bandIn + _bandHold)
                x = 0f;
            else
            {
                var t = (_bandAge - _bandIn - _bandHold) / _bandOut;
                if (t >= 1f)
                {
                    _bandAge = -1f;
                    _band.gameObject.SetActive(false);
                    return;
                }
                x = Mathf.Lerp(0f, width, t * t);
            }
            _band.anchoredPosition = new Vector2(x, _band.anchoredPosition.y);
            // The title stamps in as the band lands.
            var stamp = Mathf.Clamp01((_bandAge - _bandIn * 0.6f) / 0.2f);
            var titleScale = Mathf.Lerp(1.3f, 1f, 1f - (1f - stamp) * (1f - stamp));
            _bandTitle.rectTransform.localScale = new Vector3(titleScale, titleScale, 1f);
        }

        // ---- The shutter ----

        private void UpdateShutter(float dt)
        {
            _stageAge += dt;
            _swingAge += dt;
            var height = _shutterCanvas.rect.height;
            _slats.SetFloat(SlatsId, height / SlatHeight);

            switch (_stage)
            {
                case Stage.Open:
                    _shutter.gameObject.SetActive(false);
                    return;

                case Stage.Closing:
                {
                    var drop = Mathf.Clamp01(_stageAge / _drop);
                    _shutter.anchoredPosition = new Vector2(0f, height * (1f - EaseOutBounce(drop)));
                    if (drop < 1f)
                        _swingAge = 0f; // it swings from the landing
                    var rise = Mathf.Clamp01((_stageAge - _drop - _signHold) / _rise);
                    _blur = rise;
                    _signGroup.alpha = 1f - Mathf.Clamp01(rise * 2f);
                    SetDashboard(Mathf.Clamp01(rise / 0.35f), -Reference * (1f - EaseOutBack(rise, 1.1f)));
                    if (rise >= 1f)
                        SetShut();
                    break;
                }

                case Stage.Shut:
                    _shutter.anchoredPosition = Vector2.zero;
                    break;

                case Stage.Opening:
                {
                    var sharpen = Mathf.Clamp01(_stageAge / _sharpen);
                    _blur = 1f - sharpen;
                    _signGroup.alpha = sharpen;
                    if (sharpen < 1f)
                        _swingAge = 0f;
                    if (_leaving != null)
                    {
                        _leaving.anchoredPosition = new Vector2(0f, -Reference * sharpen * sharpen);
                        if (sharpen >= 1f)
                        {
                            Destroy(_leaving.parent.gameObject);
                            _leaving = null;
                        }
                    }
                    var roll = Mathf.Clamp01((_stageAge - _sharpen - _dayHold) / _rollUp);
                    _shutter.anchoredPosition = new Vector2(0f, height * EaseInOut(roll));
                    if (roll >= 1f)
                    {
                        _stage = Stage.Open;
                        _shutter.gameObject.SetActive(false);
                        return;
                    }
                    break;
                }
            }

            _slats.SetFloat(BlurId, _blur);
            _slats.SetFloat(DimId, _shutDim * _blur);
            // The sign swings on its nail, a few degrees and dying away.
            var swing = 5f * Mathf.Exp(-3f * _swingAge) * Mathf.Sin(_swingAge * 11f);
            _sign.localRotation = Quaternion.Euler(0f, 0f, swing);
        }

        // ---- The dashboard, seen from here ----

        private RectTransform DashboardCanvas => _dashboard != null ? _dashboard.transform.Find("ReviewDashboard") as RectTransform : null;
        private RectTransform DashboardPanel => DashboardCanvas != null ? DashboardCanvas.Find("Layout") as RectTransform : null;
        private RawImage Backdrop => DashboardCanvas != null && DashboardCanvas.Find("Backdrop") is Transform t ? t.GetComponent<RawImage>() : null;

        /// <summary>Whether the dashboard's parts this needs are where it expects (a test keeps it so).</summary>
        public bool FindsDashboard => DashboardPanel != null && Backdrop != null;

        private void SetDashboard(float alpha, float y)
        {
            var canvas = DashboardCanvas;
            if (canvas == null)
                return;
            if (!canvas.TryGetComponent(out CanvasGroup group))
            {
                group = canvas.gameObject.AddComponent<CanvasGroup>();
                group.ignoreParentGroups = true;
            }
            group.alpha = alpha;
            if (DashboardPanel is { } panel)
                panel.anchoredPosition = new Vector2(0f, y);
        }

        // The dashboard is put away the moment Prep starts; a still copy of it drops off the screen.
        private void CopyLeavingDashboard()
        {
            if (_leaving != null)
                Destroy(_leaving.parent.gameObject);
            _leaving = null;
            SetDashboard(1f, 0f);
            var panel = DashboardPanel;
            if (panel == null)
                return;
            // Made inactive and stripped of the dashboard's own scripts before it ever wakes: only
            // the pictures and text are wanted.
            var holder = UiKit.Rect(_cardCanvas, "LeavingDashboard", 0f, 0f, 0f, 0f);
            UiKit.Fill(holder);
            holder.gameObject.SetActive(false);
            var copy = Instantiate(panel.gameObject, holder, false);
            foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var space = behaviour.GetType().Namespace ?? "";
                if (!space.StartsWith("UnityEngine") && !space.StartsWith("TMPro"))
                    DestroyImmediate(behaviour);
            }
            holder.gameObject.SetActive(true);
            _leaving = (RectTransform)copy.transform;
            _leaving.anchoredPosition = Vector2.zero;
        }

        // ---- Building ----

        private RectTransform NewCanvas(string name, int order)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, Reference);
            // As the dashboard's: the 1920×1080 layout always fits, so its copy lines up with it.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return (RectTransform)canvasObject.transform;
        }

        private void Build()
        {
            if (_shutter != null)
                return;
            // Over the money and day cards (15), under the dashboard (20).
            _cueCanvas = NewCanvas("TransitionCues", 16);
            _shutterCanvas = NewCanvas("Shutter", 19);
            _cardCanvas = NewCanvas("LeavingDashboard", 21);
            BuildBadge();
            BuildBand();
            BuildShutter();
        }

        private TMP_FontAsset Display => _style != null ? _style.DisplayFont : null;

        private TextMeshProUGUI Title(Transform parent, string name, string text, float size, Color colour, float outline)
        {
            var label = UiKit.TextAt(Display, parent, name, text, size, colour, 0f, 0f, 1600f, HorizontalAlignmentOptions.Center);
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1600f, size * 1.4f);
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.outlineWidth = outline;
            label.outlineColor = _ink;
            return label;
        }

        private static Image Centred(Transform parent, string name, Sprite sprite, Color colour, float width, float height, float x = 0f, float y = 0f)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.sizeDelta = new Vector2(width, height);
            image.rectTransform.anchoredPosition = new Vector2(x, y);
            image.sprite = sprite;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private void BuildBadge()
        {
            var badge = UiKit.Rect(_cueCanvas, "Countdown", 0f, 0f, BadgeSize, BadgeSize + 80f);
            badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 1f);
            badge.pivot = new Vector2(0.5f, 1f);
            badge.anchoredPosition = new Vector2(0f, -BadgeTop + 70f);
            _badge = badge.gameObject.AddComponent<CanvasGroup>();
            _badge.blocksRaycasts = false;
            _badge.interactable = false;

            // "Doors open in" over the badge, so it reads before the number.
            var caption = Title(badge, "Caption", Tr.Get("Doors open in"), 42f, _cream, 0.28f);
            caption.font = _style != null ? _style.ExtraBold : caption.font;
            caption.rectTransform.anchorMin = caption.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            caption.rectTransform.anchoredPosition = new Vector2(0f, -30f);

            var disc = UiKit.CircleSprite(BadgeSize);
            var centreY = -(70f + BadgeSize / 2f);
            var face = UiKit.Rect(badge, "Face", 0f, 0f, BadgeSize, BadgeSize);
            face.anchorMin = face.anchorMax = new Vector2(0.5f, 1f);
            face.pivot = new Vector2(0.5f, 0.5f);
            face.anchoredPosition = new Vector2(0f, centreY);
            Centred(face, "Shadow", disc, new Color(0f, 0f, 0f, 0.3f), BadgeSize, BadgeSize, 5f, -8f);
            Centred(face, "Ink", disc, _ink, BadgeSize, BadgeSize);
            Centred(face, "Wood", disc, _wood, BadgeSize - 16f, BadgeSize - 16f);
            var ring = RingSprite();
            Centred(face, "Track", ring, _woodMid, BadgeSize - 40f, BadgeSize - 40f);
            _ringFill = Centred(face, "Ring", ring, _orange, BadgeSize - 40f, BadgeSize - 40f);
            _ringFill.type = Image.Type.Filled;
            _ringFill.fillMethod = Image.FillMethod.Radial360;
            _ringFill.fillOrigin = (int)Image.Origin360.Top;
            _ringFill.fillClockwise = true;
            _number = Title(face, "Number", "4", 200f, _cream, 0.22f);
            _number.rectTransform.anchoredPosition = new Vector2(0f, 4f);
            badge.gameObject.SetActive(false);
        }

        private void BuildBand()
        {
            _band = UiKit.Rect(_cueCanvas, "ServiceBand", 0f, 0f, 4200f, BandHeight + 20f);
            _band.anchorMin = _band.anchorMax = _band.pivot = new Vector2(0.5f, 0.5f);
            // Over the back of the kitchen, as in the mockup: a third of the way down.
            _band.anchoredPosition = new Vector2(0f, Reference / 2f - 330f);
            _band.localRotation = Quaternion.Euler(0f, 0f, BandTilt);
            Centred(_band, "Shadow", null, _ink, 4200f, BandHeight, 0f, -14f);
            Centred(_band, "Orange", null, _orange, 4200f, BandHeight);
            Centred(_band, "Wood", null, _wood, 4200f, BandHeight - 32f);
            _bandTitle = Title(_band, "Title", Tr.Get("Service!"), 180f, _cream, 0.24f);
            _band.gameObject.SetActive(false);
        }

        private void BuildShutter()
        {
            _shutter = UiKit.Rect(_shutterCanvas, "Shutter", 0f, 0f, 0f, 0f);
            UiKit.Fill(_shutter);

            var slats = new GameObject("Slats", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            slats.transform.SetParent(_shutter, false);
            UiKit.Fill(slats.rectTransform);
            slats.raycastTarget = false;
            var shader = _shutterShader != null ? _shutterShader : Shader.Find("Game/UI/Shutter");
            _slats = new Material(shader) { name = "Shutter (instance)", hideFlags = HideFlags.DontSave };
            slats.material = _slats;

            // The bottom rail and its handle.
            var rail = UiKit.Rect(_shutter, "Rail", 0f, 0f, 0f, 30f);
            rail.anchorMin = new Vector2(0f, 0f);
            rail.anchorMax = new Vector2(1f, 0f);
            rail.pivot = new Vector2(0.5f, 0f);
            rail.offsetMin = new Vector2(0f, 0f);
            rail.offsetMax = new Vector2(0f, 30f);
            rail.gameObject.AddComponent<Image>().color = _ink;
            var handle = UiKit.RoundBox(_shutter, "Handle", _orange, 0f, 0f, 180f, 18f, 9f);
            handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = handle.rectTransform.pivot = new Vector2(0.5f, 0f);
            handle.rectTransform.anchoredPosition = new Vector2(0f, 6f);

            // The sign hangs from a nail above the middle of the screen, on two cords.
            _sign = UiKit.Rect(_shutter, "Sign", 0f, 0f, 0f, 0f);
            _sign.anchorMin = _sign.anchorMax = new Vector2(0.5f, 0.5f);
            _sign.pivot = new Vector2(0.5f, 0.5f);
            _sign.anchoredPosition = new Vector2(0f, 230f);
            _signGroup = _sign.gameObject.AddComponent<CanvasGroup>();
            _signGroup.blocksRaycasts = false;
            const float boardWidth = 800f, boardHeight = 210f, drop = 150f;
            foreach (var side in new[] { -1f, 1f })
            {
                var cord = Centred(_sign, "Cord", null, _ink, 6f, Mathf.Sqrt(250f * 250f + drop * drop));
                cord.rectTransform.pivot = new Vector2(0.5f, 1f);
                cord.rectTransform.anchoredPosition = Vector2.zero;
                cord.rectTransform.localRotation = Quaternion.Euler(0f, 0f, side * Mathf.Atan2(250f, drop) * Mathf.Rad2Deg);
            }
            Centred(_sign, "Nail", UiKit.CircleSprite(18f), _ink, 18f, 18f);
            var boardY = -drop - boardHeight / 2f;
            Centred(_sign, "Shadow", UiKit.RoundedSprite(28f), new Color(0f, 0f, 0f, 0.3f), boardWidth + 8f, boardHeight + 8f, 6f, boardY - 10f).type = Image.Type.Sliced;
            Centred(_sign, "Ink", UiKit.RoundedSprite(28f), _ink, boardWidth + 8f, boardHeight + 8f, 0f, boardY).type = Image.Type.Sliced;
            Centred(_sign, "Orange", UiKit.RoundedSprite(24f), _orange, boardWidth, boardHeight, 0f, boardY).type = Image.Type.Sliced;
            Centred(_sign, "Cream", UiKit.RoundedSprite(18f), _cream, boardWidth - 20f, boardHeight - 20f, 0f, boardY).type = Image.Type.Sliced;
            _signText = Title(_sign, "Text", Tr.Get("Closing time"), 116f, _bad, 0.2f);
            _signText.rectTransform.anchoredPosition = new Vector2(0f, boardY + 2f);
            _signText.rectTransform.sizeDelta = new Vector2(boardWidth - 40f, boardHeight);
            _signText.enableAutoSizing = true;
            _signText.fontSizeMax = 116f;
            _signText.fontSizeMin = 60f;

            _shutter.gameObject.SetActive(false);
        }

        // A ring drawn at twice its shown size, so the radial fill's edge stays smooth.
        private static Sprite _ring;

        private static Sprite RingSprite()
        {
            if (_ring != null)
                return _ring;
            const int size = 256;
            const float outer = size / 2f - 1f, inner = outer - 30f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude;
                var a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _ring = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            _ring.hideFlags = HideFlags.DontSave;
            return _ring;
        }

        // ---- Easing ----

        private static float EaseOutBack(float t, float overshoot)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

        // A dropped shutter: lands, one small bounce, settles.
        private static float EaseOutBounce(float t)
        {
            t = Mathf.Clamp01(t);
            const float land = 0.72f;
            if (t < land)
            {
                var u = t / land;
                return u * u;
            }
            var b = (t - land) / (1f - land);
            return 1f - 0.06f * Mathf.Sin(b * Mathf.PI);
        }

        private static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
