using Game.Gameplay.Staff;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation
{
    /// <summary>A door between rooms: swings open as someone comes near. It never blocks anyone.</summary>
    public sealed class SwingDoorView : MonoBehaviour
    {
        [SerializeField] private StaffDirector _staff;
        [Tooltip("The middle of the doorway: people near this open it.")]
        [SerializeField] private Vector3 _doorway;
        [SerializeField, Min(0f)] private float _openRange = 1.4f;
        [SerializeField] private float _openAngle = 100f;
        [Tooltip("How quickly it swings, per second.")]
        [SerializeField, Min(0.01f)] private float _swingRate = 12f;
        [SerializeField] private KitchenAudio _audio;
        [Tooltip("Degrees from shut where the closing thud plays, so it lands as the door meets the frame.")]
        [SerializeField, Min(0f)] private float _thudAngle = 8f;

        private float _angle;
        private bool _open;

        private void Update()
        {
            var near = SomeoneNear();
            var goal = near ? _openAngle : 0f;
            _angle = Mathf.Lerp(_angle, goal, 1f - Mathf.Exp(-_swingRate * Time.deltaTime));
            transform.localRotation = Quaternion.Euler(0f, _angle, 0f);
            PlaySounds(near);
        }

        // Tied to the door having actually shut, not just to who's near: someone hovering at the edge
        // of the range would otherwise set off a creak every few frames.
        private void PlaySounds(bool near)
        {
            if (_audio == null)
                return;

            if (near && !_open)
            {
                _open = true;
                _audio.Play(_audio.Sounds.DoorOpen, _doorway);
            }
            else if (!near && _open && _angle < _thudAngle)
            {
                _open = false;
                _audio.Play(_audio.Sounds.DoorClose, _doorway);
            }
        }

        private bool SomeoneNear()
        {
            foreach (var player in PlayerInput.all)
            {
                if (IsNear(player.transform.position))
                    return true;
            }
            foreach (var agent in _staff.Agents)
            {
                if (IsNear(agent.transform.position))
                    return true;
            }
            return false;
        }

        private bool IsNear(Vector3 position)
        {
            position.y = _doorway.y;
            return (position - _doorway).sqrMagnitude < _openRange * _openRange;
        }
    }
}
