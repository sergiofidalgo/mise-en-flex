using System;
using Game.Simulation.Movement;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A table in the dining room, served by carrying plates to it, or across the wall if it's against
    /// it. A bar table has one chair, included. A regular table has up to one on each side, which the
    /// player chooses: its biggest table sets the biggest group that comes in. Chairs stay clear of
    /// walls, so a table against one has a side less.
    /// </summary>
    public sealed class TableStation : ServingStation
    {
        [Serializable]
        public sealed class Chair
        {
            [SerializeField] private Transform _seat;
            [SerializeField] private Transform _plateAnchor;

            public Transform Seat => _seat;
            public Transform PlateAnchor => _plateAnchor;
        }

        [Tooltip("Its chairs, in the order they're added. One: it isn't up to the player.")]
        [SerializeField] private Chair[] _chairs = Array.Empty<Chair>();

        [Tooltip("Chairs it has before anyone changes them: 1 for one bought in the shop, more for a table a restaurant comes with (Casa Lucía's have 4).")]
        [SerializeField, Min(1)] private int _startingChairs = 1;

        private int _chairCount = 1;
        private bool _chairsChosen;

        public override bool BelongsInDiningRoom => true;

        public int ChairCount => _chairCount;

        /// <summary>True if the player can choose how many chairs it has.</summary>
        public bool HasChoiceOfChairs => _chairs.Length > 1;

        public override void Bind(KitchenServices services)
        {
            base.Bind(services);
            if (!_chairsChosen)
            {
                _chairCount = Math.Max(1, Math.Min(_startingChairs, Math.Max(1, _chairs.Length)));
                _chairsChosen = true;
            }
            ArrangeChairs();
        }

        /// <summary>How many of its chairs would have room, at this position and angle.</summary>
        public int RoomForChairs(Footprint at, FloorPlan building)
        {
            var room = 0;
            foreach (var chair in _chairs)
            {
                if (building.CanStand(SeatAt(chair, at)))
                    room++;
            }
            return room;
        }

        public bool CanAddChair =>
            HasChoiceOfChairs && _chairCount < RoomForChairs(Footprint, Services.Building);

        public void AddChair()
        {
            _chairCount++;
            ArrangeChairs();
        }

        /// <summary>Its chairs as they were saved.</summary>
        public void RestoreChairs(int count)
        {
            _chairCount = System.Math.Max(1, count);
            _chairsChosen = true;
            ArrangeChairs();
        }

        public void RemoveChair()
        {
            _chairCount--;
            ArrangeChairs();
        }

        /// <summary>Puts its chairs on the sides with room: as it's placed, and as chairs change.</summary>
        public void ArrangeChairs()
        {
            while (Places.Count < _chairCount)
                AddPlace(null, null);
            while (Places.Count > _chairCount)
                RemoveLastPlace();

            var used = 0;
            foreach (var chair in _chairs)
            {
                var use = used < _chairCount && Services.Building.CanStand(SeatAt(chair, Footprint));
                chair.Seat.gameObject.SetActive(use);
                if (!use)
                    continue;
                Places[used].Seat = chair.Seat;
                Places[used].PlateAnchor = chair.PlateAnchor;
                used++;
            }
        }

        /// <summary>
        /// While it's carried: shows only the chairs that would have room where it would land, so a
        /// chair never shows inside a wall (decisions 2026-11-11). Placing arranges them for real.
        /// </summary>
        public void PreviewChairs(Footprint at, FloorPlan building)
        {
            var used = 0;
            foreach (var chair in _chairs)
            {
                var show = used < _chairCount && building.CanStand(SeatAt(chair, at));
                chair.Seat.gameObject.SetActive(show);
                if (show)
                    used++;
            }
        }

        private static System.Numerics.Vector2 SeatAt(Chair chair, Footprint at)
        {
            var local = chair.Seat.localPosition;
            return at.Centre + at.Direction(new System.Numerics.Vector2(local.x, local.z));
        }
    }
}
