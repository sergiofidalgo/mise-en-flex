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
    /// The fridge has a door at the front and one at the back, each two panes on separate tracks
    /// covering half its width. The door on the opener's side opens the half in front of them, by
    /// sliding that pane over the other, so it never leaves the fridge's own width: nothing about
    /// the room or the people in it can get in its way.
    /// </summary>
    public sealed class FridgeDoorView : MonoBehaviour
    {
        [Tooltip("Each door's left pane (as seen facing that door), on the inner track. Opening, it slides right, behind the other.")]
        [SerializeField] private Transform _frontLeft;
        [Tooltip("Each door's right pane (as seen facing that door), on the outer track. Opening, it slides left, in front of the other.")]
        [SerializeField] private Transform _frontRight;
        [SerializeField] private Transform _backLeft;
        [SerializeField] private Transform _backRight;
        [Tooltip("Puffs out of whichever door opens.")]
        [SerializeField] private ParticleSystem _mist;
        [Tooltip("Which sides are doors and which are solid; a door against a wall isn't there to open.")]
        [SerializeField] private FridgeShellView _shell;

        [Tooltip("Metres a pane slides to open its half: the pane's width less the overlap.")]
        [SerializeField, Range(0.1f, 0.6f)] private float _slide = 0.44f;
        [SerializeField, Min(0.01f)] private float _openSeconds = 0.2f;
        [SerializeField, Min(0.01f)] private float _closeSeconds = 0.25f;
        [Tooltip("Puffs of cold air when the door opens.")]
        [SerializeField, Min(0)] private int _mistBurst = 14;

        private float _amount;
        private int _keptOpenOnFrame = int.MinValue;
        private Vector3 _frontLeftShut, _frontRightShut, _backLeftShut, _backRightShut;
        private Vector3 _mistAt;
        private Quaternion _mistTurn;
        private bool _measured;
        private Transform _opener;

        public event Action Opened;
        public event Action Closed;

        public bool IsOpen { get; private set; }

        /// <summary>True when it's the back door that's open (or opening); false for the front.</summary>
        public bool OpensBack { get; private set; }

        /// <summary>True when the right half (facing that door) is the one open; false for the left.</summary>
        public bool OpensRight { get; private set; } = true;

        /// <summary>
        /// Call every frame something should hold the door open. <paramref name="opener"/> is who
        /// opened it: the door on their side opens the half in front of them.
        /// </summary>
        public void KeepOpen(Transform opener = null)
        {
            _keptOpenOnFrame = Time.frameCount;
            if (!IsOpen && opener != null)
                _opener = opener;
        }

        /// <summary>
        /// Which door and half open for someone at <paramref name="position"/>: those in front of
        /// them. If the door on their side is solid against something, it's the other one.
        /// </summary>
        public (bool Back, bool Right) OpensFor(Vector3 position)
        {
            var local = transform.InverseTransformPoint(position);
            var back = local.z > 0f;
            if (_shell != null && !IsDoor(back) && IsDoor(!back))
                back = !back;
            // Facing the back door, the fridge's left is on their right.
            return (back, back ? local.x <= 0f : local.x >= 0f);
        }

        private bool IsDoor(bool back) =>
            _shell == null || _shell.IsOpen(back ? FridgeShellView.Facing.Back : FridgeShellView.Facing.Front);

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
                        (OpensBack, OpensRight) = OpensFor(_opener.position);
                    if (IsDoor(OpensBack))
                        PuffFromOpenDoor();
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

        /// <summary>Poses the doors, 0 shut to 1 open. Public so it can be previewed outside Play mode.</summary>
        public void Show(float amount)
        {
            Measure();
            // Eased both ways, with no overshoot: a pane on a track glides and stops, it doesn't bounce.
            var offset = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(amount)) * _slide;
            Pose(_frontLeft, _frontLeftShut, _frontRight, _frontRightShut, !OpensBack ? offset : 0f);
            Pose(_backLeft, _backLeftShut, _backRight, _backRightShut, OpensBack ? offset : 0f);
        }

        /// <summary>Previews one door opened on one side, outside Play mode.</summary>
        public void Show(float amount, bool back, bool opensRight)
        {
            OpensBack = back;
            OpensRight = opensRight;
            Show(amount);
        }

        // Panes move in their door's own space, so the back door, turned round, mirrors the front.
        private void Pose(Transform left, Vector3 leftShut, Transform right, Vector3 rightShut, float offset)
        {
            left.localPosition = leftShut + (OpensRight ? Vector3.zero : Vector3.right * offset);
            right.localPosition = rightShut + (OpensRight ? Vector3.left * offset : Vector3.zero);
        }

        // The cold air rolls out of whichever door opened.
        private void PuffFromOpenDoor()
        {
            Measure();
            var turn = OpensBack ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            _mist.transform.localPosition = turn * _mistAt;
            _mist.transform.localRotation = turn * _mistTurn;
            _mist.Emit(_mistBurst);
        }

        // Where everything sits, as built.
        private void Measure()
        {
            if (_measured)
                return;
            _measured = true;
            _frontLeftShut = _frontLeft.localPosition;
            _frontRightShut = _frontRight.localPosition;
            _backLeftShut = _backLeft.localPosition;
            _backRightShut = _backRight.localPosition;
            _mistAt = _mist.transform.localPosition;
            _mistTurn = _mist.transform.localRotation;
        }
    }
}
