using System.Collections.Generic;
using Game.Simulation.Items;
using Game.Simulation.Movement;
using UnityEngine;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// The street's wheelie bin (decisions 2026-11-11): moved in rearranging like furniture, but only
    /// round the outside of the building along its <see cref="BinTrack"/>. It doesn't jump to the
    /// cursor as other pieces do: it rolls after it on its wheels, a little behind, the long way
    /// round the corners. Not a station: chefs don't use it, and it isn't sold or saved.
    /// </summary>
    public sealed class WheelieBin : Interactable
    {
        [Header("Track (set by GreyboxStreet from the building)")]
        [SerializeField] private Vector2 _buildingMin;
        [SerializeField] private Vector2 _buildingMax;
        [Tooltip("Metres between a wall and the bin's back.")]
        [SerializeField, Min(0f)] private float _wallGap = 0.15f;
        [Tooltip("The bin's length along a wall and its depth out from it, in metres.")]
        [SerializeField] private Vector2 _size = new Vector2(1.8f, 1.2f);
        [Tooltip("The radius its middle rounds a corner on, in metres. Bigger and the building's corner pokes into it.")]
        [SerializeField, Min(0.1f)] private float _cornerRadius = 1f;

        [Tooltip("Metres of the east wall's north end it never rolls onto: the air conditioning's outdoor unit stands there.")]
        [SerializeField, Min(0f)] private float _eastEndKept;

        [Header("Rolling")]
        [Tooltip("How far behind the cursor it rolls: it gets there in about twice this, in seconds.")]
        [SerializeField, Min(0.01f)] private float _rollLag = 0.3f;
        [Tooltip("Its fastest roll, in metres per second, so a long way round takes a moment.")]
        [SerializeField, Min(0.1f)] private float _topSpeed = 7f;
        [Tooltip("The wheels, each turned about its axle (local z) as it rolls.")]
        [SerializeField] private Transform[] _wheels = new Transform[0];
        [Tooltip("The wheels' radius, in metres, so they turn as far as the bin rolls.")]
        [SerializeField, Min(0.01f)] private float _wheelRadius = 0.12f;

        [Header("Keeping the way in clear")]
        [Tooltip("Metres kept clear beside the street door, on top of its own width.")]
        [SerializeField, Min(0f)] private float _doorMargin = 0.5f;
        [Tooltip("Where guests step up to the door, in metres out from it (CustomerDirector's doorstep).")]
        [SerializeField, Min(0f)] private float _doorstep = 0.9f;
        [Tooltip("The width of the guests' way from the doorstep to the pavement, in metres.")]
        [SerializeField, Min(0.1f)] private float _pathWidth = 0.9f;

        private BinTrack _track;
        private float _shown;
        private float _target;
        private float _velocity;
        private float _wheelTurn;

        public override IItemContainer Container => null;

        /// <summary>Where it can roll.</summary>
        public BinTrack Track => _track ??= MakeTrack();

        /// <summary>Where it's headed, along the track.</summary>
        public float Target => _target;

        /// <summary>Where it stands now, along the track (behind <see cref="Target"/> while it rolls).</summary>
        public float Shown => _shown;

        /// <summary>True while it's still rolling to its spot.</summary>
        public bool IsRolling => Mathf.Abs(_target - _shown) > 0.005f;

        private void Awake()
        {
            _shown = Track.Nearest(Floor(transform.position));
            _target = _shown;
            Pose();
        }

        /// <summary>Rolls to a spot along the track, from wherever it is now.</summary>
        public void RollTo(float s) => _target = Mathf.Clamp(s, 0f, Track.Length);

        /// <summary>Puts it straight at a spot, without rolling (a load, a test).</summary>
        public void JumpTo(float s)
        {
            _target = _shown = Mathf.Clamp(s, 0f, Track.Length);
            _velocity = 0f;
            Pose();
        }

        /// <summary>
        /// The places it mustn't stand in: the street door and a margin either side, and the guests'
        /// way from the doorstep south to the pavement (along a side wall, when the door is on one).
        /// </summary>
        /// <param name="at">The street door's footprint, where it stands or would.</param>
        public IReadOnlyList<Footprint> KeepClear(Footprint at) =>
            BinTrack.KeepClear(at, _wallGap + _size.y, _doorMargin, _doorstep, _pathWidth);

        private void Update()
        {
            if (!IsRolling && Mathf.Approximately(_velocity, 0f))
                return;
            var before = _shown;
            // Rearranging runs with the game paused, so it rolls on real time, as the cursor moves.
            _shown = Mathf.SmoothDamp(_shown, _target, ref _velocity, _rollLag, _topSpeed, Time.unscaledDeltaTime);
            if (!IsRolling && Mathf.Abs(_velocity) < 0.01f)
            {
                _shown = _target;
                _velocity = 0f;
            }
            // Distance rolled turns the wheels by that much of their rim: angle = distance / radius.
            _wheelTurn = Mathf.Repeat(_wheelTurn + (_shown - before) / _wheelRadius * Mathf.Rad2Deg, 360f);
            Pose();
        }

        private void Pose()
        {
            var (position, degrees) = Track.At(_shown);
            transform.SetPositionAndRotation(new Vector3(position.X, transform.position.y, position.Y), Quaternion.Euler(0f, degrees, 0f));
            foreach (var wheel in _wheels)
            {
                if (wheel != null)
                    wheel.localRotation = Quaternion.Euler(0f, 0f, _wheelTurn);
            }
        }

        private BinTrack MakeTrack() => new BinTrack(new SimVector2(_buildingMin.x, _buildingMin.y), new SimVector2(_buildingMax.x, _buildingMax.y),
            _wallGap, new SimVector2(_size.x, _size.y), _cornerRadius, _eastEndKept);

        private static SimVector2 Floor(Vector3 position) => new SimVector2(position.x, position.z);
    }
}
