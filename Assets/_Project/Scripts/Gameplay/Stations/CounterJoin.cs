using System.Collections.Generic;
using Game.Simulation.Movement;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// Joins a counter's top with the counters right beside it, so a run of counters reads as one
    /// continuous surface (the user's note, 2026-11-08): rounded along sides open to the room,
    /// square and flush where it meets another counter. It keeps up as counters are moved in the
    /// layout editor. Only counters join each other: hobs, sinks and the rest keep their own edges.
    /// </summary>
    [RequireComponent(typeof(Interactable))]
    public sealed class CounterJoin : MonoBehaviour
    {
        // Every counter that joins, to look for neighbours among; a handful, so checking them all
        // each frame is cheap.
        private static readonly List<CounterJoin> Counters = new List<CounterJoin>();

        [Tooltip("The top whose mesh changes with what it joins.")]
        [SerializeField] private MeshFilter _top;

        [Tooltip("The top for each set of joined sides, by CounterJoins bits: 1 +x, 2 -x, 4 +z, 8 -z.")]
        [SerializeField] private Mesh[] _tops = new Mesh[16];

        [Tooltip("Half a standard station's width, in metres.")]
        [SerializeField, Min(0.01f)] private float _halfSize = 0.5f;

        private readonly List<Footprint> _neighbours = new List<Footprint>();
        private Interactable _station;
        private int _joined = -1;

        public Footprint Footprint => Station.Footprint;

        private Interactable Station => _station != null ? _station : _station = GetComponent<Interactable>();

        private void OnEnable() => Counters.Add(this);

        private void OnDisable() => Counters.Remove(this);

        private void LateUpdate()
        {
            _neighbours.Clear();
            foreach (var counter in Counters)
            {
                if (counter != this)
                    _neighbours.Add(counter.Footprint);
            }
            Show(CounterJoins.Joined(Footprint, _halfSize, _neighbours));
        }

        /// <summary>Shows the top for these joined sides (CounterJoins bits).</summary>
        public void Show(int joined)
        {
            if (joined == _joined || _top == null || joined < 0 || joined >= _tops.Length)
                return;
            _joined = joined;
            _top.sharedMesh = _tops[joined];
        }
    }
}
