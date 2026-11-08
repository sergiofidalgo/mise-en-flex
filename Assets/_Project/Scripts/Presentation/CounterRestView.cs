using Game.Gameplay.Items;
using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Food put down on a counter sits a little off centre and turned, differently on every counter
    /// (the user's note, 2026-11-08), so a kitchen of counters doesn't look stamped out, and round
    /// things like an onion show it by facing every which way. The spot and turn come from the
    /// counter's position and the food's kind, so they don't jump about while it sits there. Only
    /// directly on a counter: not in a container, on the board, or anywhere else. Only looks.
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
                var counter = _anchor.parent != null ? _anchor.parent.GetComponent<CounterStation>() : null;
                _resting = counter != null;
                if (_resting)
                    Choose(_anchor.position, _view.Item?.TypeId ?? name);
            }
            if (_resting)
                _view.SetHeldPose(_offset, _turn, scale: _scale);
        }

        // A spot in a disc and a turn, from a hash of where the counter is and what's on it.
        private void Choose(Vector3 counterPosition, string kind)
        {
            var seed = Hash(Mathf.RoundToInt(counterPosition.x * 10f), Mathf.RoundToInt(counterPosition.z * 10f), kind);
            var angle = Unit(seed, 1) * 2f * Mathf.PI;
            var reach = Mathf.Sqrt(Unit(seed, 2)) * _maxOffset; // even over the disc
            _offset = new Vector3(Mathf.Cos(angle) * reach, 0f, Mathf.Sin(angle) * reach);
            _turn = Quaternion.Euler(0f, (Unit(seed, 3) * 2f - 1f) * _maxTurn, 0f);
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
