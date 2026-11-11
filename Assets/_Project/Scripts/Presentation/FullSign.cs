using System;
using Game.Gameplay.Stations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation
{
    /// <summary>
    /// The 🚫 that pops up over a station that can't take anything more: the bin, a fridge, a
    /// shelf (the user's notes, 2026-11-08 and 2026-11-11). It sits just above the station's top,
    /// bobs, faces the camera, and rocks when a player with something in hand faces the station,
    /// since that's when they'd try. Owned by the station's view, which says when it's full.
    /// </summary>
    [Serializable]
    public sealed class FullSign
    {
        [Tooltip("Gap between the station's top and the bottom of the sign (m).")]
        [SerializeField, Min(0f)] private float _gap = 0.05f;
        [Tooltip("The sign's width in the world (m).")]
        [SerializeField, Min(0.05f)] private float _size = 0.34f;
        [Tooltip("How long the sign takes to pop in or out (s).")]
        [SerializeField, Min(0.01f)] private float _pop = 0.18f;
        [Tooltip("How far it bobs up and down (m), and how many times a second.")]
        [SerializeField, Min(0f)] private float _bobHeight = 0.03f;
        [SerializeField, Min(0f)] private float _bobRate = 1.2f;
        [Tooltip("How far it rocks (degrees) when someone holding something faces the full station, and how fast.")]
        [SerializeField, Min(0f)] private float _shakeAngle = 14f;
        [SerializeField, Min(0f)] private float _shakeRate = 22f;

        private static Sprite _noEntry;

        private Interactable _station;
        private Transform _anchor;
        private Transform _sign;
        private float _top;
        private float _shown;

        /// <summary>Makes the (hidden) sign. Call from the view's Awake.</summary>
        /// <param name="anchor">What it stands over, if not the station's centre: the bin's model slides within its slot.</param>
        public void Build(Interactable station, Transform anchor = null)
        {
            _station = station;
            _anchor = anchor != null ? anchor : station.transform;
            _top = TopOf(station.transform);

            var go = new GameObject("FullSign");
            go.transform.SetParent(station.transform, false);
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = NoEntrySprite();
            sprite.sortingOrder = 10;
            go.SetActive(false);
            _sign = go.transform;
        }

        /// <summary>Call from the view's LateUpdate, after the camera has moved.</summary>
        public void Tick(bool full)
        {
            if (_sign == null)
                return;

            _shown = Mathf.MoveTowards(_shown, full ? 1f : 0f, Time.deltaTime / _pop);
            var visible = _shown > 0f;
            if (_sign.gameObject.activeSelf != visible)
                _sign.gameObject.SetActive(visible);
            if (!visible)
                return;

            // Pops in with a little overshoot, so it catches the eye the moment it fills.
            var size = _size * Overshoot(_shown);
            _sign.localScale = Vector3.one * size;
            var bob = _bobHeight * Mathf.Sin(Time.time * _bobRate * 2f * Mathf.PI);
            var over = _anchor.position;
            over.y = _station.transform.position.y + _top + _gap + _size / 2f + bob;
            _sign.position = over;

            // Faces the camera flat on, so it reads as a sign from the overhead angle.
            var camera = Camera.main;
            var rock = SomeoneTrying() ? _shakeAngle * Mathf.Sin(Time.time * _shakeRate) : 0f;
            if (camera != null)
                _sign.rotation = camera.transform.rotation * Quaternion.Euler(0f, 0f, rock);
        }

        // 0..1 to a scale that swells a little past 1 and settles back (ease-out-back).
        private static float Overshoot(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private bool SomeoneTrying()
        {
            foreach (var player in PlayerInput.all)
            {
                if (player.TryGetComponent(out PlayerInteractor interactor)
                    && interactor.Target == _station && !interactor.Hands.IsEmpty)
                    return true;
            }
            return false;
        }

        // The station's own top, from its meshes, measured once: its model doesn't change height.
        private static float TopOf(Transform station)
        {
            var top = 0f;
            foreach (var renderer in station.GetComponentsInChildren<MeshRenderer>())
                top = Mathf.Max(top, renderer.bounds.max.y - station.position.y);
            return top;
        }

        // A no-entry sign drawn in code (a white disc, a red ring and a slash from top left to
        // bottom right like 🚫, a dark rim so it reads on any floor), so it needs no texture asset.
        // TODO(Phase 9): swap for a drawn icon if the art pass wants one.
        private static Sprite NoEntrySprite()
        {
            if (_noEntry != null)
                return _noEntry;

            const int size = 128;
            const float outer = 0.5f;      // the sign's radius, in texture widths
            const float rimWidth = 0.035f;
            const float ringWidth = 0.11f;
            const float slashHalf = 0.055f;
            const float softEdge = 1.5f / size;
            var red = new Color(0.86f, 0.16f, 0.14f, 1f);
            var rim = new Color(0.10f, 0.08f, 0.08f, 1f);
            var white = new Color(1f, 0.98f, 0.95f, 1f);

            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var px = (x + 0.5f) / size - 0.5f;
                    var py = (y + 0.5f) / size - 0.5f;
                    var r = Mathf.Sqrt(px * px + py * py);
                    var fromSlash = Mathf.Abs(px + py) / Mathf.Sqrt(2f); // distance from the line y = -x

                    var colour = r > outer - rimWidth ? rim
                        : r > outer - rimWidth - ringWidth || fromSlash < slashHalf ? red
                        : white;
                    colour.a = Mathf.Clamp01((outer - r) / softEdge);
                    pixels[y * size + x] = colour;
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "TEX_FullSign",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            _noEntry = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _noEntry;
        }
    }
}
