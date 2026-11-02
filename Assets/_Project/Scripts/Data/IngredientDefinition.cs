using System;
using System.Linq;
using Game.Simulation.Cooking;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Everything designers tune about one ingredient. Defaults describe a 300 g, 2.5 cm steak:
    /// in a preheated pan at full heat it reaches medium-rare at about 3 minutes per side plus
    /// a rest (in-game time; ~6–8 real seconds at 60x).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Ingredient Definition", fileName = "Ingredient")]
    public sealed class IngredientDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Doneness
        {
            [SerializeField] private string _name;
            [Tooltip("°C the core must have reached.")]
            [SerializeField] private float _minPeakCoreTemperature;

            public string Name => _name;
            public float MinPeakCoreTemperature => _minPeakCoreTemperature;
        }

        [Tooltip("Id used by the simulation and the item catalog, e.g. \"Steak\".")]
        [SerializeField] private string _typeId;
        [SerializeField] private GameObject _prefab;

        [Header("Heat")]
        [SerializeField, Min(0.001f)] private float _massKg = 0.3f;
        [Tooltip("J/(kg·K). Meat ≈ 3500, vegetables ≈ 3900.")]
        [SerializeField, Min(1f)] private float _specificHeat = 3500f;
        [Tooltip("Share of the heat capacity in each surface layer; the core gets the rest.")]
        [SerializeField, Range(0.01f, 0.45f)] private float _surfaceFraction = 0.15f;
        [Tooltip("W/K between each surface and the core. Lower = core lags further behind (thicker cut).")]
        [SerializeField, Min(0f)] private float _surfaceCoreConductance = 0.9f;
        [Tooltip("W/K between the pan and the surface lying in it.")]
        [SerializeField, Min(0f)] private float _panContactConductance = 6f;
        [Tooltip("W/K between an exposed surface and the air.")]
        [SerializeField, Min(0f)] private float _airConductance = 0.25f;
        [Tooltip("W/K of evaporative cooling per °C above boiling. Keeps wet surfaces near 100 °C.")]
        [SerializeField, Min(0f)] private float _evaporationConductance = 25f;

        [Header("Browning")]
        [Tooltip("°C of the pan above which browning starts.")]
        [SerializeField] private float _browningStartTemperature = 140f;
        [Tooltip("Browning per °C above the start per in-game second. 1/12600 gives a good crust in ~3 min at a 210 °C pan.")]
        [SerializeField, Min(0f)] private float _browningRate = 1f / 12600f;
        [Tooltip("Browning at which a side counts as burnt (around 1 is a good crust).")]
        [SerializeField, Min(0f)] private float _burntBrowning = 2f;

        [Header("Doneness (ascending)")]
        [SerializeField] private Doneness[] _donenessLevels = Array.Empty<Doneness>();

        [Header("Storage")]
        [Tooltip("°C when taken out of storage (fridge ≈ 4).")]
        [SerializeField] private float _startTemperature = 4f;
        [Tooltip("Freshness lost per in-game hour at 20 °C (1/24 = spoiled in a day at room temperature).")]
        [SerializeField, Min(0f)] private float _freshnessLossPerHourAt20C = 1f / 24f;

        [Header("Serving (scored at the pass)")]
        [Tooltip("Score doneness against the target core temperature below.")]
        [SerializeField] private bool _scoreDoneness;
        [Tooltip("Peak core °C the kitchen aims for. Placeholder house standard (medium-rare ≈ 54.5) until orders in Phase 2.")]
        [SerializeField] private float _targetCoreTemperature = 54.5f;
        [Tooltip("Score the crust (browning).")]
        [SerializeField] private bool _scoreBrowning;
        [Tooltip("Should reach the table hot: serving temperature is scored.")]
        [SerializeField] private bool _servedHot;
        [Tooltip("Average °C at or above which serving temperature scores full marks.")]
        [SerializeField] private float _servingIdealTemperature = 52f;
        [Tooltip("Average °C at or below which serving temperature scores nothing.")]
        [SerializeField] private float _servingColdTemperature = 38f;
        [Tooltip("In-game seconds of rest off the pan for full marks (steak ≈ 180); 0 = not scored.")]
        [SerializeField, Min(0f)] private float _restTargetSeconds;

        [Header("Cutting (prep counter)")]
        [Tooltip("Type id this becomes when cut (e.g. DicedOnion), or empty if it isn't cut.")]
        [SerializeField] private string _cutIntoTypeId;
        [Tooltip("Knife chops to finish the cut.")]
        [SerializeField, Min(0)] private int _cutsNeeded;
        [Tooltip("Real seconds for the cutting dial's needle to go once round.")]
        [SerializeField, Min(0.05f)] private float _chopSecondsPerTurn = 0.7f;
        [Tooltip("Centre of the sweet spot, degrees clockwise from the top of the dial. Each ingredient has its own.")]
        [SerializeField, Range(0f, 359f)] private float _sweetSpotAngle = 90f;
        [Tooltip("Degrees either side of the centre for a Good cut.")]
        [SerializeField, Min(0f)] private float _sweetSpotGood = 20f;
        [Tooltip("Degrees either side for an Early/Late cut; beyond is a Miss.")]
        [SerializeField, Min(0f)] private float _sweetSpotOk = 40f;

        public string TypeId => _typeId;
        public GameObject Prefab => _prefab;

        public IngredientProperties ToProperties() => new IngredientProperties
        {
            TypeId = _typeId,
            HeatCapacity = _massKg * _specificHeat,
            SurfaceFraction = _surfaceFraction,
            SurfaceCoreConductance = _surfaceCoreConductance,
            PanContactConductance = _panContactConductance,
            AirConductance = _airConductance,
            EvaporationConductance = _evaporationConductance,
            BrowningStartTemperature = _browningStartTemperature,
            BrowningRate = _browningRate,
            BurntBrowning = _burntBrowning,
            DonenessLevels = _donenessLevels
                .Select(d => new DonenessLevel(d.Name, d.MinPeakCoreTemperature))
                .ToArray(),
            StartTemperature = _startTemperature,
            FreshnessLossPerHourAt20C = _freshnessLossPerHourAt20C,
            TargetCoreTemperature = _scoreDoneness ? _targetCoreTemperature : (float?)null,
            ScoresBrowning = _scoreBrowning,
            ServedHot = _servedHot,
            ServingIdealTemperature = _servingIdealTemperature,
            ServingColdTemperature = _servingColdTemperature,
            RestTargetSeconds = _restTargetSeconds,
            CutIntoTypeId = string.IsNullOrEmpty(_cutIntoTypeId) ? null : _cutIntoTypeId,
            CutsNeeded = _cutsNeeded,
            ChopSecondsPerTurn = _chopSecondsPerTurn,
            SweetSpotAngle = _sweetSpotAngle,
            SweetSpotGoodHalfWidth = _sweetSpotGood,
            SweetSpotOkHalfWidth = _sweetSpotOk,
        };
    }
}
