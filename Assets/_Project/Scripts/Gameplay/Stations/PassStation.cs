using Game.Simulation.Items;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// The pass: where finished plates leave the kitchen, through the hatch in the wall between the
    /// rooms. With counter service (decision 2026-11-02) each pass counter faces one stool.
    /// </summary>
    public sealed class PassStation : ServingStation
    {
        [Tooltip("Which stool this is (0 = leftmost).")]
        [SerializeField, Min(0)] private int _seatIndex;

        [Tooltip("Where the customer sits.")]
        [SerializeField] private Transform _seat;

        public int SeatIndex => _seatIndex;

        /// <summary>For whoever clears up.</summary>
        public IDirtyPlateSource DirtyPlates => Places[0].Slot;

        public override void Bind(KitchenServices services)
        {
            base.Bind(services);
            AddPlace(_seat, ItemAnchor).SeatIndex = _seatIndex;
        }
    }
}
