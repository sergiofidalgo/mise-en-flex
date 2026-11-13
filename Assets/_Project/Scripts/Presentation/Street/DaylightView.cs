using Game.Gameplay;
using Game.Simulation.Days;
using Game.Simulation.Street;
using UnityEngine;

namespace Game.Presentation.Street
{
    /// <summary>
    /// Lights the scene by the service's clock (the user's request, 2026-11-12): a lunch plays in
    /// daylight, a dinner at night, and an all-day service goes from afternoon through golden hour
    /// and dusk into night (<see cref="Daylight"/>). The toon shaders multiply the sun and sky by
    /// one colour outdoors and another inside the building's walls, which this sets as globals;
    /// lamps are untouched, so the street's and the kitchen's own lights carry the night. Before
    /// the doors open it's the opening hour; after they close, the hour they closed at.
    /// </summary>
    public sealed class DaylightView : MonoBehaviour
    {
        private static readonly int OnId = Shader.PropertyToID("_GameDaylightOn");
        private static readonly int OutdoorsId = Shader.PropertyToID("_GameDaylight");
        private static readonly int IndoorsId = Shader.PropertyToID("_GameIndoorLight");
        private static readonly int BoxId = Shader.PropertyToID("_GameIndoorBox");

        /// <summary>
        /// An hour to show instead of the clock's, for stills and for judging the look: everything
        /// that goes by the clock out here (this, the street's lamps) follows it. Null for the clock.
        /// </summary>
        public static float? Preview { get; set; }

        [SerializeField] private KitchenCompositionRoot _root;
        [Tooltip("The sun: its shadows soften at night.")]
        [SerializeField] private Light _sun;
        [Tooltip("The building's outer walls on the ground (min X, min Z) and (max X, max Z): inside them the restaurant's own light holds.")]
        [SerializeField] private Vector2 _buildingMin;
        [SerializeField] private Vector2 _buildingMax;
        [Tooltip("Seconds the light takes to follow a jump in the hour (a new day's morning after a night).")]
        [SerializeField, Min(0.01f)] private float _ease = 1.5f;
        [Tooltip("How strong the sun's shadows are at night, against their strength by day.")]
        [SerializeField, Range(0f, 1f)] private float _nightShadows = 0.5f;

        [Header("To judge the look in the editor")]
        [Tooltip("Show the hour below instead of the clock's.")]
        [SerializeField] private bool _fixHour;
        [SerializeField, Range(0f, 24f)] private float _hour = 21f;

        private Vector3 _outdoors = Vector3.one;
        private Vector3 _indoors = Vector3.one;
        private float _dayShadows;
        private bool _started;

        /// <summary>The hour on the clock now, for the kitchen these services run (or the preview's).</summary>
        public static float ClockHour(KitchenServices services)
        {
            if (Preview.HasValue)
                return Preview.Value;
            if (services == null || services.Service == null)
                return 12f;
            // Before the doors open it's the opening hour; from then on the service's own clock,
            // which stays where it stopped through closing and the review.
            var phase = services.Days.Phase;
            var elapsed = phase == DayPhase.Plan || phase == DayPhase.Prep ? 0f : services.Service.Elapsed;
            return services.Service.Settings.Clock.HourAt(elapsed);
        }

        private void Awake() => _dayShadows = _sun != null ? _sun.shadowStrength : 0f;

        private void OnDisable() => Shader.SetGlobalFloat(OnId, 0f);

        private void Update()
        {
            var hour = _fixHour ? _hour : ClockHour(_root != null ? _root.Services : null);
            var outdoors = ToUnity(Daylight.At(hour));
            var indoors = ToUnity(Daylight.Indoors(hour));
            // The first frame is simply at its hour; after that it eases, so a new morning doesn't pop.
            var blend = _started ? 1f - Mathf.Exp(-Time.unscaledDeltaTime / _ease) : 1f;
            _started = true;
            _outdoors = Vector3.Lerp(_outdoors, outdoors, blend);
            _indoors = Vector3.Lerp(_indoors, indoors, blend);

            Shader.SetGlobalFloat(OnId, 1f);
            Shader.SetGlobalVector(OutdoorsId, _outdoors);
            Shader.SetGlobalVector(IndoorsId, _indoors);
            Shader.SetGlobalVector(BoxId, new Vector4(_buildingMin.x, _buildingMin.y, _buildingMax.x, _buildingMax.y));
            if (_sun != null)
                _sun.shadowStrength = _dayShadows * Mathf.Lerp(1f, _nightShadows, Daylight.Nightfall(hour));
        }

        private static Vector3 ToUnity(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
