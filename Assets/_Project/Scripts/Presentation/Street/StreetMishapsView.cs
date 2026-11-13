using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Stations;
using Game.Gameplay.Street;
using TMPro;
using UnityEngine;

namespace Game.Presentation.Street
{
    /// <summary>
    /// What a hit and a fetching-back look and sound like: a car's horn before it reaches a chef,
    /// a thud as it does, stars circling their head while they lie there; and, when a bodyguard
    /// has set a wandering chef back on the doorstep, a line over their head saying why. The
    /// sounds are made here from sine waves and noise: placeholders until real ones are recorded.
    /// </summary>
    public sealed class StreetMishapsView : MonoBehaviour
    {
        [SerializeField] private HitAndRun _hits;
        [SerializeField] private SceneLeave _leaving;
        [Tooltip("What the stars are made of: something bright and unlit.")]
        [SerializeField] private Material _star;
        [SerializeField] private TMP_FontAsset _font;
        [Tooltip("What a returned chef is told, in English; one is picked each time, never the same twice running. Translated by the language tables.")]
        [SerializeField] private string[] _lines =
        {
            "The boss says the kitchen is that way.",
            "Shift's not over, chef.",
            "Nice try.",
            "Lost? The stove's inside.",
            "No breaks until the till closes.",
        };
        [Tooltip("Seconds a line stays up.")]
        [SerializeField, Min(0.5f)] private float _lineSeconds = 3f;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.5f;

        private const int SampleRate = 22050;

        private sealed class Stars
        {
            public Transform Ring;
            public float Until;
        }

        private sealed class Line
        {
            public Transform Root;
            public Transform Chef;
            public float Since;
        }

        private readonly Dictionary<PlayerInteractor, Stars> _stars = new Dictionary<PlayerInteractor, Stars>();
        private readonly List<Line> _shown = new List<Line>();
        private AudioSource _speaker;
        private AudioClip _horn;
        private AudioClip _thud;
        private AudioClip _ding;
        private int _lastLine = -1;

        private void Awake()
        {
            _speaker = gameObject.AddComponent<AudioSource>();
            _speaker.playOnAwake = false;
            _speaker.spatialBlend = 0f;
            _horn = Horn();
            _thud = Thud();
            _ding = Ding();
        }

        private void OnEnable()
        {
            if (_hits != null)
            {
                _hits.Horn += HandleHorn;
                _hits.Struck += HandleStruck;
                _hits.Landed += HandleLanded;
                _hits.Recovered += HandleRecovered;
            }
            if (_leaving != null)
                _leaving.Returned += HandleReturned;
        }

        private void OnDisable()
        {
            if (_hits != null)
            {
                _hits.Horn -= HandleHorn;
                _hits.Struck -= HandleStruck;
                _hits.Landed -= HandleLanded;
                _hits.Recovered -= HandleRecovered;
            }
            if (_leaving != null)
                _leaving.Returned -= HandleReturned;
        }

        private void HandleHorn(RoadVehicle car) => _speaker.PlayOneShot(_horn, _volume);

        private void HandleStruck(PlayerInteractor chef, RoadVehicle car) => _speaker.PlayOneShot(_thud, _volume);

        // Three stars circling over their head for as long as they're dazed.
        private void HandleLanded(PlayerInteractor chef)
        {
            HandleRecovered(chef);
            var ring = new GameObject("DazedStars").transform;
            ring.SetParent(transform, false);
            for (var k = 0; k < 3; k++)
            {
                var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(star.GetComponent<Collider>());
                star.name = "Star";
                star.transform.SetParent(ring, false);
                var angle = k * 120f * Mathf.Deg2Rad;
                star.transform.localPosition = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.34f;
                star.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
                if (_star != null)
                    star.GetComponent<Renderer>().sharedMaterial = _star;
            }
            _stars[chef] = new Stars { Ring = ring, Until = Time.time + (_hits != null ? _hits.DazedSeconds : 1.5f) + 0.4f };
        }

