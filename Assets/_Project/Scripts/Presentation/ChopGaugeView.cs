using System.Collections.Generic;
using Game.Gameplay.Stations;
using Game.Simulation.Items;
using Game.Simulation.Prep;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation
{
    /// <summary>
    /// The cutting dial as a chunky gauge in the scene (decision 2026-11-11, the user's pick "cut A"),
    /// replacing the flat fan of <see cref="Hud.ChopDialHud"/>. Over each board a chef is cutting at
    /// stands a half dial in the kitchen's toon style: a wood-dark fan with an Ink outline, the
    /// ingredient's sweet spot on its rim (green core, amber edges) with the X button just outside
    /// it, and a knife for a needle, swinging far right to far left and back. Pressing X as it
    /// crosses the green cuts well.
    ///
    /// Each cut is shown on the dial itself: a tick where the blade was, the fan flashing the grade's
    /// colour (a pop for a good cut, a shake for a miss) and a mark over the X: ✓ good, – early or
    /// late, ✕ miss. A row of pips inside the fan, one per cut the ingredient needs, fills in each
    /// cut's colour. The streak speeding the knife up is the only speed cue: no text. After a good
    /// cut the sweet spot slides one step along (its ingredient's step, scaled by the complexity
    /// dial), as if working down the ingredient. The dial faces
    /// the camera, so it reads at every rotation, and stays inside the screen.
    /// </summary>
    public sealed class ChopGaugeView : MonoBehaviour
    {
        private const int OutlineOrder = 10;
        private const int FanOrder = 11;
        private const int ArcOrder = 12;
        private const int TickOrder = 13;
        private const int KnifeOrder = 14;
        private const int ButtonOrder = 17;

        // Dial units: the rim's outside at 0.5.
        private const float Inner = 0.17f;
        private const float Outer = 0.5f;
        private const float Thick = 0.03f;

        [SerializeField] private Material _onTop;

        [Header("Size and place")]
        [Tooltip("Pixels across the dial on a 1080p screen, whatever the camera's distance.")]
        [SerializeField, Min(10f)] private float _sizePixels = 140f;
        [Tooltip("Metres above the station's base for the dial's hub (the counter top).")]
        [SerializeField, Min(0f)] private float _anchorHeight = 0.95f;
        [Tooltip("Metres from the board to the hub, on screen, on the far side from the chef (up the screen with no chef near).")]
        [SerializeField] private float _away = 0.25f;

        [Header("Colours")]
        [SerializeField] private Color _blade = new Color(0.86f, 0.87f, 0.88f);
        [SerializeField] private Color _handle = new Color32(0x8B, 0x5A, 0x33, 0xFF);
        [SerializeField] private Color _pipEmpty = new Color32(0x8B, 0x5A, 0x33, 0xFF);

        [Header("Feedback")]
        [SerializeField, Min(0f)] private float _popInSeconds = 0.18f;
        [SerializeField, Min(0.05f)] private float _flashSeconds = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _flashStrength = 0.6f;
        [SerializeField, Min(0.05f)] private float _markSeconds = 0.7f;
        [Tooltip("Seconds for the sweet spot to slide to its next place after a good cut.")]
        [SerializeField, Min(0.01f)] private float _spotSlideSeconds = 0.12f;
        [Tooltip("Extra size at the start of the pop after a good cut.")]
        [SerializeField, Min(0f)] private float _goodPop = 0.12f;
        [Tooltip("Metres of side-to-side shake after a miss.")]
        [SerializeField, Min(0f)] private float _missShake = 0.025f;

        private readonly Dictionary<PrepStation, Kit> _kits = new Dictionary<PrepStation, Kit>();
        private readonly Dictionary<PrepStation, float> _lastChop = new Dictionary<PrepStation, float>();
        private readonly List<PrepStation> _faced = new List<PrepStation>();
        private readonly List<PrepStation> _gone = new List<PrepStation>();

        private sealed class Kit
        {
            public Transform Root;
            public Transform Face;
            public Transform SpotFace;
            public float ShownSpot = float.NaN;
            public float SpotVelocity;
            public Renderer Outline;
            public Renderer Fan;
            public Renderer Ok;
            public Renderer GoodArc;
            public Transform Knife;
            public Transform Tick;
            public Renderer TickRenderer;
            public Transform Button;
            public Transform Mark;
            public Renderer[] Pips;
            public (float Sweep, float Good, float Ok) Built;
            public ChopDial Dial;
            public float Shown;
        }

        /// <summary>The boards showing a dial, for tests.</summary>
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

        public static PressGrade ToPress(ChopGrade grade) =>
            grade == ChopGrade.Good ? PressGrade.Good : grade == ChopGrade.Miss ? PressGrade.Bad : PressGrade.Off;

        private void LateUpdate()
        {
            var camera = Camera.main;
            _faced.Clear();
            foreach (var player in PlayerInput.all)
            {
                // Not while managing (the target is then the layout cursor's) or reviewing.
                if (player.TryGetComponent(out PlayerInteractor interactor) && !interactor.IsManaging && !interactor.IsReviewing
                    && interactor.Target is PrepStation board && interactor.Tool is Knife && !_faced.Contains(board))
                    _faced.Add(board);
            }

            _gone.Clear();
            foreach (var pair in _kits)
            {
                if (pair.Key == null)
                    _gone.Add(pair.Key);
                else if (!_faced.Contains(pair.Key))
                    pair.Value.Root.gameObject.SetActive(false);
            }
            foreach (var station in _gone)
            {
                Destroy(_kits[station].Root.gameObject);
                _kits.Remove(station);
                _lastChop.Remove(station);
            }
            if (camera == null)
                return;

            foreach (var station in _faced)
            {
                var kit = KitFor(station);
                var dial = station.Counter?.Dial;
                if (dial == null)
                {
                    kit.Root.gameObject.SetActive(false);
                    kit.Shown = 0f;
                    continue;
                }
                var since = _lastChop.TryGetValue(station, out var at) ? Time.unscaledTime - at : float.MaxValue;
                Draw(kit, station, dial, station.Counter.Cuts, station.Counter.CutsNeeded, Time.unscaledTimeAsDouble, since, camera);
            }
        }

        private void Draw(Kit kit, PrepStation station, ChopDial dial, int cuts, int cutsNeeded, double now, float since, Camera camera)
        {
            kit.Root.gameObject.SetActive(true);
            if (kit.Dial != dial)
            {
                kit.Dial = dial;
                kit.ShownSpot = dial.SweetSpot;
                kit.SpotVelocity = 0f;
                kit.Shown = 0f;
            }
            Build(kit, dial);
            var dt = Time.unscaledDeltaTime;
            kit.Shown = Mathf.MoveTowards(kit.Shown, 1f, _popInSeconds > 0f ? dt / _popInSeconds : 1f);

            var grade = dial.LastGrade;
            var fresh = grade.HasValue && cuts > 0;
            var press = grade.HasValue ? ToPress(grade.Value) : PressGrade.Good;
            var colour = TimingGaugeKit.GradeColour(press);
            var flashT = fresh ? Mathf.Clamp01(since / _flashSeconds) : 1f;

            // Placed over the board, facing the camera; a good cut pops it, a miss shakes it.
            var scale = TimingGaugeKit.EaseOutBack(kit.Shown);
            if (fresh && flashT < 1f && press == PressGrade.Good)
                scale *= 1f + _goodPop * (1f - flashT);
            var top = station.transform.position + Vector3.up * _anchorHeight;
            var hub = top + TimingGaugeKit.AwayFromChef(top, TimingGaugeKit.NearestChef(top, 2.5f), camera, _away);
            TimingGaugeKit.FaceCamera(kit.Root, camera, hub, TimingGaugeKit.MetresFor(camera, hub, _sizePixels) * Mathf.Max(0.001f, scale));
            if (fresh && flashT < 1f && press == PressGrade.Bad)
                kit.Root.position += kit.Root.right * (Mathf.Sin(since * 55f) * _missShake * (1f - flashT));

            // The fan flashes the grade's colour.
            TimingGaugeKit.Paint(kit.Fan, Color.Lerp(TimingGaugeKit.WoodDark, colour, flashT < 1f ? _flashStrength * (1f - flashT) : 0f));

            // The knife: angle clockwise from the top is (half the sweep − its position).
            var half = dial.SweepDegrees * 0.5f;
            kit.Knife.localRotation = Quaternion.Euler(0f, 0f, -(half - dial.NeedlePosition(now)));

            // Where the last cut landed.
            var tickT = fresh ? Mathf.Clamp01(since / _markSeconds) : 1f;
            kit.Tick.gameObject.SetActive(tickT < 1f && dial.LastHitPosition.HasValue);
            if (kit.Tick.gameObject.activeSelf)
            {
                kit.Tick.localRotation = Quaternion.Euler(0f, 0f, -(half - dial.LastHitPosition.Value));
                TimingGaugeKit.Paint(kit.TickRenderer, Color.Lerp(colour, TimingGaugeKit.WoodDark, tickT * tickT));
            }

            // The sweet spot slides to where it stepped after a good cut (decision 2026-11-11), so the
            // move reads; its arcs turn on a pivot rather than being rebuilt.
            if (float.IsNaN(kit.ShownSpot))
                kit.ShownSpot = dial.SweetSpot;
            kit.ShownSpot = Mathf.SmoothDamp(kit.ShownSpot, dial.SweetSpot, ref kit.SpotVelocity, _spotSlideSeconds, Mathf.Infinity, dt);
            kit.SpotFace.parent.localRotation = Quaternion.Euler(0f, 0f, -(half - kit.ShownSpot));

            // The X just outside the sweet spot; the grade mark pops in its place after a cut.
            var spot = (half - kit.ShownSpot) * Mathf.Deg2Rad;
            var buttonAt = new Vector3(Mathf.Sin(spot), Mathf.Cos(spot), 0f) * (Outer + 0.1f) + new Vector3(0f, 0f, -0.03f);
            var markT = fresh ? since / _markSeconds : 1f;
            kit.Mark.gameObject.SetActive(markT < 1f);
            kit.Button.gameObject.SetActive(markT >= 1f);
            kit.Button.localPosition = buttonAt;
            kit.Button.localScale = Vector3.one * 0.23f;
            if (markT < 1f)
            {
                TimingGaugeKit.SetMark(kit.Mark, press);
                kit.Mark.localPosition = buttonAt;
                kit.Mark.localScale = Vector3.one * (0.25f * (1f + 0.4f * Mathf.Pow(1f - markT, 3f)));
            }

            // A pip per cut needed, in an arc inside the fan (clear of the chef by the hub), each
            // filled in its cut's colour, left to right.
            for (var i = 0; i < kit.Pips.Length; i++)
            {
                var show = i < cutsNeeded;
                kit.Pips[i].gameObject.SetActive(show);
                if (!show)
                    continue;
                var along = cutsNeeded > 1 ? i / (float)(cutsNeeded - 1) : 0.5f;
                var angle = Mathf.Lerp(-half * 0.7f, half * 0.7f, along) * Mathf.Deg2Rad;
                kit.Pips[i].transform.localPosition = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) * 0.255f + new Vector3(0f, 0f, -0.02f);
                kit.Pips[i].transform.localScale = new Vector3(0.075f, 0.01f, 0.075f);
                TimingGaugeKit.Paint(kit.Pips[i], i < dial.Grades.Count ? TimingGaugeKit.GradeColour(ToPress(dial.Grades[i])) : _pipEmpty);
            }
        }

        // The fan and the sweet spot's arcs, rebuilt when the ingredient changes them.
        private void Build(Kit kit, ChopDial dial)
        {
            (float SweepDegrees, float Good, float Ok) key = (dial.SweepDegrees, dial.GoodHalfWidth, dial.OkHalfWidth);
            if (kit.Fan != null && kit.Built == key)
                return;
            kit.Built = key;
            Replace(ref kit.Outline, kit, "Outline", Sector(Inner - 0.03f, Outer + 0.03f, -key.SweepDegrees * 0.5f - 3f, key.SweepDegrees * 0.5f + 3f, Thick * 0.5f), TimingGaugeKit.Ink, OutlineOrder);
            Replace(ref kit.Fan, kit, "Fan", Sector(Inner, Outer, -key.SweepDegrees * 0.5f, key.SweepDegrees * 0.5f, Thick), TimingGaugeKit.WoodDark, FanOrder);
            // The sweet spot's arcs, centred on the top of a pivot that turns to the spot.
            Replace(ref kit.Ok, kit, "Ok", Sector(Outer - 0.15f, Outer, -key.Ok, key.Ok, Thick * 1.3f), TimingGaugeKit.Warn, ArcOrder, kit.SpotFace);
            Replace(ref kit.GoodArc, kit, "Good", Sector(Outer - 0.17f, Outer + 0.02f, -key.Good, key.Good, Thick * 1.6f), TimingGaugeKit.Good, ArcOrder + 1, kit.SpotFace);
        }

        private void Replace(ref Renderer renderer, Kit kit, string name, Mesh mesh, Color colour, int order, Transform parent = null)
        {
            if (renderer != null)
            {
                Destroy(renderer.GetComponent<MeshFilter>().sharedMesh);
                Destroy(renderer.gameObject);
            }
            renderer = TimingGaugeKit.Piece(parent != null ? parent : kit.Face, name, mesh, _onTop, colour, order);
        }

        // A ring sector in degrees clockwise from the top, lying in the face's plane.
        private static Mesh Sector(float inner, float outer, float fromDegrees, float toDegrees, float height) =>
            StationKitMeshes.RingSector(inner, outer, fromDegrees * Mathf.Deg2Rad, toDegrees * Mathf.Deg2Rad, height, height * 0.4f, 16);

        private Kit KitFor(PrepStation station)
        {
            if (_kits.TryGetValue(station, out var kit))
                return kit;
            station.Chopped += _ => _lastChop[station] = Time.unscaledTime;

            var root = new GameObject($"ChopGauge_{station.name}") { hideFlags = HideFlags.DontSave }.transform;
            root.SetParent(transform, false);
            kit = new Kit { Root = root };
            // The ring meshes lie flat (y up); turned so the dial faces the camera, its top up on screen.
            kit.Face = new GameObject("Face") { hideFlags = HideFlags.DontSave }.transform;
            kit.Face.SetParent(root, false);
            kit.Face.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var spotPivot = new GameObject("SweetSpot") { hideFlags = HideFlags.DontSave }.transform;
            spotPivot.SetParent(root, false);
            kit.SpotFace = new GameObject("Face") { hideFlags = HideFlags.DontSave }.transform;
            kit.SpotFace.SetParent(spotPivot, false);
            kit.SpotFace.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            // The knife: a steel blade from the hub to the rim and a wooden handle at the hub, outlined.
            kit.Knife = new GameObject("Knife") { hideFlags = HideFlags.DontSave }.transform;
            kit.Knife.SetParent(root, false);
            var bladeOutline = TimingGaugeKit.Block(kit.Knife, "BladeOutline", _onTop, TimingGaugeKit.Ink, KnifeOrder);
            TimingGaugeKit.Place(bladeOutline, new Vector2(0f, 0.3f), new Vector2(0.075f, 0.42f), 0.01f, -0.04f);
            var blade = TimingGaugeKit.Block(kit.Knife, "Blade", _onTop, _blade, KnifeOrder + 1);
            TimingGaugeKit.Place(blade, new Vector2(0.004f, 0.3f), new Vector2(0.05f, 0.4f), 0.01f, -0.05f);
            var handleOutline = TimingGaugeKit.Block(kit.Knife, "HandleOutline", _onTop, TimingGaugeKit.Ink, KnifeOrder);
            TimingGaugeKit.Place(handleOutline, new Vector2(0f, 0.04f), new Vector2(0.085f, 0.18f), 0.01f, -0.04f);
            var handle = TimingGaugeKit.Block(kit.Knife, "Handle", _onTop, _handle, KnifeOrder + 1);
            TimingGaugeKit.Place(handle, new Vector2(0f, 0.04f), new Vector2(0.06f, 0.16f), 0.01f, -0.05f);
            var hub = TimingGaugeKit.Disc(root, "Hub", _onTop, TimingGaugeKit.Ink, KnifeOrder + 2);
            hub.transform.localScale = new Vector3(0.07f, 0.01f, 0.07f);
            hub.transform.localPosition = new Vector3(0f, 0f, -0.06f);

            kit.Tick = new GameObject("LastCut") { hideFlags = HideFlags.DontSave }.transform;
            kit.Tick.SetParent(root, false);
            kit.TickRenderer = TimingGaugeKit.Block(kit.Tick, "Tick", _onTop, Color.white, TickOrder);
            TimingGaugeKit.Place(kit.TickRenderer, new Vector2(0f, (Inner + Outer) * 0.5f + 0.06f), new Vector2(0.025f, Outer - Inner + 0.06f), 0.01f, -0.03f);

            kit.Button = TimingGaugeKit.XButton(root, _onTop, ButtonOrder);
            kit.Mark = TimingGaugeKit.Mark(root, _onTop, ButtonOrder + 4);
            kit.Pips = new Renderer[8];
            for (var i = 0; i < kit.Pips.Length; i++)
                kit.Pips[i] = TimingGaugeKit.Disc(root, "Pip", _onTop, _pipEmpty, ArcOrder);
            _kits[station] = kit;
            return kit;
        }
    }
}
