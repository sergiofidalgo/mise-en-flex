using System.Collections.Generic;
using Game.Data;
using Game.Gameplay;
using Game.Simulation.Days;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Game.Presentation
{
    /// <summary>
    /// A ring on the floor in each player's colour, so who is who reads at a glance (decision
    /// 2026-11-11): at the chef's feet in the office and in the kitchen during Prep. A player who is
    /// rearranging has none: their cursor's frame is already in their colour (decision 2026-11-13),
    /// and the chef they left standing doesn't need marking. Service and Review show none:
    /// the chefs' tinted bodies are enough there, and the floor stays clear for the food.
    /// One per scene; in the office leave the kitchen fields empty and the rings always show.
    /// </summary>
    public sealed class FootRingView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private CoopSettings _settings;
        [Tooltip("Unlit and transparent (SH_FootRing); tinted per player through a property block.")]
        [SerializeField] private Material _material;
        [Tooltip("The kitchen's root: rings show in Prep only. Empty in the office, where they always show.")]
        [SerializeField] private KitchenCompositionRoot _root;
        [Tooltip("The kitchen's layout editor: a rearranging player's ring hides (their cursor shows their colour).")]
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

        private readonly Dictionary<PlayerInput, GameObject> _rings = new Dictionary<PlayerInput, GameObject>();
        private readonly List<PlayerInput> _gone = new List<PlayerInput>();
        private Mesh _bandMesh;
        private Mesh _fillMesh;

        /// <summary>
        /// Whether a chef shows their ring: in the office and Prep (<paramref name="showing"/>), unless
        /// their player is rearranging, when the cursor's own frame shows their colour instead.
        /// </summary>
        public static bool ShowsRing(bool showing, bool rearranging) => showing && !rearranging;

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
                // A chef switched off (the elevator ride) shows none.
                var shown = ShowsRing(showing, rearranging) && player.gameObject.activeInHierarchy;

                if (!_rings.TryGetValue(player, out var ring))
                {
                    if (!shown)
                        continue;
                    ring = Create(player.playerIndex);
                    _rings[player] = ring;
                }

                ring.SetActive(shown);
                if (shown)
                    ring.transform.position = player.transform.position + Vector3.up * _height;
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
                Destroy(_rings[player]);
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

        private GameObject Create(int playerIndex)
        {
            var root = new GameObject($"FootRing_P{playerIndex + 1}");
            root.transform.SetParent(transform, false);
            var colour = _settings != null ? _settings.GetPlayerColor(playerIndex) : Color.white;
            AddPart(root.transform, "Band", _bandMesh, new Color(colour.r, colour.g, colour.b, _bandOpacity));
            AddPart(root.transform, "Fill", _fillMesh, new Color(colour.r, colour.g, colour.b, _fillOpacity));
            return root;
        }

        private void AddPart(Transform parent, string name, Mesh mesh, Color colour)
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
        }
    }
}
