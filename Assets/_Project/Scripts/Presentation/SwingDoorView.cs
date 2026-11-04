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

        private float _angle;

        private void Update()
        {
            var goal = SomeoneNear() ? _openAngle : 0f;
            _angle = Mathf.Lerp(_angle, goal, 1f - Mathf.Exp(-_swingRate * Time.deltaTime));
            transform.localRotation = Quaternion.Euler(0f, _angle, 0f);
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
