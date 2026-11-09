using System.Collections.Generic;
using Game.Presentation.Office.Ambience;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The bodyguards keep their eyes on the chefs (the user's notes, 2026-11-09). Anyone in the
    /// guards' own room, or in the rooms off it (the test kitchen and the hall of fame), is stared
    /// at: the guard turns to them and follows them round. Chefs anywhere else (the boss's office,
    /// the waiting room, the entrance, the accountant) are left alone, and with nobody to watch the
    /// guards face front again and go back to their idle glances.
    /// Who watches whom: one chef, both guards stare at them; two, they split down the middle of
    /// the screen, the left guard taking the chef on the left; three or more, each guard watches
    /// the chef nearest to him.
    /// </summary>
    public sealed class BodyguardWatch : MonoBehaviour
    {
        [Tooltip("Each guard's turning frame, left guard first (on screen and in the room).")]
        [SerializeField] private Transform[] _guards = System.Array.Empty<Transform>();
        [Tooltip("Their idle glances, switched off while they stare.")]
        [SerializeField] private Oscillate[] _idles = System.Array.Empty<Oscillate>();
        [Tooltip("Rooms where a chef is watched, as floor rectangles (x, z, width, depth).")]
        [SerializeField] private Rect[] _watched = System.Array.Empty<Rect>();
        [Tooltip("How fast a guard turns to stare (degrees a second): quick, so it reads as a stare.")]
        [SerializeField, Min(10f)] private float _turnSpeed = 320f;
        [Tooltip("How fast he turns back to the front when nobody's left to watch.")]
        [SerializeField, Min(10f)] private float _relaxSpeed = 120f;

        private readonly List<Vector3> _seen = new List<Vector3>();
        private Quaternion[] _front;
        private Transform[] _people;
        private Quaternion[] _peopleRest;

        private void Start()
        {
            _front = new Quaternion[_guards.Length];
            _people = new Transform[_guards.Length];
            _peopleRest = new Quaternion[_guards.Length];
            for (var i = 0; i < _guards.Length; i++)
            {
                _front[i] = _guards[i].rotation;
                if (i < _idles.Length && _idles[i] != null)
                {
                    _people[i] = _idles[i].transform;
                    _peopleRest[i] = _people[i].localRotation;
                }
            }
        }

        private void Update()
        {
            _seen.Clear();
            foreach (var player in PlayerInput.all)
            {
                var at = player.transform.position;
                foreach (var room in _watched)
                    if (room.Contains(new Vector2(at.x, at.z)))
                    {
                        _seen.Add(at);
                        break;
                    }
            }

            var dt = Time.deltaTime;
            for (var i = 0; i < _guards.Length; i++)
            {
                var guard = _guards[i];
                if (guard == null)
                    continue;
                var target = Target(i);
                var staring = target.HasValue;
                Quaternion wanted;
                if (staring)
                {
                    var look = target.Value - guard.position;
                    look.y = 0f;
                    wanted = look.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(look) : guard.rotation;
                }
                else
                    wanted = _front[i];
                guard.rotation = Quaternion.RotateTowards(guard.rotation, wanted, (staring ? _turnSpeed : _relaxSpeed) * dt);

                // The idle glance only while relaxed and facing front again; while staring the guard
                // is still, which is what makes it a stare.
                var idle = i < _idles.Length ? _idles[i] : null;
                if (idle == null)
                    continue;
                var relaxed = !staring && Quaternion.Angle(guard.rotation, _front[i]) < 1f;
                if (idle.enabled != relaxed)
                {
                    idle.enabled = relaxed;
                    if (!relaxed && _people[i] != null)
                        _people[i].localRotation = _peopleRest[i];
                }
            }
        }

        // Who guard `index` watches, or null for nobody.
        private Vector3? Target(int index)
        {
            switch (_seen.Count)
            {
                case 0:
                    return null;
                case 1:
                    return _seen[0];
                case 2:
                {
                    // Split down the middle of the screen: the left guard takes the chef further left.
                    var camera = Camera.main;
                    var a = _seen[0];
                    var b = _seen[1];
                    var aLeft = camera != null
                        ? camera.WorldToScreenPoint(a).x <= camera.WorldToScreenPoint(b).x
                        : a.x <= b.x;
                    var left = aLeft ? a : b;
                    var right = aLeft ? b : a;
                    return index == 0 ? left : right;
                }
                default:
                {
                    // Each guard watches the chef nearest to him.
                    var guard = _guards[index].position;
                    var best = _seen[0];
                    foreach (var at in _seen)
                        if ((at - guard).sqrMagnitude < (best - guard).sqrMagnitude)
                            best = at;
                    return best;
                }
            }
        }
    }
}
