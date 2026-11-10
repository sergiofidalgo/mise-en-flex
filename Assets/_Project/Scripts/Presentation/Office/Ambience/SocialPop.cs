using UnityEngine;

namespace Game.Presentation.Office.Ambience
{
    /// <summary>
    /// A heart, a star or a comment bubble rising over someone's phone now and then (user's note,
    /// 2026-11-10: the opening must stress that the game is about social media and reviews). It
    /// pops in, floats up, shrinks away, always facing the camera; timed by
    /// <see cref="AmbientMotion.Pop"/>, so every phone pops at its own moments.
    /// </summary>
    public sealed class SocialPop : MonoBehaviour
    {
        [Tooltip("One icon per kind, children of this, hidden between pops.")]
        [SerializeField] private GameObject[] _icons = System.Array.Empty<GameObject>();
        [SerializeField, Min(1f)] private float _period = 5f;
        [SerializeField, Min(0.3f)] private float _duration = 1.6f;
        [Tooltip("How far the icon rises over its pop (m).")]
        [SerializeField, Min(0f)] private float _rise = 0.8f;
        [SerializeField, Min(0.05f)] private float _size = 0.45f;
        [SerializeField] private float _seed;

        private int _showing = -1;
        private Camera _camera;

        public void Set(GameObject[] icons, float period, float duration, float rise, float size, float seed) =>
            (_icons, _period, _duration, _rise, _size, _seed) = (icons, period, duration, rise, size, seed);

        private void Awake() => Show(-1);

        private void LateUpdate()
        {
            var progress = AmbientMotion.Pop(Time.time, _period, _duration, _seed);
            var kind = progress < 0f ? -1 : AmbientMotion.PopKind(Time.time, _period, _seed, _icons.Length);
            Show(kind);
            if (kind < 0)
                return;
            var icon = _icons[kind].transform;
            // In with a little overshoot, up, out at the end.
            var grow = progress < 0.15f ? Mathf.Sin(progress / 0.15f * Mathf.PI * 0.6f) / Mathf.Sin(Mathf.PI * 0.6f) : 1f;
            var shrink = progress > 0.75f ? 1f - (progress - 0.75f) / 0.25f : 1f;
            icon.localPosition = Vector3.up * (_rise * Mathf.Sqrt(progress));
            icon.localScale = Vector3.one * (_size * Mathf.Max(0f, grow * shrink));
            if (_camera == null)
                _camera = Camera.main;
            if (_camera != null)
                icon.rotation = _camera.transform.rotation;
        }

        private void Show(int kind)
        {
            if (kind == _showing)
                return;
            _showing = kind;
            for (var i = 0; i < _icons.Length; i++)
                if (_icons[i] != null)
                    _icons[i].SetActive(i == kind);
        }
    }
}
