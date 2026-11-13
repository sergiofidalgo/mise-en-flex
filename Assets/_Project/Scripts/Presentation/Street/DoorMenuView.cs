using Game.Gameplay.Customers;
using UnityEngine;

namespace Game.Presentation.Street
{
    /// <summary>
    /// The menu's A-board belongs to the street door (the user's note, 2026-11-12: "right next to
    /// the main door... if the door moves, the menu should be like attached to it"). It stands
    /// just outside, against the wall beside the door, on the side the queue doesn't use, and
    /// goes wherever players move the door, turning with its wall. The spot where a passer-by
    /// reads it moves with it.
    /// </summary>
    public sealed class DoorMenuView : MonoBehaviour
    {
        [SerializeField] private CustomerDirector _director;
        [Tooltip("Where a passer-by stands to read it.")]
        [SerializeField] private Transform _reader;
        [Tooltip("Metres from the door's middle, along the wall.")]
        [SerializeField, Min(0f)] private float _beside = 1.2f;
        [Tooltip("Metres out from the door's wall.")]
        [SerializeField, Min(0f)] private float _out = 0.45f;
        [Tooltip("Metres out from the door's wall its reader stands.")]
        [SerializeField, Min(0f)] private float _readerOut = 1.3f;

        // After the layout editor and the doorway have moved the door this frame (and, on the first
        // frame, after the kitchen's services exist: the queue's side is worked out from the room).
        private void LateUpdate() => Place();

        private void Place()
        {
            var door = _director != null ? _director.Door : null;
            if (door == null)
                return;
            var outward = -door.forward;
            var beside = door.position - _director.QueueSide * _beside;
            beside.y = transform.position.y;
            // Its chalked face looks out from the wall.
            transform.SetPositionAndRotation(beside + outward * _out, Quaternion.LookRotation(-outward, Vector3.up));
            if (_reader != null)
                _reader.SetPositionAndRotation(beside + outward * _readerOut, Quaternion.LookRotation(-outward, Vector3.up));
        }
    }
}
