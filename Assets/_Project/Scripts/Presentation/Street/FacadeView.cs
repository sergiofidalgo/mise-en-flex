using Game.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Street
{
    /// <summary>
    /// The restaurant's front, above the cut walls (the user's request, 2026-11-12: "show some
    /// parts of the restaurant façade as players go out"): pilasters, a fascia with the name on it
    /// and a striped valance. Indoors the walls stay cut low, so the kitchen and the dining room are never
    /// hidden; once every chef is outside, the front rises out of the wall's top, and it sinks back
    /// as soon as one steps in. Cosmetic only.
    /// </summary>
    public sealed class FacadeView : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [Tooltip("What rises: its origin on top of the front wall, growing upwards from there.")]
        [SerializeField] private Transform _rise;
        [Tooltip("The lettering on the fascia.")]
        [SerializeField] private TMPro.TextMeshPro _name;
        [Tooltip("Seconds to rise, and to sink.")]
        [SerializeField, Min(0.01f)] private float _seconds = 0.4f;
        [Tooltip("Seconds every chef must have been outside before it rises, so crossing the doorway doesn't make it flap.")]
        [SerializeField, Min(0f)] private float _settle = 0.35f;

        private float _shown;
        private float _outsideFor;

        /// <summary>How far up it is, 0–1.</summary>
        public float Shown => _shown;

        private void Awake() => Apply();

        // The fascia carries the name of the restaurant being played, as its sign does.
        private void Start()
        {
            var services = _root != null ? _root.Services : null;
            if (services != null && _name != null)
                _name.text = services.RestaurantName;
        }

        private void Update()
        {
            _outsideFor = EveryoneOutside() ? _outsideFor + Time.deltaTime : 0f;
            var target = _outsideFor >= _settle ? 1f : 0f;
            if (Mathf.Approximately(_shown, target))
                return;
            _shown = Mathf.MoveTowards(_shown, target, Time.deltaTime / _seconds);
            Apply();
        }

        private bool EveryoneOutside()
        {
            var services = _root != null ? _root.Services : null;
            var players = PlayerInput.all;
            if (services == null || services.Building == null || players.Count == 0)
                return false;
            foreach (var player in players)
            {
                var at = player.transform.position;
                if (services.Building.IsIndoors(new System.Numerics.Vector2(at.x, at.z)))
                    return false;
            }
            return true;
        }

        private void Apply()
        {
            if (_rise == null)
                return;
            // Eased at both ends; gone altogether when it's down, so nothing shimmers on the wall's top.
            var eased = Mathf.SmoothStep(0f, 1f, _shown);
            if (_rise.gameObject.activeSelf != _shown > 0f)
                _rise.gameObject.SetActive(_shown > 0f);
            _rise.localScale = new Vector3(1f, Mathf.Max(eased, 0.001f), 1f);
        }
    }
}
