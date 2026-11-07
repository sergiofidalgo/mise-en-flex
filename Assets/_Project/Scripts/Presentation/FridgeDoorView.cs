using System;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Opens a fridge's door while anyone has its menu open (RT held), with a puff of cold air as
    /// it opens. A chef's <see cref="StorageUseView"/> keeps it open each frame, so two players at
    /// one fridge keep it open until both let go. Opened and Closed are for sound.
    /// </summary>
    public sealed class FridgeDoorView : MonoBehaviour
    {
        [Tooltip("Turns about its hinge; closed at no rotation.")]
        [SerializeField] private Transform _door;
        [SerializeField] private ParticleSystem _mist;

        [Tooltip("Degrees the door swings open, outwards towards the camera.")]
        [SerializeField, Range(0f, 150f)] private float _openAngle = 105f;
        [SerializeField, Min(0.01f)] private float _openSeconds = 0.25f;
        [SerializeField, Min(0.01f)] private float _closeSeconds = 0.3f;
        [Tooltip("Puffs of cold air when the door opens.")]
        [SerializeField, Min(0)] private int _mistBurst = 14;

        private float _amount;
        private int _keptOpenOnFrame = int.MinValue;

        public event Action Opened;
        public event Action Closed;

        public bool IsOpen { get; private set; }

        /// <summary>Call every frame something should hold the door open.</summary>
        public void KeepOpen() => _keptOpenOnFrame = Time.frameCount;

        private void Update()
        {
            // One frame's grace, so the order scripts run in doesn't make it flicker shut.
            var open = Time.frameCount - _keptOpenOnFrame <= 1;
            if (open != IsOpen)
            {
                IsOpen = open;
                if (open)
                {
                    _mist.Emit(_mistBurst);
                    Opened?.Invoke();
                }
                else
                {
                    Closed?.Invoke();
                }
            }

            _amount = Swing.Step(_amount, open, _openSeconds, _closeSeconds, Time.deltaTime);
            Show(_amount);
        }

        /// <summary>Poses the door, 0 shut to 1 open. Public so it can be previewed outside Play mode.</summary>
        public void Show(float amount) =>
            _door.localRotation = Quaternion.Euler(0f, Swing.EaseOutBack(amount) * _openAngle, 0f);
    }
}
