using System.Collections.Generic;
using Game.Data;
using Game.Gameplay;
using Game.Simulation.Days;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Game.Presentation
{
    /// <summary>Where a player's foot ring is drawn this frame (decision 2026-11-11).</summary>
    public enum FootRingPlace
    {
        Hidden,
        AtChef,
        AtCursor,
    }

    /// <summary>
    /// A ring on the floor in each player's colour, so who is who reads at a glance (decision
    /// 2026-11-11): at the chef's feet in the office and in the kitchen during Prep, and under the
    /// player's own cursor while they rearrange, leaving the chef. Service and Review show none:
    /// the chefs' tinted bodies are enough there, and the floor stays clear for the food.
    /// One per scene; in the office leave the kitchen fields empty and the rings always show.
    /// </summary>
    public sealed class FootRingView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private CoopSettings _settings;
        [Tooltip("At a chef's feet: unlit and transparent (SH_FootRing), hidden behind what stands in front.")]
        [SerializeField] private Material _material;
        [Tooltip("Under a cursor: the same, drawn over furniture, so it reads where the cursor is over a counter.")]
        [SerializeField] private Material _cursorMaterial;
        [Tooltip("The kitchen's root: rings show in Prep only. Empty in the office, where they always show.")]
        [SerializeField] private KitchenCompositionRoot _root;
        [Tooltip("The kitchen's layout editor: a rearranging player's ring moves to their cursor.")]
        [SerializeField] private LayoutEditor _editor;

        [Tooltip("Metres from the centre to the ring's outer edge: just outside the chef's shoulders.")]
        [SerializeField, Min(0.05f)] private float _outerRadius = 0.48f;
        [Tooltip("Metres across the coloured band.")]
        [SerializeField, Min(0.01f)] private float _bandWidth = 0.09f;
        [Tooltip("Metres above the floor: clears the floor and dirt decals so it never z-fights.")]
        [SerializeField, Min(0.001f)] private float _height = 0.03f;
        [Tooltip("The band's opacity.")]
        [SerializeField, Range(0f, 1f)] private float _bandOpacity = 0.9f;
        [Tooltip("The disc inside the band: faint, so the floor still shows through.")]
        [SerializeField, Range(0f, 1f)] private float _fillOpacity = 0.22f;
        [Tooltip("Segments round the circle; 48 looks round from the overhead camera.")]
        [SerializeField, Range(8, 128)] private int _segments = 48;

        private readonly Dictionary<PlayerInput, Ring> _rings = new Dictionary<PlayerInput, Ring>();
        private readonly List<PlayerInput> _gone = new List<PlayerInput>();
        private Mesh _bandMesh;
        private Mesh _fillMesh;

        private sealed class Ring
        {
            public GameObject Root;
            public MeshRenderer[] Parts;
            public bool OnTop;
        }

        /// <summary>
        /// Office and Prep show the ring (<paramref name="showing"/>); a rearranging player's goes to
        /// their cursor, everyone else's stays at their chef.
        /// </summary>
        public static FootRingPlace PlaceFor(bool showing, bool rearranging)
        {
            if (!showing)
                return FootRingPlace.Hidden;
            return rearranging ? FootRingPlace.AtCursor : FootRingPlace.AtChef;
        }

        /// <summary>
        /// A flat annulus in the XZ plane facing up, from <paramref name="inner"/> to
        /// <paramref name="outer"/> metres (inner 0 makes a disc).
        /// </summary>
        public static Mesh BuildRing(float inner, float outer, int segments)
        {
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * inner;
                vertices[i * 2 + 1] = direction * outer;

                // Two triangles per segment, wound clockwise seen from above so they face up.
                var next = (i + 1) % segments;
                var t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = next * 2;
                triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2 + 1;
                triangles[t + 4] = next * 2;
                triangles[t + 5] = next * 2 + 1;
            }
            var mesh = new Mesh { name = "FootRing", vertices = vertices, triangles = triangles };
            var normals = new Vector3[vertices.Length];
            for (var i = 0; i < normals.Length; i++)
                normals[i] = Vector3.up;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Awake()
        {
            var inner = Mathf.Max(0f, _outerRadius - _bandWidth);
            _bandMesh = BuildRing(inner, _outerRadius, _segments);
            _fillMesh = BuildRing(0f, inner, _segments);
        }

        private void OnDestroy()
        {
            Destroy(_bandMesh);
            Destroy(_fillMesh);
        }

        private void LateUpdate()
        {
            var showing = Showing();
            foreach (var player in PlayerInput.all)
            {
                var rearranging = _editor != null && _editor.IsManaging(player);
                var place = PlaceFor(showing, rearranging);
                if (place == FootRingPlace.AtChef && !player.gameObject.activeInHierarchy)
                    place = FootRingPlace.Hidden; // a chef off the floor (the elevator ride) takes theirs along

                if (!_rings.TryGetValue(player, out var ring))
                {
                    if (place == FootRingPlace.Hidden)
                        continue;
                    ring = Create(player.playerIndex);
                    _rings[player] = ring;
                }

                ring.Root.SetActive(place != FootRingPlace.Hidden);
                UseMaterial(ring, place == FootRingPlace.AtCursor);
                if (place == FootRingPlace.AtChef)
                    ring.Root.transform.position = player.transform.position + Vector3.up * _height;
                else if (place == FootRingPlace.AtCursor && CursorOf(player, out var at))
                    ring.Root.transform.position = new Vector3(at.x, _height, at.y);
            }

            // Players who left: their PlayerInput is destroyed, so the key reads as null.
            _gone.Clear();
            foreach (var player in _rings.Keys)
            {
                if (player == null)
                    _gone.Add(player);
            }
            foreach (var player in _gone)
            {
                Destroy(_rings[player].Root);
                _rings.Remove(player);
            }
        }

        private bool Showing()
        {
            if (_root == null)
                return true; // the office
            var days = _root.Services?.Days;
            return days != null && days.Phase == DayPhase.Prep;
        }

        // Follows the cursor as its frame does (LayoutEditorHud): where a carried piece would land.
        private bool CursorOf(PlayerInput player, out Vector2 at)
        {
            foreach (var cursor in _editor.Cursors)
            {
                if (cursor.Player != player)
                    continue;
                var position = cursor.Held != null ? cursor.Landing : cursor.Position;
                at = new Vector2(position.X, position.Y);
                return true;
            }
            at = default;
            return false;
        }

        private Ring Create(int playerIndex)
        {
            var root = new GameObject($"FootRing_P{playerIndex + 1}");
            root.transform.SetParent(transform, false);
            var colour = _settings != null ? _settings.GetPlayerColor(playerIndex) : Color.white;
            var parts = new[]
            {
                AddPart(root.transform, "Band", _bandMesh, new Color(colour.r, colour.g, colour.b, _bandOpacity)),
                AddPart(root.transform, "Fill", _fillMesh, new Color(colour.r, colour.g, colour.b, _fillOpacity)),
            };
            return new Ring { Root = root, Parts = parts };
        }

        private void UseMaterial(Ring ring, bool onTop)
        {
            if (ring.OnTop == onTop)
                return;
            ring.OnTop = onTop;
            var material = onTop && _cursorMaterial != null ? _cursorMaterial : _material;
            foreach (var part in ring.Parts)
                part.sharedMaterial = material;
        }

        private MeshRenderer AddPart(Transform parent, string name, Mesh mesh, Color colour)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, colour);
            renderer.SetPropertyBlock(block);
            return renderer;
        }
    }
}
