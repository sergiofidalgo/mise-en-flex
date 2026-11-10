using Game.Simulation.Cleaning;
using Game.Simulation.Cooking;
using Game.Simulation.Interaction;
using Game.Simulation.Items;
using Game.Simulation.Movement;
using Game.Simulation.Prep;
using Game.Simulation.Quality;
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

        [Tooltip("How fast food and pans change, against the clock. The clock runs at its own pace; a steak has to be judgeable by eye.")]
        [SerializeField, Min(0.01f)] private float _cookingSpeed = 0.9f;

        [Header("Kitchen")]
        [Tooltip("Kitchen air temperature, °C.")]
        [SerializeField] private float _ambientTemperature = 22f;

        [Tooltip("Inside a fridge, °C.")]
        [SerializeField] private float _fridgeTemperature = 4f;

        [Header("Player movement")]
        [Tooltip("Metres per second at full stick. The greybox kitchen is 10 m wide.")]
        [SerializeField, Min(0f)] private float _moveSpeed = 4.2f;

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

        [Header("Quality (scored at the pass)")]
        [Tooltip("Doneness window at the most generous cooking precision (±°C around the target).")]
        [SerializeField, Min(0f)] private float _donenessToleranceGenerous = 5f;
        [Tooltip("Doneness window at the strictest cooking precision (±°C).")]
        [SerializeField, Min(0f)] private float _donenessToleranceStrict = 1.5f;
        [Tooltip("°C beyond the window over which the doneness score falls to 0.")]
        [SerializeField, Min(0.1f)] private float _donenessFalloff = 8f;
        [Tooltip("Relative weights of the score parts.")]
        [SerializeField, Min(0f)] private float _donenessWeight = 0.4f;
        [SerializeField, Min(0f)] private float _crustWeight = 0.25f;
        [SerializeField, Min(0f)] private float _temperatureWeight = 0.25f;
        [SerializeField, Min(0f)] private float _freshnessWeight = 0.1f;
        [SerializeField, Min(0f)] private float _cutWeight = 0.15f;
        [SerializeField, Min(0f)] private float _restWeight = 0.15f;

        [Header("Crust scoring")]
        [Tooltip("Average browning of both sides at or below which the crust scores nothing.")]
        [SerializeField, Min(0f)] private float _crustPale = 0.1f;
        [Tooltip("Average browning from which the crust scores full marks.")]
        [SerializeField, Min(0f)] private float _crustGoodMin = 0.6f;

        [Header("Overnight")]
        [Tooltip("In-game hours between closing and the next day. Food ages through them.")]
        [SerializeField, Min(0f)] private float _overnightHours = 18f;
        [Tooltip("Stored food below this freshness gets thrown out at closing.")]
        [SerializeField, Range(0f, 1f)] private float _spoiledBelow = 0.1f;

        [Header("Washing by hand")]
        [Tooltip("Acceleration at full stick: high, so small movements shift the point a lot.")]
        [SerializeField, Min(0f)] private float _washStick = 10f;
        [Tooltip("How hard the point runs away from the centre, per unit of distance.")]
        [SerializeField, Min(0f)] private float _washInstability = 3.2f;
        [Tooltip("How fast the point's speed dies away, per second. Higher means less inertia.")]
        [SerializeField, Min(0f)] private float _washDamping = 4.2f;
        [Tooltip("Size of the random nudge. Keep below instability × ok radius, or doing nothing could clean a plate.")]
        [SerializeField, Min(0f)] private float _washNudge = 1.0f;
        [Tooltip("Fraction of the circle: the target drawn in the centre to aim for.")]
        [SerializeField, Range(0f, 1f)] private float _washGoodRadius = 0.4f;
        [Tooltip("Fraction of the circle beyond which nothing gets clean; inside, the closer the quicker.")]
        [SerializeField, Range(0f, 1f)] private float _washOkRadius = 0.6f;
        [Tooltip("Seconds of full-speed scrubbing per plate; scrubbing slows the further the soap is from the centre.")]
        [SerializeField, Min(0.1f)] private float _washSecondsPerPlate = 2.4f;
        [Tooltip("Pushes per plate, one as each share of it comes clean (the user's call: 2, so each plate is tipped three ways).")]
        [SerializeField, Min(0)] private int _washCorrections = 2;
        [Tooltip("How hard each push kicks the point away (circle radii per second).")]
        [SerializeField, Min(0f)] private float _washPushSpeed = 3.5f;
        [Tooltip("Random spread, degrees either way, around pushes spaced evenly round the circle.")]
        [SerializeField, Range(0f, 60f)] private float _washPushSpread = 20f;

        [Header("Dirt (decision 2026-11-10: a medium mess; amounts in wipes, one loop of the cleaning game each)")]
        [Tooltip("Wipes a diner leaves on their table or stool when they finish.")]
        [SerializeField, Min(0f)] private float _dinerSmudge = 1f;
        [Tooltip("Wipes a plate dropped by a waiter or broken at the sink leaves on the floor.")]
        [SerializeField, Min(0f)] private float _plateSpill = 3f;
        [Tooltip("Wipes food burnt in a pan on the heat leaves on the counter beside the hob (or the floor).")]
        [SerializeField, Min(0f)] private float _burntScorch = 2f;
        [Tooltip("Guests through the dining room per floor scuff.")]
        [SerializeField, Min(1)] private int _guestsPerScuff = 6;
        [Tooltip("Wipes a floor scuff takes.")]
        [SerializeField, Min(0f)] private float _scuff = 1f;
        [Tooltip("Wipes the foam from a fire put out takes.")]
        [SerializeField, Min(0f)] private float _fireFoam = 2f;
        [Tooltip("Metres: new dirt this close to a spot on the same surface adds to that spot.")]
        [SerializeField, Min(0f)] private float _dirtMergeRadius = 0.35f;
        [Tooltip("The most wipes one spot can hold.")]
        [SerializeField, Min(0.1f)] private float _dirtMaxPerSpot = 4f;
        [Tooltip("Score (0..1) a guest loses for a full wipe of dirt on their own table.")]
        [SerializeField, Range(0f, 1f)] private float _dirtTablePenalty = 0.1f;
        [Tooltip("Score lost per wipe of floor dirt near the guest's seat.")]
        [SerializeField, Range(0f, 1f)] private float _dirtFloorPenalty = 0.04f;
        [Tooltip("Metres from a seat that floor dirt is noticed.")]
        [SerializeField, Min(0f)] private float _dirtFloorRadius = 1.5f;
        [Tooltip("The most dirt can take off a dish's score.")]
        [SerializeField, Range(0f, 1f)] private float _dirtMaxPenalty = 0.2f;
        [Tooltip("A penalty at least this big makes dirt what the guest's review is about, if nothing worse happened.")]
        [SerializeField, Range(0f, 1f)] private float _dirtNoticedFrom = 0.05f;

        [Header("Cleaning game (trace the shape with the stick)")]
        [Tooltip("Below this push the stick reads as centred, so drift doesn't count.")]
        [SerializeField, Range(0f, 1f)] private float _traceDeadZone = 0.15f;
        [Tooltip("How far the stick can be from the path and still pull the marker. Wide = forgiving.")]
        [SerializeField, Range(0.05f, 1f)] private float _traceTolerance = 0.4f;
        [Tooltip("How far ahead along the path (in loops) the marker can be pulled at once.")]
        [SerializeField, Range(0.02f, 0.5f)] private float _traceReach = 0.15f;
        [Tooltip("Stick units a second above which the stick is slammed rather than steered, and doesn't count.")]
        [SerializeField, Min(1f)] private float _traceMaxStickSpeed = 15f;
        [Tooltip("Fastest loops a second with the cloth (circles).")]
        [SerializeField, Min(0.05f)] private float _clothSpeed = 1.1f;
        [Tooltip("Fastest loops a second with the mop (figure-eights).")]
        [SerializeField, Min(0.05f)] private float _mopSpeed = 0.7f;
        [Tooltip("Fastest sweeps a second with the extinguisher.")]
        [SerializeField, Min(0.05f)] private float _extinguisherSpeed = 0.9f;
        [Tooltip("Metres from the chef that floor dirt can be mopped.")]
        [SerializeField, Min(0f)] private float _mopReach = 1.1f;

        [Header("Gas fires")]
        [Tooltip("Real seconds a pan stays on a lit flame with its food burnt before it catches (the alarm is the warning).")]
        [SerializeField, Min(0f)] private float _ignitionSeconds = 4f;
        [Tooltip("Extinguisher sweeps that put a fire out.")]
        [SerializeField, Min(0.1f)] private float _sweepsToPutOut = 3f;

        public DirtSettings Dirt => new DirtSettings
        {
            DinerSmudge = _dinerSmudge,
            PlateSpill = _plateSpill,
            BurntScorch = _burntScorch,
            GuestsPerScuff = _guestsPerScuff,
            Scuff = _scuff,
            FireFoam = _fireFoam,
            MergeRadius = _dirtMergeRadius,
            MaxPerSpot = _dirtMaxPerSpot,
            TablePenalty = _dirtTablePenalty,
            FloorPenalty = _dirtFloorPenalty,
            FloorRadius = _dirtFloorRadius,
            MaxPenalty = _dirtMaxPenalty,
            NoticedFrom = _dirtNoticedFrom,
        };

        public TraceGameSettings TraceGame => new TraceGameSettings
        {
            DeadZone = _traceDeadZone,
            Tolerance = _traceTolerance,
            Reach = _traceReach,
            MaxStickSpeed = _traceMaxStickSpeed,
            CircleSpeed = _clothSpeed,
            FigureEightSpeed = _mopSpeed,
            SweepSpeed = _extinguisherSpeed,
        };

        public FireSettings Fire => new FireSettings { IgnitionSeconds = _ignitionSeconds, SweepsToPutOut = _sweepsToPutOut };

        /// <summary>Metres from the chef that floor dirt can be mopped.</summary>
        public float MopReach => _mopReach;

        [Header("Flip gauge")]
        [Tooltip("Browning of the side facing down where the gauge's green band starts (around 1 is a good crust).")]
        [SerializeField, Min(0f)] private float _flipGoodFrom = 0.7f;
        [Tooltip("Browning where the green band ends.")]
        [SerializeField, Min(0f)] private float _flipGoodTo = 1.1f;
        [Tooltip("Browning of the side in the pan at which the bar warns it's about to burn.")]
        [SerializeField, Min(0f)] private float _burnWarningFrom = 1.5f;

        public float OvernightSeconds => _overnightHours * 3600f;

        public float SpoiledBelow => _spoiledBelow;

        public WashGameSettings WashGame => new WashGameSettings
        {
            StickAcceleration = _washStick,
            Instability = _washInstability,
            Damping = _washDamping,
            Nudge = _washNudge,
            GoodRadius = _washGoodRadius,
            OkRadius = _washOkRadius,
            SecondsPerPlate = _washSecondsPerPlate,
            Corrections = _washCorrections,
            PushSpeed = _washPushSpeed,
            PushSpreadDegrees = _washPushSpread,
        };

        public FlipGaugeSettings FlipGauge => new FlipGaugeSettings { GoodFrom = _flipGoodFrom, GoodTo = _flipGoodTo, WarnFrom = _burnWarningFrom };

        /// <summary>Scoring settings; <paramref name="cookingPrecision"/> (realism dial, 0..1) narrows the doneness window.</summary>
        public ScoringSettings Quality(float cookingPrecision) => new ScoringSettings
        {
            DonenessTolerance = Mathf.Lerp(_donenessToleranceGenerous, _donenessToleranceStrict, cookingPrecision),
            DonenessFalloff = _donenessFalloff,
            DonenessWeight = _donenessWeight,
            CrustWeight = _crustWeight,
            CrustPale = _crustPale,
            CrustGoodMin = _crustGoodMin,
            TemperatureWeight = _temperatureWeight,
            FreshnessWeight = _freshnessWeight,
            CutWeight = _cutWeight,
            RestWeight = _restWeight,
        };

        [Header("Item motion")]
        [Tooltip("Seconds for an item to fly between hands and a counter.")]
        [SerializeField, Min(0f)] private float _itemMoveDuration = 0.12f;

        [Tooltip("Metres the item arcs upwards mid-flight.")]
        [SerializeField, Min(0f)] private float _itemArcHeight = 0.25f;

        [Tooltip("Extra scale at mid-flight (0.15 = 15% bigger), for a small 'pop'.")]
        [SerializeField, Min(0f)] private float _itemPop = 0.15f;

        [Header("Cutting dial pacing")]
        [Tooltip("Needle speed multiplier after a Good cut (above 1: a streak speeds up).")]
        [SerializeField, Min(0.1f)] private float _chopSpeedAfterGood = 1.15f;
        [Tooltip("After an Early/Late cut (slightly below 1).")]
        [SerializeField, Min(0.1f)] private float _chopSpeedAfterOk = 0.95f;
        [Tooltip("After a Miss (well below 1).")]
        [SerializeField, Min(0.1f)] private float _chopSpeedAfterMiss = 0.75f;
        [Tooltip("Slowest the needle can get, relative to the ingredient's base speed.")]
        [SerializeField, Min(0.1f)] private float _chopSpeedMin = 0.5f;
        [Tooltip("Fastest the needle can get, relative to the ingredient's base speed.")]
        [SerializeField, Min(0.1f)] private float _chopSpeedMax = 2f;

        [Tooltip("Width of the cutting dial's arc in degrees; the needle sweeps it right to left (≈150–180).")]
        [SerializeField, Range(60f, 270f)] private float _chopSweepDegrees = 170f;

        public float ChopSweepDegrees => _chopSweepDegrees;

        public ChopPacing ChopPacing =>
            new ChopPacing(_chopSpeedAfterGood, _chopSpeedAfterOk, _chopSpeedAfterMiss, _chopSpeedMin, _chopSpeedMax);

        [Header("Hob")]
        [Tooltip("Hob dial change per second at full trigger. 0.8 = off to full in 1.25 s.")]
        [SerializeField, Min(0f)] private float _heatDialSpeed = 0.8f;

        public float HeatDialSpeed => _heatDialSpeed;

        public float TimeScale => _timeScale;

        public float CookingSpeed => _cookingSpeed;

        public float AmbientTemperature => _ambientTemperature;

        public float FridgeTemperature => _fridgeTemperature;

        public MovementSettings Movement =>
            new MovementSettings(_moveSpeed, _accelerationTime, _decelerationTime, _turnSpeedDegrees);

        public TargetingSettings Targeting =>
            new TargetingSettings(_interactRange, _interactHalfAngle, _targetStickiness);

        public ItemMoveSettings ItemMove =>
            new ItemMoveSettings(_itemMoveDuration, _itemArcHeight, _itemPop);
    }
}