        private void HandleRecovered(PlayerInteractor chef)
        {
            if (!_stars.TryGetValue(chef, out var stars))
                return;
            if (stars.Ring != null)
                Destroy(stars.Ring.gameObject);
            _stars.Remove(chef);
        }

        private void HandleReturned(PlayerInteractor chef)
        {
            _speaker.PlayOneShot(_ding, _volume);
            if (_lines.Length == 0)
                return;
            var pick = Random.Range(0, _lines.Length);
            if (pick == _lastLine)
                pick = (pick + 1) % _lines.Length;
            _lastLine = pick;

            var root = new GameObject("SentBackLine").transform;
            root.SetParent(transform, false);
            var label = new GameObject("Text").AddComponent<TextMeshPro>();
            label.transform.SetParent(root, false);
            if (_font != null)
                label.font = _font;
            label.text = Tr.Get(_lines[pick]);
            label.fontSize = 2.6f;
            label.color = new Color(1f, 0.97f, 0.9f);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.rectTransform.sizeDelta = new Vector2(4.2f, 1.4f);
            // A dark edge, so it reads over anything behind it at any hour.
            label.outlineColor = new Color32(40, 26, 18, 255);
            label.outlineWidth = 0.28f;
            label.sortingOrder = 50;
            _shown.Add(new Line { Root = root, Chef = chef.transform, Since = Time.time });
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            foreach (var pair in _stars)
            {
                var chef = pair.Key;
                var ring = pair.Value.Ring;
                if (chef == null || ring == null)
                    continue;
                // Over their head wherever it has ended up (they're lying down), turning.
                ring.position = chef.transform.position + chef.transform.up * 1.75f + Vector3.up * 0.25f;
                ring.rotation = Quaternion.Euler(0f, Time.time * 260f, 0f);
            }
            for (var i = _shown.Count - 1; i >= 0; i--)
            {
                var line = _shown[i];
                var age = Time.time - line.Since;
                if (age >= _lineSeconds || line.Chef == null)
                {
                    if (line.Root != null)
                        Destroy(line.Root.gameObject);
                    _shown.RemoveAt(i);
                    continue;
                }
                // Pops up over their head, follows them, faces the camera, shrinks away at the end.
                var grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.18f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_lineSeconds - age) / 0.25f));
                line.Root.position = line.Chef.position + Vector3.up * 2.55f;
                line.Root.localScale = Vector3.one * grow;
                if (camera != null)
                    line.Root.rotation = camera.transform.rotation;
            }
        }

        // ---- Placeholder sounds ----------------------------------------------------------------------

        // Two notes together, a little rough: a small car's horn, twice.
        private static AudioClip Horn()
        {
            return Make("StreetHorn", 0.62f, t =>
            {
                var beep = t < 0.22f || (t > 0.3f && t < 0.6f) ? 1f : 0f;
                var edge = Mathf.Clamp01(Mathf.Min(t, 0.62f - t) / 0.01f);
                var tone = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 415f * t)) * 0.5f + Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 523f * t)) * 0.5f;
                return tone * 0.45f * beep * edge;
            });
        }

        // A low thump with a slap of noise on top.
        private static AudioClip Thud()
        {
            var random = new System.Random(7);
            return Make("StreetThud", 0.3f, t =>
            {
                var low = Mathf.Sin(2f * Mathf.PI * (110f - 160f * t) * t) * Mathf.Exp(-t * 14f);
                var slap = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 55f) * 0.6f;
                return (low + slap) * 0.9f;
            });
        }

        // A small bell: set down, back to work.
        private static AudioClip Ding()
        {
            return Make("StreetDing", 0.7f, t =>
                (Mathf.Sin(2f * Mathf.PI * 1320f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 1980f * t)) * Mathf.Exp(-t * 6f) * 0.4f);
        }

        private static AudioClip Make(string name, float seconds, System.Func<float, float> wave)
        {
            var samples = new float[Mathf.CeilToInt(seconds * SampleRate)];
            for (var i = 0; i < samples.Length; i++)
                samples[i] = Mathf.Clamp(wave(i / (float)SampleRate), -1f, 1f);
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
