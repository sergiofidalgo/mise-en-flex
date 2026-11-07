using Game.Simulation.Cooking;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// The kitchen's equipment settings: each kind of heat, the pan, plates, containers and trays.
    /// The name is from when tiers were planned; kinds of heat replaced them (decision 2026-11-07).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Equipment Tier", fileName = "EquipmentTier")]
    public sealed class EquipmentTier : ScriptableObject
    {
        [Header("Hob")]
        [Tooltip("Watts at full dial. A domestic gas burner is around 2–3 kW.")]
        [SerializeField, Min(0f)] private float _hobMaxPower = 2000f;

        [Header("Induction hob")]
        [Tooltip("The pan's temperature on each step above off, °C, lowest first. Three steps (decision 2026-11-07).")]
        [SerializeField] private float[] _inductionSteps = { 130f, 190f, 250f };
        [Tooltip("W/K. How hard it pulls the pan to the step: the pan gets there in about 3 × pan heat capacity / this seconds of game time.")]
        [SerializeField, Min(1f)] private float _inductionResponse = 75f;
        [Tooltip("Watts it can put in or take out. High, so a change of step shows almost at once (the user's call).")]
        [SerializeField, Min(0f)] private float _inductionMaxPower = 6000f;

        [Header("Charcoal grill")]
        [Tooltip("°C of the hot zone's grate. Always lit (decision 2026-11-07).")]
        [SerializeField] private float _grillHotTemperature = 260f;
        [Tooltip("°C of the cool zone's grate: below browning, so food finishes there without more crust.")]
        [SerializeField] private float _grillCoolTemperature = 150f;
        [Tooltip("W/K. How hard a zone pulls a pan on it to its temperature.")]
        [SerializeField, Min(1f)] private float _grillResponse = 75f;
        [Tooltip("Watts a zone can put into a pan.")]
        [SerializeField, Min(0f)] private float _grillMaxPower = 6000f;

        [Header("Pan")]
        [Tooltip("J/K. Heavier pans take longer to preheat but drop less when food goes in.")]
        [SerializeField, Min(1f)] private float _panHeatCapacity = 750f;
        [Tooltip("W/K lost to air. Sets the top temperature: about ambient + power / this (≈ 280 °C).")]
        [SerializeField, Min(0.01f)] private float _panAirLoss = 7.7f;
        [Tooltip("Ingredients that fit in the pan at once. 1 by design (decision 2026-11-02).")]
        [SerializeField, Min(1)] private int _panCapacity = 1;

        [Header("Plate")]
        [Tooltip("Food items that fit on a plate (e.g. steak, onions, a garnish).")]
        [SerializeField, Min(1)] private int _plateCapacity = 3;
        [Tooltip("Plates the kitchen owns. Dirty and broken ones aren't available, so this sets the pressure to wash up.")]
        [SerializeField, Min(0)] private int _plateCount = 6;

        [Header("Washing up")]
        [Tooltip("Dirty plates the pile holds before plates have to stay where they are.")]
        [SerializeField, Min(1)] private int _dirtyPileCapacity = 6;
        [Tooltip("Clean plates the stack holds.")]
        [SerializeField, Min(1)] private int _cleanPileCapacity = 6;
        [Tooltip("Plates the sink holds at once, dirty and clean together.")]
        [SerializeField, Min(1)] private int _sinkCapacity = 2;

        [Header("Containers")]
        [SerializeField, Min(0)] private int _containerCount = 6;
        [Tooltip("Portions of one ingredient a container holds.")]
        [SerializeField, Min(1)] private int _containerCapacity = 6;
        [Tooltip("How fast food ages in one, compared to in the open. Should beat room temperature but not a fridge.")]
        [SerializeField, Min(0f)] private float _containerFreshnessFactor = 0.5f;

        [Header("Trays")]
        [SerializeField, Min(0)] private int _trayCount = 4;
        [Tooltip("Things a tray holds: ingredients or plates, in any mix.")]
        [SerializeField, Min(1)] private int _trayCapacity = 4;
        [Tooltip("How fast food loose on one ages, compared to in the open.")]
        [SerializeField, Min(0f)] private float _trayFreshnessFactor = 1.5f;

        public int PlateCapacity => _plateCapacity;

        public int PlateCount => _plateCount;

        public int DirtyPileCapacity => _dirtyPileCapacity;
        public int CleanPileCapacity => _cleanPileCapacity;
        public int SinkCapacity => _sinkCapacity;
        public int ContainerCount => _containerCount;
        public int ContainerCapacity => _containerCapacity;
        public float ContainerFreshnessFactor => _containerFreshnessFactor;
        public int TrayCount => _trayCount;
        public int TrayCapacity => _trayCapacity;
        public float TrayFreshnessFactor => _trayFreshnessFactor;

        public HobProperties Hob => new HobProperties { MaxPower = _hobMaxPower };

        public HobProperties InductionHob => new HobProperties
        {
            Kind = HeatKind.Induction,
            MaxPower = _inductionMaxPower,
            StepTemperatures = (float[])_inductionSteps.Clone(),
            Response = _inductionResponse,
        };

        /// <summary>A grill zone, as a heat source for a pan put on it.</summary>
        public HobProperties GrillZone(bool hot) => new HobProperties
        {
            Kind = HeatKind.Fixed,
            MaxPower = _grillMaxPower,
            FixedTemperature = hot ? _grillHotTemperature : _grillCoolTemperature,
            Response = _grillResponse,
        };

        public float GrillTemperature(bool hot) => hot ? _grillHotTemperature : _grillCoolTemperature;

        /// <summary>The heat source a hob of this kind gets.</summary>
        public HobProperties HobFor(HeatKind kind) => kind == HeatKind.Induction ? InductionHob : Hob;

        public PanProperties Pan => new PanProperties
        {
            HeatCapacity = _panHeatCapacity,
            AirLossConductance = _panAirLoss,
            Capacity = _panCapacity,
        };
    }
}
