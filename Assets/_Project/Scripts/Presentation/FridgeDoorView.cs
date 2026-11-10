using System;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Lifts a chest fridge's glass lid while anyone has its menu open (RT held), with a breath of
    /// cold air as it opens (decision 2026-11-08). A chef's <see cref="StorageUseView"/> keeps it open
    /// each frame, so two players at one fridge keep it open until both let go. Opened and Closed are
    /// for sound. When stock bought at a stall arrives, the lid pops open a moment with the same
    /// breath of cold air (decision 2026-11-10), so the purchase shows where it went.
    ///
    /// The lid hinges along the back of the top and opens to no more than upright, so it stays over
    /// the fridge's own footprint: no wall, furniture or chef can be in its way, wherever the fridge
    /// stands. Being glass, it hides nothing even when it stands between the camera and the stock.
    /// </summary>
    public sealed class FridgeDoorView : MonoBehaviour
    {
        [Tooltip("Turns about its hinge along the back of the top; shut at no rotation, lying towards the front.")]
        [SerializeField] private Transform _lid;
        [SerializeField] private ParticleSystem _mist;

        [Tooltip("Degrees the lid opens. With its small overshoot it must stay under 90, or it would lean out past the back.")]
        [SerializeField, Range(30f, 80f)] private float _openAngle = 75f;
        [SerializeField, Min(0.01f)] private float _openSeconds = 0.25f;
        [SerializeField, Min(0.01f)] private float _closeSeconds = 0.3f;
        [Tooltip("How far past open it swings before settling, as a fraction.")]
        [SerializeField, Range(0f, 2f)] private float _overshoot = 1f;
        [Tooltip("Puffs of cold air when the lid opens: a short, visible breath (the user's note, 2026-11-08).")]
        [SerializeField, Min(0)] private int _mistBurst = 32;
        [Tooltip("Puffs a second that keep spilling out while it stays open; none by default.")]
        [SerializeField, Min(0f)] private float _mistWhileOpen;

        [Tooltip("Seconds the lid stays up when bought stock arrives.")]
        [SerializeField, Min(0f)] private float _stockedOpenSeconds = 0.35f;

        private float _amount;
        private float _poppedUntil = -1f;
        private Game.Gameplay.Stations.StorageStation _storage;
        // Never yet. Not int.MinValue: "frame now minus it" overflowed to a negative number, which
        // read as "kept open just now", so the lid stood open from the start of the day until someone
        // opened and closed it (the user's bug report, 2026-11-08).
        private int _keptOpenOnFrame = -1000;

        public event Action Opened;
        public event Action Closed;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            _storage = GetComponent<Game.Gameplay.Stations.StorageStation>();
        }

        private void OnEnable()
        {
            if (_storage != null)
                _storage.Stocked += Pop;
        }

        private void OnDisable()
        {
            if (_storage != null)
                _storage.Stocked -= Pop;
        }

        // Up a moment and down again; the mist and the sounds come with the opening as usual.
        private void Pop(int portions) => _poppedUntil = Time.time + _stockedOpenSeconds;

        /// <summary>Call every frame something should hold the lid open. Who opens it doesn't matter.</summary>
        public void KeepOpen(Transform opener = null) => _keptOpenOnFrame = Time.frameCount;

        private void Update()
        {
            // One frame's grace, so the order scripts run in doesn't make it flicker shut.
            var open = Time.frameCount - _keptOpenOnFrame <= 1 || Time.time < _poppedUntil;
            if (open != IsOpen)
            {
                IsOpen = open;
                var spill = _mist.emission;
                spill.rateOverTime = open ? _mistWhileOpen : 0f;
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

        /// <summary>Poses the lid, 0 shut to 1 open. Public so it can be previewed outside Play mode.</summary>
        public void Show(float amount)
        {
            // Thrown open, it swings a little past and settles back; never beyond upright.
            var angle = Mathf.Min(Swing.EaseOutBack(amount, _overshoot) * _openAngle, 89f);
            // About its own x, a positive angle lifts the front edge, which lies towards −z.
            _lid.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
