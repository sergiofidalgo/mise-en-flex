using System.Collections.Generic;
using Game.Simulation.Movement;
using UnityEngine;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Data
{
    /// <summary>
    /// The building: a kitchen and a dining room side by side, with a low wall between them. Anything
    /// against the wall works from both sides, and a door lets people walk between the two.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Floor Plan", fileName = "FloorPlanConfig")]
    public sealed class FloorPlanConfig : ScriptableObject
    {
        [Tooltip("The corner of where someone's centre can be, clear of the outer walls (x, z).")]
        [SerializeField] private Vector2 _roomMin = new Vector2(-3.6f, -6.6f);
        [SerializeField] private Vector2 _roomMax = new Vector2(3.6f, 1.6f);

        [Tooltip("Walls inside the building, as x, z, width, depth. A door is a gap between two.")]
        [SerializeField] private Rect[] _walls =
        {
            new Rect(-4f, -3.1f, 0.5f, 0.2f),   // west wall to the kitchen door
            new Rect(-2.3f, -3.1f, 6.3f, 0.2f), // kitchen door to the east wall
        };

        [Tooltip("Where the kitchen door's hinge is, and how wide it is, along the wall.")]
        [SerializeField] private Vector2 _kitchenDoorHinge = new Vector2(-3.5f, -3f);
        [SerializeField, Min(0.1f)] private float _kitchenDoorWidth = 1.2f;

        [Tooltip("Where kitchen furniture's centres can go when square to the room.")]
        [SerializeField] private Vector2 _kitchenAreaMin = new Vector2(-3.4f, -2.4f);
        [SerializeField] private Vector2 _kitchenAreaMax = new Vector2(3.4f, 1.4f);

        [Tooltip("Where dining furniture's centres can go when square to the room.")]
        [SerializeField] private Vector2 _diningAreaMin = new Vector2(-3.4f, -6.4f);
        [SerializeField] private Vector2 _diningAreaMax = new Vector2(3.4f, -3.6f);

        [Tooltip("Where the street door stands on the dining room's outer walls: the south-west corner of that line.")]
        [SerializeField] private Vector2 _doorLineMin = new Vector2(-3.55f, -6.55f);
        [Tooltip("The other end of it: x is the east wall, y how far north the side walls go.")]
        [SerializeField] private Vector2 _doorLineMax = new Vector2(3.55f, -3.7f);
        [Tooltip("How close to a wall the door has to be, in metres, to snap onto it.")]
        [SerializeField, Min(0f)] private float _doorSnapRange = 1f;
        [Tooltip("How far the door stays from a corner, in metres.")]
        [SerializeField, Min(0f)] private float _doorEndMargin = 0.7f;

        [SerializeField, Min(0f)] private float _stationHalfSize = 0.5f;
        [Tooltip("Half a walker's width, for keeping clear of furniture and walls.")]
        [SerializeField, Min(0f)] private float _walkerRadius = 0.3f;
        [Tooltip("How far from a station's centre someone stands to work at it.")]
        [SerializeField, Min(0f)] private float _standDistance = 0.85f;

        public Rect[] Walls => _walls;
        public Vector2 KitchenDoorHinge => _kitchenDoorHinge;
        public float KitchenDoorWidth => _kitchenDoorWidth;
        public SimVector2 RoomMin => ToSim(_roomMin);
        public SimVector2 RoomMax => ToSim(_roomMax);
        public SimVector2 KitchenAreaMin => ToSim(_kitchenAreaMin);
        public SimVector2 KitchenAreaMax => ToSim(_kitchenAreaMax);
        public SimVector2 DiningAreaMin => ToSim(_diningAreaMin);
        public SimVector2 DiningAreaMax => ToSim(_diningAreaMax);
        public SimVector2 DoorLineMin => ToSim(_doorLineMin);
        public SimVector2 DoorLineMax => ToSim(_doorLineMax);
        public float DoorSnapRange => _doorSnapRange;
        public float DoorEndMargin => _doorEndMargin;
        public float StationHalfSize => _stationHalfSize;
        public float WalkerRadius => _walkerRadius;

        /// <summary>
        /// For the builder: a restaurant's building, generated from its layout (Phase 6, milestone 3).
        /// The walker's and stations' sizes keep their tuned values.
        /// </summary>
        public void SetBuilding(Vector2 roomMin, Vector2 roomMax, Rect[] walls, Vector2 kitchenDoorHinge, float kitchenDoorWidth,
            Vector2 kitchenAreaMin, Vector2 kitchenAreaMax, Vector2 diningAreaMin, Vector2 diningAreaMax, Vector2 doorLineMin, Vector2 doorLineMax)
        {
            _roomMin = roomMin;
            _roomMax = roomMax;
            _walls = walls ?? new Rect[0];
            _kitchenDoorHinge = kitchenDoorHinge;
            _kitchenDoorWidth = kitchenDoorWidth;
            _kitchenAreaMin = kitchenAreaMin;
            _kitchenAreaMax = kitchenAreaMax;
            _diningAreaMin = diningAreaMin;
            _diningAreaMax = diningAreaMax;
            _doorLineMin = doorLineMin;
            _doorLineMax = doorLineMax;
        }

        public FloorPlan ToPlan()
        {
            var walls = new List<(SimVector2, SimVector2)>();
            foreach (var wall in _walls)
                walls.Add((ToSim(wall.min), ToSim(wall.max)));
            return new FloorPlan(RoomMin, RoomMax, walls, _stationHalfSize, _walkerRadius, _standDistance);
        }

        private static SimVector2 ToSim(Vector2 value) => new SimVector2(value.x, value.y);
    }
}
