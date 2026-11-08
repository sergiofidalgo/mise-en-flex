using Game.Gameplay.Items;
using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Food put down on a counter sits a little off centre and turned, differently on every counter
    /// (the user's note, 2026-11-08), so a kitchen of counters doesn't look stamped out, and round
    /// things like an onion show it by facing every which way. The spot and turn come from the
    /// counter's position and the food's kind, so they don't jump about while it sits there. A pile
    /// of dirty plates on a counter counts too, each plate in it nudged, turned and tipped its own
    /// way, so the pile looks dropped rather than stacked (the user's note, 2026-11-08). Not in a
    /// container, on the board, or anywhere else. Only looks.
    /// </summary>
    public sealed class CounterRestView : MonoBehaviour
    {
        [SerializeField] private ItemView _view;
        [Tooltip("How far from the counter's centre it can sit (m, before the counter's 1.4x).")]
        [SerializeField, Min(0f)] private float _maxOffset = 0.09f;
        [Tooltip("How far it can be turned either way (degrees).")]
        [SerializeField, Range(0f, 180f)] private float _maxTurn = 180f;
        [Tooltip("Its size on a counter, on top of the counter's own 1.4x (plates and dishes show bigger still).")]
        [SerializeField, Min(0.1f)] private float _scale = 1f;
        [Tooltip("How far it can tip either way (degrees), as a plate does on an uneven pile.")]
        [SerializeField, Range(0f, 20f)] private float _maxTilt;

        private Transform _anchor;
        private bool _resting;
        private Vector3 _offset;
        private Quaternion _turn = Quaternion.identity;

        private void LateUpdate()
        {
            if (_view == null || _view.Anchor == null)
                return;
            if (_view.Anchor != _anchor)
            {
                _anchor = _view.Anchor;
                _resting = OnCounter(_anchor);
                if (_resting)
                    Choose(_anchor.position, _view.Item?.TypeId ?? name);
            }
            if (_resting)
                _view.SetHeldPose(_offset, _turn, scale: _scale);
        }

        // Directly on a counter's item spot, or on a plate pile's level above it.
        private static bool OnCounter(Transform anchor)
        {
            var parent = anchor.parent;
            if (parent == null)
                return false;
            if (parent.GetComponent<CounterStation>() != null)
                return true;
            return anchor.name.StartsWith("PileAnchor_") && parent.parent != null && parent.parent.GetComponent<CounterStation>() != null;
        }

        // A spot in a disc, a turn and a tip, from a hash of where it sits (each level of a pile its
        // own) and what it is.
        private void Choose(Vector3 counterPosition, string kind)
        {
            var seed = Hash(Mathf.RoundToInt(counterPosition.x * 10f), Mathf.RoundToInt(counterPosition.z * 10f) * 1000
                + Mathf.RoundToInt(counterPosition.y * 100f), kind);
            var angle = Unit(seed, 1) * 2f * Mathf.PI;
            var reach = Mathf.Sqrt(Unit(seed, 2)) * _maxOffset; // even over the disc
            _offset = new Vector3(Mathf.Cos(angle) * reach, 0f, Mathf.Sin(angle) * reach);
            // Only up the pile: the bottom one lies flat on the counter top.
            var tilt = _anchor != null && _anchor.name.StartsWith("PileAnchor_") ? _maxTilt : 0f;
            _turn = Quaternion.Euler((Unit(seed, 4) * 2f - 1f) * tilt, (Unit(seed, 3) * 2f - 1f) * _maxTurn,
                (Unit(seed, 5) * 2f - 1f) * tilt);
        }

        // FNV-1a over the counter's position (in 10 cm steps) and the food's kind: the same every run.
        private static uint Hash(int x, int z, string kind)
        {
            var h = 2166136261u;
            void Mix(int value)
            {
                for (var i = 0; i < 4; i++)
                {
                    h ^= (uint)((value >> (8 * i)) & 0xFF);
                    h *= 16777619u;
                }
            }
            Mix(x);
            Mix(z);
            foreach (var c in kind)
                Mix(c);
            return h;
        }

        // A number in [0, 1) for each salt, from the seed.
        private static float Unit(uint seed, uint salt)
        {
            var h = seed ^ (salt * 0x9E3779B9u);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
}
