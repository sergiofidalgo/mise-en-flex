using System.Collections.Generic;
using Game.Gameplay;
using Game.Gameplay.Cleaning;
using Game.Gameplay.Stations;
using Game.Simulation.Cleaning;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Game.Presentation
{
    /// <summary>
    /// The X hint for cleaning, on the mess itself (decision 2026-11-11, after the user saw a stain
    /// and didn't know what to do: the hint only ever showed on appliances). When a chef with empty
    /// hands could start cleaning (dirt on what they face or on the floor at their feet, a sink with
    /// a dirty plate, a fire), the ring that holding X will fill appears on it, empty and gently
    /// pulsing, with the small X button at its top: where the stick's circle starts. Dirt also gets
    /// a flat stain inside its ring, drawn over what's in front, so a spill hidden behind a counter
    /// still reads as dirt. It's the same
    /// ring of 15 chunky segments as <see cref="CleaningView"/> and <see cref="SinkWashRingView"/>,
    /// so the hint is a preview of the game. It fades once the player has cleaned a few times, as
    /// the appliance hints do.
    /// </summary>
    public sealed class CleanHintView : MonoBehaviour
    {
        private const int Segments = 15;
        private const float SegmentGap = 0.3f;
        private const int RingOrder = 5;
        private const int ButtonOrder = 17;

        [SerializeField] private KitchenCompositionRoot _root;
        [Tooltip("Game/ToonOnTop: every piece, tinted per renderer.")]
        [SerializeField] private Material _onTop;
        [Tooltip("For fading once learnt; optional.")]
        [SerializeField] private ApplianceHints _hints;

        [Header("Look")]
        [Tooltip("Pixels across the X button on a 1080p screen (the gauges' size).")]
        [SerializeField, Min(8f)] private float _buttonPixels = 32f;
        [Tooltip("Ring radius (m) for dirt on a table or counter, on the floor, a fire and the sink.")]
        [SerializeField] private Vector4 _radius = new Vector4(0.3f, 0.45f, 0.32f, 0.36f);
        [Tooltip("Metres above the mess the ring floats.")]
        [SerializeField, Min(0f)] private float _lift = 0.03f;
        [Tooltip("The ring round dirt: the cleaning ring's grime, so it reads on cream counters and the grey floor alike.")]
        [SerializeField] private Color _dirtRing = new Color(0.36f, 0.23f, 0.12f);
        [Tooltip("The stain drawn inside a dirt ring, over whatever's in front: it shows dirt hidden behind a counter.")]
        [SerializeField] private Color _stain = new Color(0.62f, 0.38f, 0.18f);
        [SerializeField] private Color _water = new Color(0.33f, 0.66f, 0.92f);
        [SerializeField] private Color _flame = new Color(0.95f, 0.42f, 0.12f);
        [SerializeField, Min(0.01f)] private float _fadeSeconds = 0.2f;
        [SerializeField, Range(0f, 0.2f)] private float _pulse = 0.05f;

        private readonly Dictionary<PlayerInteractor, Kit> _kits = new Dictionary<PlayerInteractor, Kit>();
        private readonly List<PlayerInteractor> _gone = new List<PlayerInteractor>();
        // Each spot's stain outline, built once: the same random shape as its patch on the floor.
        private readonly Dictionary<int, Mesh> _stains = new Dictionary<int, Mesh>();
        private CleaningView _patches;

        private sealed class Kit
        {
            public Transform Ring;
            public Renderer[] Segments;
            public float BuiltRadius;
            public Transform Button;
            public Renderer Stain;
            public DirtSpot Spot;
            public float Shown;
            public Vector3 Centre;
            public float Radius;
        }

        /// <summary>How many chefs' clean hints are showing, for tests.</summary>
        public int Showing
        {
            get
            {
                var count = 0;
                foreach (var kit in _kits.Values)
                    if (kit.Shown > 0f)
                        count++;
                return count;
            }
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            var services = _root != null ? _root.Services : null;
            foreach (var player in PlayerInput.all)
            {
                if (!player.TryGetComponent(out PlayerInteractor chef))
                    continue;
                if (!_kits.TryGetValue(chef, out var kit))
                    _kits[chef] = kit = Build();
                var colour = _dirtRing;
                var wanted = services != null && camera != null && Wanted(chef, out kit.Centre, out kit.Radius, out colour, out kit.Spot);
                kit.Shown = Mathf.MoveTowards(kit.Shown, wanted ? 1f : 0f, Time.unscaledDeltaTime / _fadeSeconds);
                var visible = kit.Shown > 0f;
                kit.Ring.gameObject.SetActive(visible);
                kit.Button.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                Draw(kit, chef, camera);
                if (wanted)
                {
                    foreach (var segment in kit.Segments)
                        TimingGaugeKit.Paint(segment, colour);
                    // Dirt shows its own stain inside the ring; the sink and a fire show themselves.
                    kit.Stain.gameObject.SetActive(ShowStain(kit));
                }
            }

            _gone.Clear();
            foreach (var pair in _kits)
                if (pair.Key == null)
                    _gone.Add(pair.Key);
            foreach (var chef in _gone)
            {
                Destroy(_kits[chef].Ring.gameObject);
                Destroy(_kits[chef].Button.gameObject);
                _kits.Remove(chef);
            }
        }

        // Whether this chef could start cleaning now, and where.
        private bool Wanted(PlayerInteractor chef, out Vector3 centre, out float radius, out Color colour, out DirtSpot dirt)
        {
            dirt = null;
            centre = default;
            radius = 0f;
            colour = _dirtRing;
            if (!chef.CanAct || chef.IsBusy || chef.IsReviewing || chef.IsManaging || !chef.Hands.IsEmpty
                || chef.Cleaning == null || chef.Cleaning.IsCleaning)
                return false;
            if (_hints != null && _hints.UsesOf(chef.PlayerIndex, ApplianceHint.Clean) >= _hints.UsesBeforeFading)
                return false;

            if (chef.Target is SinkStation sink)
            {
                if (sink.Sink == null || sink.Sink.DirtyCount == 0 || sink.Washer != null)
                    return false;
                centre = sink.ItemAnchor != null ? sink.ItemAnchor.position : sink.transform.position + Vector3.up * 0.9f;
                radius = _radius.w;
                colour = _water;
                return true;
            }

            var tool = chef.Cleaning.WouldClean(chef.Target, out var spot, out var fire);
            switch (tool)
            {
                case CleaningTool.Extinguisher:
                    centre = fire.ItemAnchor != null ? fire.ItemAnchor.position : fire.transform.position;
                    radius = _radius.z;
                    colour = _flame;
                    return true;
                case CleaningTool.Cloth:
                    dirt = spot;
                    centre = KitchenMess.WorldOf(spot);
                    radius = _radius.x;
                    return true;
                case CleaningTool.Mop:
                    dirt = spot;
                    centre = KitchenMess.WorldOf(spot);
                    radius = _radius.y;
                    return true;
                default:
                    return false;
            }
        }

        private void Draw(Kit kit, PlayerInteractor chef, Camera camera)
        {
            if (!Mathf.Approximately(kit.BuiltRadius, kit.Radius))
                BuildRing(kit, kit.Radius);
            var eased = Mathf.SmoothStep(0f, 1f, kit.Shown);

            // The ring lies flat on the mess, turned to the camera like the cleaning ring, pulsing.
            var forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
                forward = Vector3.forward;
            var pulse = 1f + _pulse * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * 1.2f);
            kit.Ring.SetPositionAndRotation(kit.Centre + Vector3.up * _lift, Quaternion.LookRotation(forward, Vector3.up));
            kit.Ring.localScale = Vector3.one * (pulse * Mathf.Lerp(0.7f, 1f, eased));


            // The X at the top of the ring (12 o'clock on screen): where to push the stick before
            // turning it, so the hint also says where the circle starts. Drawn over the chef.
            var edge = kit.Centre + Vector3.up * (_lift + 0.05f) + forward.normalized * kit.Radius;
            TimingGaugeKit.FaceCamera(kit.Button, camera, edge, TimingGaugeKit.MetresFor(camera, edge, _buttonPixels) * Mathf.Lerp(0.6f, 1f, eased));
        }

        // The spot's own stain, same shape, colour and turn as its patch, a little smaller than the
        // ring, drawn over whatever's in front (a counter hiding floor dirt).
        private bool ShowStain(Kit kit)
        {
            if (kit.Spot == null)
                return false;
            if (_patches == null)
                _patches = FindFirstObjectByType<CleaningView>();
            if (_patches == null)
                return false;
            var stain = _patches.Stain(kit.Spot);
            if (!_stains.TryGetValue(kit.Spot.Id, out var mesh))
                _stains[kit.Spot.Id] = mesh = stain.Shape;
            else
                Destroy(stain.Shape);
            kit.Stain.GetComponent<MeshFilter>().sharedMesh = mesh;
            TimingGaugeKit.Paint(kit.Stain, stain.Colour);
            // The blob is about one unit across; keep it inside the ring, however big the patch.
            var size = Mathf.Min(stain.Size, kit.Radius * 1.5f);
            kit.Stain.transform.SetPositionAndRotation(kit.Ring.position + Vector3.up * 0.005f, Quaternion.Euler(0f, stain.Turn, 0f));
            kit.Stain.transform.localScale = new Vector3(size, 1f, size) / Mathf.Max(0.001f, kit.Ring.localScale.x);
            return true;
        }

        private Kit Build()
        {
            var ring = new GameObject("CleanHintRing") { hideFlags = HideFlags.DontSave }.transform;
            ring.SetParent(transform, false);
            var holder = new GameObject("CleanHintX") { hideFlags = HideFlags.DontSave }.transform;
            holder.SetParent(transform, false);
            var button = TimingGaugeKit.XButton(holder, _onTop, ButtonOrder);
            button.localPosition = Vector3.zero;
            var kit = new Kit { Ring = ring, Button = holder };
            // The stain: a flat blob lying in the ring, drawn over the counter in front of it.
            kit.Stain = TimingGaugeKit.Block(ring, "Stain", _onTop, _stain, RingOrder - 1);
            ring.gameObject.SetActive(false);
            holder.gameObject.SetActive(false);
            return kit;
        }

        // Thin, well-spaced segments: the empty ring, before any cleaning.
        private void BuildRing(Kit kit, float radius)
        {
            if (kit.Segments != null)
                foreach (var segment in kit.Segments)
                {
                    Destroy(segment.GetComponent<MeshFilter>().sharedMesh);
                    Destroy(segment.gameObject);
                }
            var step = Mathf.PI * 2f / Segments;
            var width = radius * 0.13f;
            var height = radius * 0.05f;
            kit.Segments = new Renderer[Segments];
            for (var i = 0; i < Segments; i++)
            {
                var mesh = StationKitMeshes.RingSector(radius - width / 2f, radius + width / 2f,
                    (i + SegmentGap * 0.5f) * step, (i + 1 - SegmentGap * 0.5f) * step, height, height * 0.35f);
                kit.Segments[i] = TimingGaugeKit.Piece(kit.Ring, "Segment", mesh, _onTop, _dirtRing, RingOrder);
                kit.Segments[i].shadowCastingMode = ShadowCastingMode.Off;
            }
            kit.BuiltRadius = radius;
        }
    }
}
