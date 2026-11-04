using Game.Simulation.Cooking;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Kitchen equipment quality. Phase 1 has one tier; better hobs and pans come with Phase 4.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Equipment Tier", fileName = "EquipmentTier")]
    public sealed class EquipmentTier : ScriptableObject
    {
        [Header("Hob")]
        [Tooltip("Watts at full dial. A domestic gas burner is around 2–3 kW.")]
        [SerializeField, Min(0f)] private float _hobMaxPower = 2000f;

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

        public PanProperties Pan => new PanProperties
        {
            HeatCapacity = _panHeatCapacity,
            AirLossConductance = _panAirLoss,
            Capacity = _panCapacity,
        };
    }
}
