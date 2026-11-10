using System.Collections.Generic;
using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Cleaning;
using Game.Simulation.Cleaning;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Game.Presentation
{
    /// <summary>
    /// Shows dirt and the cleaning game (Phase 5, decision 2026-11-10).
    ///
    /// <para><b>Dirt</b> lies flat on the floor, tables and counters as cartoony patches: sauce and
    /// crumbs where guests ate, a splash with shards where a plate broke, soot by a burnt pan, grey
    /// footprints, white foam after a fire. Each shrinks as it's wiped and is gone when clean.</para>
    ///
    /// <para><b>The game</b> is drawn on the dirt itself (the station is the interface, 2026-11-09): a
    /// track of beads in the shape to trace, lit in the chef's colour up to the marker, chevrons
    /// ahead showing the way, and the tool's head (mop, cloth, extinguisher nozzle) riding the marker,
    /// so the stick visibly moves it. It turns with the camera, never the furniture: pushing the
    /// stick up moves the marker up the screen at any of the 8 rotations, with the chef on any side.
    /// The track is drawn over the chef (<c>Game/ToonOnTop</c>), like the wash ring.</para>
    /// </summary>
    public sealed class CleaningView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private const int TrackBeads = 36;
        private const int Chevrons = 3;

        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private CoopSettings _settings;
        [Tooltip("Game/ToonOnTop: the track, marker and chevrons, drawn over whatever stands in front.")]
        [SerializeField] private Material _onTop;
        [Tooltip("Game/Toon: the dirt patches and the tools; each piece sets its own colour.")]
        [SerializeField] private Material _toon;

        [Header("Dirt")]
        [Tooltip("Metres across a patch at its dirtiest, by kind: smudge, spill, scorch, scuff, foam.")]
        [SerializeField] private float[] _patchSize = { 0.34f, 0.7f, 0.42f, 0.5f, 0.75f };
        [SerializeField] private Color[] _patchColor =
        {
            new Color(0.62f, 0.33f, 0.16f), // smudge: gravy and crumbs
            new Color(0.70f, 0.46f, 0.22f), // spill: sauce and food
            new Color(0.17f, 0.15f, 0.14f), // scorch: soot
            new Color(0.40f, 0.36f, 0.33f), // scuff: footprints
            new Color(0.93f, 0.96f, 0.98f), // foam
        };
        [SerializeField] private Color _shardColor = new Color(0.95f, 0.95f, 0.92f);
        [Tooltip("Smallest a patch shrinks to before it goes, as a share of its size.")]
        [SerializeField, Range(0f, 1f)] private float _cleanestShown = 0.3f;
        [Tooltip("Metres a patch sits above what it's on, so it never flickers into it.")]
        [SerializeField, Min(0f)] private float _lift = 0.004f;

        [Header("Track")]
        [Tooltip("Metres from the middle to the track's edge, per tool: cloth, mop, extinguisher.")]
        [SerializeField] private Vector3 _trackRadius = new Vector3(0.45f, 0.75f, 0.5f);
        [Tooltip("Metres above the dirt (or the pan) the track floats, per tool: cloth, mop, extinguisher.")]
        [SerializeField] private Vector3 _trackHeight = new Vector3(0.03f, 0.03f, 0.32f);
        [SerializeField, Min(0.001f)] private float _beadSize = 0.06f;
        [SerializeField, Min(0.001f)] private float _markerSize = 0.16f;
        [SerializeField] private Color _ahead = new Color(0.97f, 0.97f, 0.95f);
        [SerializeField] private Color _offTrack = new Color(0.95f, 0.30f, 0.26f);
        [Tooltip("How far ahead of the marker the chevrons point the way, in loops.")]
        [SerializeField, Range(0f, 0.3f)] private float _chevronLead = 0.06f;
        [SerializeField, Range(0f, 0.3f)] private float _pulse = 0.12f;

        [Header("Tools")]
        [SerializeField] private Color _mopHead = new Color(0.85f, 0.82f, 0.7f);
        [SerializeField] private Color _handle = new Color(0.55f, 0.38f, 0.22f);
        [SerializeField] private Color _cloth = new Color(0.35f, 0.6f, 0.9f);
        [SerializeField] private Color _extinguisher = new Color(0.86f, 0.12f, 0.1f);

        private readonly Dictionary<DirtSpot, Transform> _patches = new Dictionary<DirtSpot, Transform>();
        private readonly Dictionary<ChefCleaning, Kit> _kits = new Dictionary<ChefCleaning, Kit>();
        private readonly List<DirtSpot> _gone = new List<DirtSpot>();
        private readonly List<ChefCleaning> _idle = new List<ChefCleaning>();
        private MaterialPropertyBlock _block;
        private DirtMap _dirt;
        private Mesh _sphere;
        private Mesh _cylinder;
        private Mesh _cube;

        private sealed class Kit
        {
            public Transform Root;
            public Renderer[] Beads;
            public Renderer[] Chevrons;
            public Renderer Marker;
            public Transform Tool;
            public Renderer ToolHead;
            public Transform Handle;
            public CleaningTool Built;
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _sphere = BuiltIn(PrimitiveType.Sphere);
            _cylinder = BuiltIn(PrimitiveType.Cylinder);
            _cube = BuiltIn(PrimitiveType.Cube);
        }

        private void OnDisable()
        {
            if (_dirt != null)
                _dirt.Removed -= HandleRemoved;
            _dirt = null;
        }

        private void LateUpdate()
        {
            var services = _root != null ? _root.Services : null;
            if (services == null)
                return;
            if (_dirt != services.Dirt)
            {
                _dirt = services.Dirt;
                _dirt.Removed += HandleRemoved;
            }

            foreach (var spot in _dirt.Spots)
                ShowPatch(spot);
            ShowGames();
        }

        // ---- Dirt ----

        private void HandleRemoved(DirtSpot spot)
        {
            if (_patches.TryGetValue(spot, out var patch))
            {
                if (patch != null)
                    Destroy(patch.gameObject);
                _patches.Remove(spot);
            }
        }

        private void ShowPatch(DirtSpot spot)
        {
            if (!_patches.TryGetValue(spot, out var patch) || patch == null)
            {
                patch = BuildPatch(spot);
                _patches[spot] = patch;
            }
            var kind = (int)spot.Kind;
            var size = Pick(_patchSize, kind, 0.4f) * Mathf.Sqrt(Mathf.Clamp(spot.Peak, 0.5f, 4f) / 2f);
            var shown = Mathf.Lerp(_cleanestShown, 1f, spot.Remaining) * size;
            var at = KitchenMess.WorldOf(spot) + Vector3.up * _lift;
            patch.position = at;
            var turn = spot.Host is Component host && host != null ? host.transform.eulerAngles.y : 0f;
            patch.rotation = Quaternion.Euler(0f, turn + spot.Id * 47f, 0f);
            patch.localScale = new Vector3(shown, 1f, shown);
        }

        // A flat, lumpy blob with a few crumbs (or shards) round it, different for every spot.
        private Transform BuildPatch(DirtSpot spot)
        {
            var root = new GameObject($"Dirt_{spot.Kind}_{spot.Id}").transform;
            root.SetParent(transform, false);
            var random = new System.Random(spot.Id * 7919);
            var color = Pick(_patchColor, (int)spot.Kind, Color.gray);

            var blob = Piece(root, BlobMesh(random), _toon, color, 0);
            blob.transform.localScale = Vector3.one;

            // Bits round it: crumbs where guests ate, shards from a broken plate, flecks of soot or foam.
            var bits = spot.Kind == DirtKind.Scuff ? 0 : 5;
            for (var i = 0; i < bits; i++)
            {
                var angle = (float)(random.NextDouble() * Mathf.PI * 2f);
                var distance = 0.35f + (float)random.NextDouble() * 0.25f;
                var bitColor = spot.Kind == DirtKind.Spill && i % 2 == 0 ? _shardColor : color * 0.8f;
                var bit = Piece(root, spot.Kind == DirtKind.Spill ? _cube : _sphere, _toon, bitColor, 0);
                bit.transform.localPosition = new Vector3(Mathf.Cos(angle) * distance, 0.01f, Mathf.Sin(angle) * distance);
                bit.transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
                var s = 0.05f + (float)random.NextDouble() * 0.04f;
                bit.transform.localScale = new Vector3(s, 0.02f, s * (spot.Kind == DirtKind.Spill ? 0.5f : 1f));
            }

            // Footprints read as two small soles side by side.
            if (spot.Kind == DirtKind.Scuff)
            {
                blob.enabled = false;
                for (var i = 0; i < 4; i++)
                {
                    var sole = Piece(root, _cube, _toon, color, 0);
                    sole.transform.localPosition = new Vector3((i % 2 == 0 ? -0.12f : 0.12f), 0.004f, -0.3f + i * 0.2f);
                    sole.transform.localScale = new Vector3(0.12f, 0.008f, 0.22f);
                }
            }
            return root;
        }

        // A lumpy disc of radius about 0.5, a few millimetres thick, flat-shaded.
        private static Mesh BlobMesh(System.Random random)
        {
            const int sides = 14;
            const float thickness = 0.006f;
            var radii = new float[sides];
            for (var i = 0; i < sides; i++)
                radii[i] = 0.36f + (float)random.NextDouble() * 0.16f;

            var vertices = new List<Vector3> { new Vector3(0f, thickness, 0f) };
            var triangles = new List<int>();
            for (var i = 0; i < sides; i++)
            {
                var angle = i * Mathf.PI * 2f / sides;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radii[i], thickness, Mathf.Sin(angle) * radii[i]));
            }
            for (var i = 0; i < sides; i++)
                triangles.AddRange(new[] { 0, 1 + (i + 1) % sides, 1 + i });

            // The rim, down to the surface.
            for (var i = 0; i < sides; i++)
            {
                var a = vertices[1 + i];
                var b = vertices[1 + (i + 1) % sides];
                var start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(new Vector3(b.x, 0f, b.z));
                vertices.Add(new Vector3(a.x, 0f, a.z));
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            var mesh = new Mesh { name = "DirtBlob" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---- The game ----

        private void ShowGames()
        {
            _idle.Clear();
            foreach (var pair in _kits)
            {
                if (pair.Key == null || !pair.Key.IsCleaning)
                    _idle.Add(pair.Key);
            }
            foreach (var chef in _idle)
            {
                if (_kits.TryGetValue(chef, out var kit) && kit.Root != null)
                {
                    // The tool goes away with the track.
                    kit.Root.gameObject.SetActive(false);
                    if (kit.Tool != null)
                        kit.Tool.gameObject.SetActive(false);
                    if (kit.Handle != null)
                        kit.Handle.gameObject.SetActive(false);
                }
                if (chef == null)
                    _kits.Remove(chef);
            }

            foreach (var chef in ChefCleaning.All)
            {
                if (!chef.IsCleaning)
                    continue;
                if (!_kits.TryGetValue(chef, out var kit))
                {
                    kit = BuildKit();
                    _kits[chef] = kit;
                }
                kit.Root.gameObject.SetActive(true);
                if (kit.Tool != null)
                    kit.Tool.gameObject.SetActive(true);
                if (kit.Handle != null)
                    kit.Handle.gameObject.SetActive(true);
                Draw(chef, kit);
            }
        }

        private void Draw(ChefCleaning chef, Kit kit)
        {
            var game = chef.Game;
            var tool = chef.Tool;
            var index = tool == CleaningTool.Mop ? 1 : tool == CleaningTool.Extinguisher ? 2 : 0;
            var radius = _trackRadius[index];
            var colour = PlayerColour(chef);

            // Laid flat and turned to the camera: stick up is screen up, wherever the chef stands.
            var (right, forward) = ScreenAxes();
            var centre = chef.Where + Vector3.up * _trackHeight[index];
            kit.Root.SetPositionAndRotation(centre, Quaternion.LookRotation(forward, Vector3.up));

            Vector3 OnTrack(float along)
            {
                var p = TraceGame.PointAt(game.Shape, along - Mathf.Floor(along));
                return new Vector3(p.X * radius, 0f, p.Y * radius);
            }

            // Beads behind the marker light up in the chef's colour, as far as this loop has gone.
            var marker = game.Along;
            for (var i = 0; i < kit.Beads.Length; i++)
            {
                var along = i / (float)kit.Beads.Length;
                var bead = kit.Beads[i];
                bead.transform.localPosition = OnTrack(along);
                var behind = (marker - along + 1f) % 1f;
                var lit = behind < Mathf.Max(0.02f, Mathf.Repeat(game.Loops, 1f)) || game.Loops >= 1f;
                SetColor(bead, lit ? colour : _ahead);
                bead.transform.localScale = Vector3.one * _beadSize * (lit ? 1.15f : 0.9f);
            }

            // Chevrons a little ahead show which way round to go.
            for (var i = 0; i < kit.Chevrons.Length; i++)
            {
                var at = marker + _chevronLead * (i + 1);
                var here = OnTrack(at);
                var next = OnTrack(at + 0.01f);
                var chevron = kit.Chevrons[i];
                chevron.transform.localPosition = here;
                chevron.transform.localRotation = Quaternion.LookRotation(next - here, Vector3.up) * Quaternion.Euler(0f, 45f, 0f);
                chevron.transform.localScale = new Vector3(_beadSize * 1.6f, _beadSize * 0.5f, _beadSize * 1.6f);
                SetColor(chevron, game.OnTrack ? colour : _ahead);
            }

            // The marker pulses while it's being pulled along, and goes red when the stick has lost it.
            var pulse = game.OnTrack ? 1f + _pulse * Mathf.Sin(Time.time * 18f) : 0.9f;
            kit.Marker.transform.localPosition = OnTrack(marker) + Vector3.up * 0.01f;
            kit.Marker.transform.localScale = new Vector3(_markerSize, _markerSize * 0.5f, _markerSize) * pulse;
            SetColor(kit.Marker, game.OnTrack ? colour : _offTrack);

            ShowTool(chef, kit, kit.Root.TransformPoint(OnTrack(marker)));
        }

        // The tool's head rides the marker; the mop's handle reaches back to the chef's hands.
        private void ShowTool(ChefCleaning chef, Kit kit, Vector3 marker)
        {
            if (kit.Built != chef.Tool)
                BuildTool(kit, chef.Tool);
            var hands = chef.transform.position + Vector3.up * 1.0f + chef.transform.forward * 0.3f;
            switch (chef.Tool)
            {
                case CleaningTool.Mop:
                {
                    var head = new Vector3(marker.x, chef.Where.y + 0.03f, marker.z);
                    kit.Tool.position = head;
                    kit.Tool.rotation = Quaternion.LookRotation(Flat(head - chef.transform.position), Vector3.up);
                    PlaceHandle(kit.Handle, hands, head);
                    break;
                }
                case CleaningTool.Extinguisher:
                {
                    // The bottle in the chef's hands, its hose nozzle on the marker over the flames.
                    kit.Tool.position = chef.transform.position + chef.transform.forward * 0.35f + Vector3.up * 0.8f;
                    kit.Tool.rotation = chef.transform.rotation;
                    PlaceHandle(kit.Handle, kit.Tool.position + Vector3.up * 0.2f, marker);
                    break;
                }
                default:
                    kit.Tool.position = new Vector3(marker.x, chef.Where.y + 0.015f, marker.z);
                    kit.Tool.rotation = Quaternion.LookRotation(Flat(marker - chef.transform.position), Vector3.up);
                    break;
            }
        }

        private static void PlaceHandle(Transform handle, Vector3 from, Vector3 to)
        {
            if (handle == null)
                return;
            var along = to - from;
            handle.position = (from + to) * 0.5f;
            handle.rotation = along.sqrMagnitude > 1e-4f ? Quaternion.FromToRotation(Vector3.up, along) : Quaternion.identity;
            var scale = handle.localScale;
            handle.localScale = new Vector3(scale.x, along.magnitude * 0.5f, scale.z);
        }

        private Kit BuildKit()
        {
            var root = new GameObject("CleaningTrack").transform;
            root.SetParent(transform, false);
            var kit = new Kit { Root = root, Beads = new Renderer[TrackBeads], Chevrons = new Renderer[Chevrons] };
            for (var i = 0; i < TrackBeads; i++)
                kit.Beads[i] = Piece(root, _sphere, _onTop, _ahead, 0);
            for (var i = 0; i < Chevrons; i++)
                kit.Chevrons[i] = Piece(root, _cube, _onTop, _ahead, 1);
            kit.Marker = Piece(root, _cylinder, _onTop, _ahead, 2);
            return kit;
        }

        private void BuildTool(Kit kit, CleaningTool tool)
        {
            if (kit.Tool != null)
                Destroy(kit.Tool.gameObject);
            if (kit.Handle != null)
                Destroy(kit.Handle.gameObject);
            kit.Handle = null;
            kit.Built = tool;

            var root = new GameObject($"Tool_{tool}").transform;
            root.SetParent(kit.Root.parent, false);
            kit.Tool = root;
            switch (tool)
            {
                case CleaningTool.Mop:
                    kit.ToolHead = Piece(root, _cylinder, _toon, _mopHead, 0);
                    kit.ToolHead.transform.localScale = new Vector3(0.3f, 0.03f, 0.2f);
                    kit.Handle = Piece(kit.Root.parent, _cylinder, _toon, _handle, 0).transform;
                    kit.Handle.localScale = new Vector3(0.035f, 0.5f, 0.035f);
                    break;
                case CleaningTool.Extinguisher:
                    kit.ToolHead = Piece(root, _cylinder, _toon, _extinguisher, 0);
                    kit.ToolHead.transform.localScale = new Vector3(0.16f, 0.22f, 0.16f);
                    kit.Handle = Piece(kit.Root.parent, _cylinder, _toon, new Color(0.15f, 0.15f, 0.16f), 0).transform;
                    kit.Handle.localScale = new Vector3(0.025f, 0.5f, 0.025f);
                    break;
                default:
                    kit.ToolHead = Piece(root, _cube, _toon, _cloth, 0);
                    kit.ToolHead.transform.localScale = new Vector3(0.16f, 0.025f, 0.12f);
                    break;
            }
        }

        // ---- Helpers ----

        private Renderer Piece(Transform parent, Mesh mesh, Material material, Color color, int order)
        {
            var go = new GameObject(mesh != null ? mesh.name : "Piece");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.sortingOrder = order;
            SetColor(renderer, color);
            return renderer;
        }

        private void SetColor(Renderer target, Color color)
        {
            target.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(_block);
        }

        private Color PlayerColour(ChefCleaning chef) =>
            _settings != null && chef.TryGetComponent(out PlayerInput player) ? _settings.GetPlayerColor(player.playerIndex) : Color.white;

        private static Mesh BuiltIn(PrimitiveType type)
        {
            var probe = GameObject.CreatePrimitive(type);
            var mesh = probe.GetComponent<MeshFilter>().sharedMesh;
            Destroy(probe);
            return mesh;
        }

        private static T Pick<T>(T[] values, int index, T fallback) =>
            values != null && values.Length > 0 ? values[Mathf.Clamp(index, 0, values.Length - 1)] : fallback;

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v : Vector3.forward;
        }

        // Screen right and screen up, laid on the floor.
        private static (Vector3 Right, Vector3 Forward) ScreenAxes()
        {
            var camera = Camera.main;
            var forward = camera != null ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up) : Vector3.forward;
            if (forward.sqrMagnitude < 1e-4f)
                forward = camera != null ? Vector3.ProjectOnPlane(camera.transform.up, Vector3.up) : Vector3.forward;
            forward.Normalize();
            return (Vector3.Cross(Vector3.up, forward), forward);
        }
    }
}
