using Game.Simulation.Interaction;
using Game.Simulation.Movement;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Game-feel values designers tune. Gameplay reads these every frame, so edits
    /// in the Inspector apply immediately during Play mode.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Tuning Config", fileName = "TuningConfig")]
    public sealed class TuningConfig : ScriptableObject
    {
        [Header("Time")]
        [Tooltip("In-game seconds per real second. 60 = one in-game minute per real second (decision 2026-11-02).")]
        [SerializeField, Min(0f)] private float _timeScale = 60f;

        [Header("Kitchen")]
        [Tooltip("Kitchen air temperature, °C.")]
        [SerializeField] private float _ambientTemperature = 22f;

        [Header("Player movement")]
        [Tooltip("Metres per second at full stick. The greybox kitchen is 10 m wide.")]
        [SerializeField, Min(0f)] private float _moveSpeed = 5f;

        [Tooltip("Seconds from standstill to full speed. Decision 2026-11-02: about 0.08 (snappy).")]
        [SerializeField, Min(0f)] private float _accelerationTime = 0.08f;

        [Tooltip("Seconds from full speed to a stop after letting go of the stick.")]
        [SerializeField, Min(0f)] private float _decelerationTime = 0.06f;

        [Tooltip("Degrees per second the chef turns to face the stick. 1080 = a half turn in ~0.17 s.")]
        [SerializeField, Min(0f)] private float _turnSpeedDegrees = 1080f;

        [Header("Interaction")]
        [Tooltip("Metres from the chef to a station's centre. A counter is 1 m deep, so 1.5 reaches about 0.65 m past its front edge.")]
        [SerializeField, Min(0f)] private float _interactRange = 1.5f;

        [Tooltip("Degrees either side of facing that still count as 'in front'. Wide = forgiving.")]
        [SerializeField, Range(0f, 180f)] private float _interactHalfAngle = 75f;

        [Tooltip("How strongly the current target is preferred over a near-tie, so the highlight doesn't flicker.")]
        [SerializeField, Min(0f)] private float _targetStickiness = 0.2f;

        [Header("Item motion")]
        [Tooltip("Seconds for an item to fly between hands and a counter.")]
        [SerializeField, Min(0f)] private float _itemMoveDuration = 0.12f;

        [Tooltip("Metres the item arcs upwards mid-flight.")]
        [SerializeField, Min(0f)] private float _itemArcHeight = 0.25f;

        [Tooltip("Extra scale at mid-flight (0.15 = 15% bigger), for a small 'pop'.")]
        [SerializeField, Min(0f)] private float _itemPop = 0.15f;

        [Header("Close-up")]
        [Tooltip("Hob dial change per second at full trigger. 0.8 = off to full in 1.25 s.")]
        [SerializeField, Min(0f)] private float _heatDialSpeed = 0.8f;

        [Tooltip("Seconds for the close-up panel to grow out of the station (and shrink back).")]
        [SerializeField, Min(0.01f)] private float _closeUpTransition = 0.25f;

        [Tooltip("Close-up panel width as a fraction of the screen width (the panel is square).")]
        [SerializeField, Range(0.1f, 0.6f)] private float _closeUpPanelSize = 0.3f;

        [Tooltip("Pixels kept between the panel and the station / screen edges.")]
        [SerializeField, Min(0f)] private float _closeUpPanelMargin = 24f;

        [Tooltip("Panel opacity while the other player is behind it, so both can still see.")]
        [SerializeField, Range(0f, 1f)] private float _closeUpFadedOpacity = 0.35f;

        [Tooltip("Close-up render texture size in pixels. Higher is sharper but costs frame time.")]
        [SerializeField, Min(64)] private int _closeUpTextureSize = 512;

        public float HeatDialSpeed => _heatDialSpeed;
        public float CloseUpTransition => _closeUpTransition;
        public float CloseUpPanelSize => _closeUpPanelSize;
        public float CloseUpPanelMargin => _closeUpPanelMargin;
        public float CloseUpFadedOpacity => _closeUpFadedOpacity;
        public int CloseUpTextureSize => _closeUpTextureSize;

        public float TimeScale => _timeScale;

        public float AmbientTemperature => _ambientTemperature;

        public MovementSettings Movement =>
            new MovementSettings(_moveSpeed, _accelerationTime, _decelerationTime, _turnSpeedDegrees);

        public TargetingSettings Targeting =>
            new TargetingSettings(_interactRange, _interactHalfAngle, _targetStickiness);

        public ItemMoveSettings ItemMove =>
            new ItemMoveSettings(_itemMoveDuration, _itemArcHeight, _itemPop);
    }
}
