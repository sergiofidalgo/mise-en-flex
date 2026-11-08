using System;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Slides a fridge's glass panes open while anyone has its menu open (RT held), with a puff of
    /// cold air as it opens (decision 2026-11-08). A chef's <see cref="StorageUseView"/> keeps it open
    /// each frame, so two players at one fridge keep it open until both let go. Opened and Closed are
    /// for sound.
    ///
    /// The fridge has two panes on separate tracks, each covering half its front. The half in front
    /// of whoever opens it slides behind or in front of the other, so the door never leaves the
    /// fridge's own width: nothing about the room or the people in it can get in its way.
    /// </summary>
    public sealed class FridgeDoorView : MonoBehaviour
    {
        [Tooltip("The pane on the fridge's left, on the back track. Opening, it slides right, behind the other.")]
        [SerializeField] private Transform _leftPane;
        [Tooltip("The pane on the fridge's right, on the front track. Opening, it slides left, in front of the other.")]
        [SerializeField] private Transform _rightPane;
        [SerializeField] private ParticleSystem _mist;

        [Tooltip("Metres a pane slides to open its half: the pane's width less the overlap.")]
        [SerializeField, Range(0.1f, 0.6f)] private float _slide = 0.44f;
        [SerializeField, Min(0.01f)] private float _openSeconds = 0.2f;
        [SerializeField, Min(0.01f)] private float _closeSeconds = 0.25f;
        [Tooltip("Puffs of cold air when the door opens.")]
        [SerializeField, Min(0)] private int _mistBurst = 14;

        private float _amount;
        private int _keptOpenOnFrame = int.MinValue;
        private Vector3 _leftShut;
        private Vector3 _rightShut;
        private bool _measured;
        private Transform _opener;

        public event Action Opened;
        public event Action Closed;

        public bool IsOpen { get; private set; }

        /// <summary>True when the right half is the one open (or opening); false for the left.</summary>
        public bool OpensRight { get; private set; } = true;

        /// <summary>
        /// Call every frame something should hold the door open. <paramref name="opener"/> is who
        /// opened it: the half in front of them is the one that opens.
        /// </summary>
        public void KeepOpen(Transform opener = null)
        {
            _keptOpenOnFrame = Time.frameCount;
            if (!IsOpen && opener != null)
                _opener = opener;
        }

        /// <summary>Which half opens for someone standing at <paramref name="position"/>: the one in front of them.</summary>
        public bool OpensRightFor(Vector3 position) => transform.InverseTransformPoint(position).x >= 0f;

        private void Update()
        {
            // One frame's grace, so the order scripts run in doesn't make it flicker shut.
            var open = Time.frameCount - _keptOpenOnFrame <= 1;
            if (open != IsOpen)
            {
                IsOpen = open;
                if (open)
                {
                    // The side only changes while it's shut, so a pane caught closing never jumps across.
                    if (_amount <= 0f && _opener != null)
                        OpensRight = OpensRightFor(_opener.position);
                    _mist.Emit(_mistBurst);
                    Opened?.Invoke();
                }
                else
                {
                    _opener = null;
                    Closed?.Invoke();
                }
            }

            _amount = Swing.Step(_amount, open, _openSeconds, _closeSeconds, Time.deltaTime);
            Show(_amount);
        }

        /// <summary>Poses the door, 0 shut to 1 open. Public so it can be previewed outside Play mode.</summary>
        public void Show(float amount)
        {
            Measure();
            // Eased both ways, with no overshoot: a pane on a track glides and stops, it doesn't bounce.
            var offset = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(amount)) * _slide;
            _leftPane.localPosition = _leftShut + (OpensRight ? Vector3.zero : Vector3.right * offset);
            _rightPane.localPosition = _rightShut + (OpensRight ? Vector3.left * offset : Vector3.zero);
        }

        /// <summary>Previews the door opened on one side, outside Play mode.</summary>
        public void Show(float amount, bool opensRight)
        {
            OpensRight = opensRight;
            Show(amount);
        }

        // The panes' shut places, as built.
        private void Measure()
        {
            if (_measured)
                return;
            _measured = true;
            _leftShut = _leftPane.localPosition;
            _rightShut = _rightPane.localPosition;
        }
    }
}
