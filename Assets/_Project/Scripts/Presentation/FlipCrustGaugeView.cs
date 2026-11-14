using System.Collections.Generic;
using Game.Gameplay;
using Game.Simulation.Cooking;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// The flip gauge as a crust strip in the scene (decision 2026-11-11, the user's pick "flip C"),
    /// replacing the flat bar of <see cref="Hud.FlipGaugeHud"/>. Under each piece of food that needs
    /// flipping lies a chunky strip in the colours the crust goes through, from raw to golden, brown,
    /// dark and charred. A marker slides along it with the browning of the side facing down (it
    /// reads the food, so it creeps in a cool pan and races in a hot one). The golden window is
    /// framed in green with the X button under it, and glows while the marker is in it: that's
    /// when to press X. The colour the marker stops on is the crust that comes up.
    ///
    /// On the flip (the chef's arms toss the pan and the food, <see cref="ChefArmsView"/>) a mark
    /// pops over the window: ✓ golden, – pale (too early), ✕ scorched (too late). Then the strip
    /// becomes the doneness strip (raw, rare, medium, well done) with the marker following the
    /// middle of the food. Near burning, the dark end flashes red and the strip shakes. The strip
    /// faces the camera, so it reads at every rotation, and stays inside the screen.
    /// </summary>
    public sealed class FlipCrustGaugeView : MonoBehaviour
    {
        // Draw order: pieces ignore depth (Game/ToonOnTop), so later ones cover earlier ones.
        private const int OutlineOrder = 10;
        private const int StripOrder = 11;
        private const int FrameOrder = 12;
        private const int MarkerOrder = 14;
        private const int ButtonOrder = 16;

        [SerializeField] private KitchenCompositionRoot _root;
        [Tooltip("Game/ToonOnTop: every piece, tinted per renderer.")]
        [SerializeField] private Material _onTop;

        [Header("Size and place")]
        [Tooltip("Pixels across the strip on a 1080p screen, whatever the camera's distance.")]
        [SerializeField, Min(10f)] private float _widthPixels = 150f;
        [Tooltip("Metres from the food to the strip's centre, on screen, on the far side from the chef (up the screen with no chef near).")]
        [SerializeField] private float _away = 0.55f;
        [Tooltip("The strip's height and the X button's size, as shares of its width.")]
        [SerializeField, Min(0.01f)] private float _height = 0.18f;
        [SerializeField, Min(0.01f)] private float _buttonSize = 0.215f;

        [Header("As the camera rises (decision 2026-11-14; bands in TuningConfig)")]
        [Tooltip("Metres from the food within which a chef makes the strip theirs: full size at every height.")]
        [SerializeField, Min(0f)] private float _ownReach = 2.5f;
        [Tooltip("The chip a pan nobody's at becomes in the middle band, as a share of the strip's width: the colour the crust (or the doneness) has reached.")]
        [SerializeField, Range(0.05f, 1f)] private float _chipWidth = 0.3f;

        [Header("Crust colours, raw to charred")]
        [SerializeField] private Color _raw = new Color32(0xE8, 0xA9, 0x9A, 0xFF);
        [SerializeField] private Color _golden = new Color32(0xD9, 0xA0, 0x66, 0xFF);
        [SerializeField] private Color _brown = new Color32(0xB5, 0x70, 0x2E, 0xFF);
        [SerializeField] private Color _dark = new Color32(0x6B, 0x3A, 0x1C, 0xFF);
        [SerializeField] private Color _charred = new Color32(0x24, 0x1D, 0x19, 0xFF);
        [Tooltip("Where charred starts on the strip, between the warning and burnt (0 = at the warning, 1 = at burnt).")]
        [SerializeField, Range(0f, 1f)] private float _charredFrom = 0.5f;

        [Header("Doneness, after the flip: raw, then each level")]
        [SerializeField] private Color[] _stageColors =
        {
            new Color32(0x9B, 0x4A, 0x6E, 0xFF),
            new Color32(0xD9, 0x49, 0x3B, 0xFF),
            new Color32(0xF0, 0x95, 0x95, 0xFF),
            new Color32(0x9A, 0x82, 0x6E, 0xFF),
        };

        [Header("Feedback")]
        [SerializeField, Min(0f)] private float _popInSeconds = 0.18f;
        [Tooltip("Seconds the grade mark shows after a flip.")]
        [SerializeField, Min(0.1f)] private float _markSeconds = 0.9f;
        [Tooltip("Times a second the window glows while the marker is in it.")]
        [SerializeField, Min(0f)] private float _glowRate = 3f;
        [Tooltip("Times a second the dark end flashes when the food is about to burn.")]
        [SerializeField, Min(0f)] private float _warningRate = 4f;

        private readonly Dictionary<(Pan, int), Kit> _kits = new Dictionary<(Pan, int), Kit>();
        private readonly Game.Simulation.Display.HudZoom _zoom = new Game.Simulation.Display.HudZoom();

        private sealed class Kit
        {
            public Transform Root;
            public Renderer[] Outline;
            public Renderer[] Crust;
            public Renderer[] Stages;
            public Renderer[] Frame;
            public Renderer Needle;
            public Renderer NeedleOutline;
            public Renderer Tip;
            public Transform Button;
            public Transform Mark;

            public Ingredient Food;
            public bool WasFlipped;
            public float LastPosition;
            public PressGrade LastGrade;
            public float MarkLeft;
            public float Shown;

            // The middle band's chip, and how urgent (a chef at it, or burning) the strip is, eased.
            public Renderer Chip;
            public Renderer ChipOutline;
            public Color ChipColour;
            public float Urgency;
        }

        /// <summary>How many strips are showing as the middle band's chip, for tests and captures.</summary>
        public int ShowingAsChips
        {
            get
            {
                var count = 0;
                foreach (var kit in _kits.Values)
                    if (kit.Root != null && kit.Root.gameObject.activeSelf && kit.Chip.gameObject.activeSelf)
                        count++;
                return count;
            }
        }

        /// <summary>The grade the crust would get if flipped now, for the mark (and tests).</summary>
        public static PressGrade GradeAt(float browning, FlipGaugeSettings settings) =>
            browning < settings.GoodFrom ? PressGrade.Off : browning <= settings.GoodTo ? PressGrade.Good : PressGrade.Bad;

        /// <summary>How many strips are showing, for tests.</summary>
        public int Showing
        {
            get
            {
                var count = 0;
                foreach (var kit in _kits.Values)
                    if (kit.Root != null && kit.Root.gameObject.activeSelf)
                        count++;
                return count;
            }
        }

        /// <summary>The grade of the last flip shown, for tests.</summary>
        public PressGrade? LastFlipGrade { get; private set; }

        private void LateUpdate()
        {
            var services = _root != null ? _root.Services : null;
            var camera = Camera.main;
            foreach (var kit in _kits.Values)
                kit.Root.gameObject.SetActive(false);
            if (services == null || camera == null || services.Forkcast.IsOpen)
                return;

            var settings = services.Tuning.FlipGauge;
            var zoomSettings = services.Tuning.HudZoom;
            _zoom.Tick(camera.transform.position.y, Time.unscaledDeltaTime, zoomSettings);
            var dt = Time.deltaTime;
            foreach (var pan in services.Cooking.Pans)
            for (var piece = 0; piece < pan.Contents.Count; piece++)
            {
                var food = pan.Contents[piece];
                if (food == null || !services.ItemViews.TryGetView(food, out var view))
                    continue;
                var stages = FlipGauge.ShowsStages(food);
                if (!stages && !FlipGauge.Shows(food) && !(_kits.TryGetValue((pan, piece), out var fading) && fading.MarkLeft > 0f))
                    continue;

                var kit = KitFor(pan, piece);
                if (kit.Food != food)
                {
                    kit.Food = food;
                    kit.WasFlipped = food.HasBeenFlipped;
                    kit.Shown = 0f;
                    kit.MarkLeft = 0f;
                }

                // The flip: graded by where the marker was the moment before.
                if (food.HasBeenFlipped && !kit.WasFlipped)
                {
                    kit.MarkLeft = _markSeconds;
                    LastFlipGrade = kit.LastGrade;
                    TimingGaugeKit.SetMark(kit.Mark, kit.LastGrade);
                }
                kit.WasFlipped = food.HasBeenFlipped;
                kit.MarkLeft = Mathf.Max(0f, kit.MarkLeft - dt);

                kit.Root.gameObject.SetActive(true);
                kit.Shown = Mathf.MoveTowards(kit.Shown, 1f, _popInSeconds > 0f ? dt / _popInSeconds : 1f);
                var at = view.transform.position;
                var chef = TimingGaugeKit.NearestChef(at, Mathf.Max(2.5f, _ownReach));
                var warning = FlipGauge.IsAboutToBurn(food, settings);

                // A chef at the pan, or food about to burn, keeps the full strip at any height; a pan
                // nobody's at shrinks with the scene, turns into a chip, and fades out far up. Changing
                // form shrinks the strip away and pops the chip in (and back), never both at once.
                var own = chef.HasValue && new Vector2(chef.Value.x - at.x, chef.Value.z - at.z).sqrMagnitude <= _ownReach * _ownReach;
                kit.Urgency = Game.Simulation.Display.HudZoom.Ease(kit.Urgency, own || warning ? 1f : 0f, Time.unscaledDeltaTime, zoomSettings.ChangeSeconds);
                var look = _zoom.For(kit.Urgency);
                var chip = look.Simple >= 0.5f;
                var form = Mathf.Abs(1f - 2f * look.Simple);
                var where = at + TimingGaugeKit.AwayFromChef(at, chef, camera, _away);
                var size = TimingGaugeKit.MetresFor(camera, where, _widthPixels) * look.Scale * look.Alpha * form;
                TimingGaugeKit.FaceCamera(kit.Root, camera, where, Mathf.Max(0.001f, size * TimingGaugeKit.EaseOutBack(kit.Shown)));
                var flash = warning ? (Mathf.Sin(Time.time * _warningRate * Mathf.PI * 2f) + 1f) * 0.5f : 0f;
                if (warning)
                    kit.Root.position += kit.Root.right * (Mathf.Sin(Time.time * 47f) * 0.006f);

                foreach (var part in new[] { kit.Outline[0], kit.Needle, kit.NeedleOutline, kit.Tip })
                    part.gameObject.SetActive(true);
                if (stages)
                    DrawStages(kit, food);
                else
                    DrawCrust(kit, food, settings, flash);

                // The mark pops over the window, then fades away.
                var markT = kit.MarkLeft / _markSeconds;
                kit.Mark.gameObject.SetActive(kit.MarkLeft > 0f);
                if (kit.MarkLeft > 0f)
                {
                    var pop = 1f + 0.4f * Mathf.Pow(markT, 3f);
                    kit.Mark.localScale = Vector3.one * (_buttonSize * 1.25f * pop);
                }
                ShowChip(kit, chip);
            }
        }

        // The middle band's chip: everything else off, one block in the colour the marker is on.
        private void ShowChip(Kit kit, bool chip)
        {
            kit.Chip.gameObject.SetActive(chip);
            kit.ChipOutline.gameObject.SetActive(chip);
            if (!chip)
                return;
            foreach (Transform child in kit.Root)
            {
                if (child != kit.Chip.transform && child != kit.ChipOutline.transform)
                    child.gameObject.SetActive(false);
            }
            TimingGaugeKit.Paint(kit.Chip, kit.ChipColour);
        }

        private void DrawCrust(Kit kit, Ingredient food, FlipGaugeSettings settings, float flash)
        {
            var properties = food.Properties;
            var good0 = FlipGauge.ToPosition(settings.GoodFrom, properties);
            var good1 = FlipGauge.ToPosition(settings.GoodTo, properties);
            var warn = FlipGauge.ToPosition(settings.WarnFrom, properties);
            var charred = Mathf.Lerp(warn, 1f, _charredFrom);
            float[] edges = { 0f, good0, good1, warn, charred, 1f };
            Color[] colours = { _raw, _golden, _brown, _dark, _charred };
            for (var i = 0; i < kit.Crust.Length; i++)
            {
                kit.Crust[i].gameObject.SetActive(true);
                Span(kit.Crust[i], edges[i], edges[i + 1], 0f);
                var colour = colours[i];
                if (i >= 3)
                    colour = Color.Lerp(colour, TimingGaugeKit.Bad, flash * 0.8f);
                TimingGaugeKit.Paint(kit.Crust[i], colour);
            }
            var at = FlipGauge.Position(food);
            for (var i = 0; i < colours.Length; i++)
            {
                if (at <= edges[i + 1] || i == colours.Length - 1)
                {
                    kit.ChipColour = i >= 3 ? Color.Lerp(colours[i], TimingGaugeKit.Bad, flash * 0.8f) : colours[i];
                    break;
                }
            }
            foreach (var stage in kit.Stages)
                stage.gameObject.SetActive(false);

            var position = FlipGauge.Position(food);
            var browning = food.Browning(food.DownSide);
            kit.LastPosition = position;
            kit.LastGrade = GradeAt(browning, settings);
            var inWindow = kit.LastGrade == PressGrade.Good;

            // The golden window: a green frame, glowing while the marker is in it, and the X under it.
            var glow = inWindow ? (Mathf.Sin(Time.time * _glowRate * Mathf.PI * 2f) + 1f) * 0.5f : 0f;
            FrameAround(kit, good0, good1, inWindow ? 0.03f + 0.012f * glow : 0.022f);
            foreach (var bar in kit.Frame)
            {
                bar.gameObject.SetActive(true);
                TimingGaugeKit.Paint(bar, Color.Lerp(TimingGaugeKit.Good, Color.white, glow * 0.35f));
            }
            kit.Button.gameObject.SetActive(kit.MarkLeft <= 0f);
            kit.Button.localPosition = new Vector3(X((good0 + good1) * 0.5f), -_height * 0.5f - _buttonSize * 0.62f, -0.02f);
            kit.Button.localScale = Vector3.one * (_buttonSize * (inWindow ? 1.15f + 0.1f * glow : 1f));
            kit.Mark.localPosition = new Vector3(X((good0 + good1) * 0.5f), _height * 0.5f + _buttonSize * 0.75f, -0.03f);

            Needle(kit, position);
        }

        private void DrawStages(Kit kit, Ingredient food)
        {
            var count = FlipGauge.StageCount(food.Properties);
            for (var i = 0; i < kit.Stages.Length; i++)
            {
                var show = i < count;
                kit.Stages[i].gameObject.SetActive(show);
                if (show)
                    Span(kit.Stages[i], i / (float)count, (i + 1) / (float)count, 0f);
            }
            var reached = Mathf.Clamp(Mathf.FloorToInt(FlipGauge.StagePosition(food) * count), 0, Mathf.Max(0, count - 1));
            kit.ChipColour = _stageColors[Mathf.Min(reached, _stageColors.Length - 1)];
            foreach (var crust in kit.Crust)
                crust.gameObject.SetActive(false);
            foreach (var bar in kit.Frame)
                bar.gameObject.SetActive(false);
            kit.Button.gameObject.SetActive(false);
            Needle(kit, FlipGauge.StagePosition(food));
        }

        // The marker: a white needle through the strip, outlined, with a diamond on top.
        private void Needle(Kit kit, float position)
        {
            var x = X(position);
            var tall = _height * 1.7f;
            TimingGaugeKit.Place(kit.NeedleOutline, new Vector2(x, 0f), new Vector2(0.032f, tall + 0.02f), 0.01f, -0.04f);
            TimingGaugeKit.Place(kit.Needle, new Vector2(x, 0f), new Vector2(0.016f, tall), 0.01f, -0.05f);
            kit.Tip.transform.localPosition = new Vector3(x, tall * 0.5f + 0.012f, -0.05f);
            kit.Tip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            kit.Tip.transform.localScale = new Vector3(0.045f, 0.045f, 0.01f);
        }

        private void FrameAround(Kit kit, float from, float to, float thickness)
        {
            var left = X(from);
            var right = X(to);
            var half = _height * 0.5f + thickness * 0.5f;
            var width = right - left + thickness * 2f;
            var middle = (left + right) * 0.5f;
            TimingGaugeKit.Place(kit.Frame[0], new Vector2(middle, half), new Vector2(width, thickness), 0.01f, -0.03f);
            TimingGaugeKit.Place(kit.Frame[1], new Vector2(middle, -half), new Vector2(width, thickness), 0.01f, -0.03f);
            TimingGaugeKit.Place(kit.Frame[2], new Vector2(left - thickness * 0.5f, 0f), new Vector2(thickness, half * 2f), 0.01f, -0.03f);
            TimingGaugeKit.Place(kit.Frame[3], new Vector2(right + thickness * 0.5f, 0f), new Vector2(thickness, half * 2f), 0.01f, -0.03f);
        }

        // A segment of the strip from one position (0..1) to another.
        private void Span(Renderer segment, float from, float to, float z)
        {
            var left = X(from);
            var right = X(Mathf.Max(from, to));
            TimingGaugeKit.Place(segment, new Vector2((left + right) * 0.5f, 0f), new Vector2(right - left, _height), 0.01f, z - 0.01f);
        }

        // Strip units: one across, centred.
        private static float X(float position) => Mathf.Clamp01(position) - 0.5f;

        private Kit KitFor(Pan pan, int piece)
        {
            if (_kits.TryGetValue((pan, piece), out var kit))
                return kit;
            var root = new GameObject($"FlipCrustGauge_{_kits.Count}") { hideFlags = HideFlags.DontSave }.transform;
            root.SetParent(transform, false);
            kit = new Kit { Root = root };

            // An Ink outline round the strip, as the art bible's outlines.
            var outline = TimingGaugeKit.Block(root, "Outline", _onTop, TimingGaugeKit.Ink, OutlineOrder);
            TimingGaugeKit.Place(outline, Vector2.zero, new Vector2(1f + 0.03f, _height + 0.03f), 0.01f, 0f);
            kit.Outline = new[] { outline };

            kit.Crust = new Renderer[5];
            for (var i = 0; i < kit.Crust.Length; i++)
                kit.Crust[i] = TimingGaugeKit.Block(root, $"Crust_{i}", _onTop, Color.white, StripOrder);
            kit.Stages = new Renderer[_stageColors.Length + 1];
            for (var i = 0; i < kit.Stages.Length; i++)
                kit.Stages[i] = TimingGaugeKit.Block(root, $"Stage_{i}", _onTop, _stageColors[Mathf.Min(i, _stageColors.Length - 1)], StripOrder);
            kit.Frame = new Renderer[4];
            for (var i = 0; i < kit.Frame.Length; i++)
                kit.Frame[i] = TimingGaugeKit.Block(root, "Window", _onTop, TimingGaugeKit.Good, FrameOrder);
            kit.NeedleOutline = TimingGaugeKit.Block(root, "NeedleOutline", _onTop, TimingGaugeKit.Ink, MarkerOrder);
            kit.Needle = TimingGaugeKit.Block(root, "Needle", _onTop, Color.white, MarkerOrder + 1);
            kit.Tip = TimingGaugeKit.Block(root, "Tip", _onTop, Color.white, MarkerOrder + 1);
            kit.Button = TimingGaugeKit.XButton(root, _onTop, ButtonOrder);
            kit.Mark = TimingGaugeKit.Mark(root, _onTop, ButtonOrder + 4);
            kit.Mark.gameObject.SetActive(false);
            var chipHeight = _height * 1.2f;
            kit.ChipOutline = TimingGaugeKit.Block(root, "ChipOutline", _onTop, TimingGaugeKit.Ink, OutlineOrder);
            TimingGaugeKit.Place(kit.ChipOutline, Vector2.zero, new Vector2(_chipWidth + 0.03f, chipHeight + 0.03f), 0.01f, 0f);
            kit.Chip = TimingGaugeKit.Block(root, "Chip", _onTop, Color.white, StripOrder);
            TimingGaugeKit.Place(kit.Chip, Vector2.zero, new Vector2(_chipWidth, chipHeight), 0.01f, -0.01f);
            kit.Chip.gameObject.SetActive(false);
            kit.ChipOutline.gameObject.SetActive(false);
            _kits[(pan, piece)] = kit;
            return kit;
        }
    }
}
