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
        [Tooltip("Ingredients that fit in the pan at once.")]
        [SerializeField, Min(1)] private int _panCapacity = 2;

        public HobProperties Hob => new HobProperties { MaxPower = _hobMaxPower };

        public PanProperties Pan => new PanProperties
        {
            HeatCapacity = _panHeatCapacity,
            AirLossConductance = _panAirLoss,
            Capacity = _panCapacity,
        };
    }
}
