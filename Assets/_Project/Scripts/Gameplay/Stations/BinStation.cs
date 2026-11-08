using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Movement;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// The bin: discards single items, and scrapes food off held plates and out of held pans.
    /// Discarded food stops being simulated and its view is removed. The bin is smaller than its
    /// floor slot and stands snug against whatever is beside it, walls or stations, wherever it's
    /// put (<see cref="SnugFit"/>, the user's note 2026-11-08).
    /// </summary>
    public sealed class BinStation : Interactable
    {
        [Tooltip("What slides within the slot to sit snug: the parent of the bin's model.")]
        [SerializeField] private Transform _snug;

        [Tooltip("How far, in metres, the bin can move from its slot's centre towards each side.")]
        [SerializeField, Min(0f)] private float _slack = 0.2f;

        [Tooltip("How fast it slides into place after something beside it moves, in metres per second.")]
        [SerializeField, Min(0f)] private float _slideSpeed = 2f;

        private readonly ItemSink _sink = new ItemSink();
        private readonly List<Footprint> _neighbours = new List<Footprint>();
        private KitchenServices _services;

        public override IItemContainer Container => _sink;

        public ItemSink Sink => _sink;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            _sink.Discarded += HandleDiscarded;
            if (_snug != null)
                _snug.position = transform.position + SnugOffset(); // in place from the start
        }

        private void OnDestroy()
        {
            _sink.Discarded -= HandleDiscarded;
        }

        // Every frame, since anything beside it can be moved in the layout editor; a handful of
        // stations, so it's cheap.
        private void LateUpdate()
        {
            if (_snug == null || _services == null)
                return;
            var target = transform.position + SnugOffset();
            _snug.position = Vector3.MoveTowards(_snug.position, target, _slideSpeed * Time.deltaTime);
        }

        private Vector3 SnugOffset()
        {
            _neighbours.Clear();
            foreach (var station in _services.Stations.All)
            {
                if (station != this && station != null)
                    _neighbours.Add(station.Footprint);
            }
            var building = _services.Building;
            var offset = SnugFit.Offset(building, Footprint.Centre, building.StationHalfSize, _slack, _neighbours);
            return new Vector3(offset.X, 0f, offset.Y);
        }

        private void HandleDiscarded(Item item)
        {
            if (item is Ingredient ingredient)
                _services.Cooking.Remove(ingredient);
            _services.ItemViews.Release(item);
        }
    }
}
