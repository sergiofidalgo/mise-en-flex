using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows a chef using storage while its menu is open (RT held): a fridge's door opens, and at
    /// a shelf the chef bends at the waist to reach in.
    /// </summary>
    public sealed class StorageUseView : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor _interactor;
        [Tooltip("The torso turns about this to bend forwards.")]
        [SerializeField] private Transform _waist;

        [SerializeField, Range(0f, 90f)] private float _bendAngle = 40f;
        [SerializeField, Min(0.01f)] private float _bendSeconds = 0.2f;
        [SerializeField, Min(0.01f)] private float _straightenSeconds = 0.25f;

        private float _amount;

        private void Update()
        {
            var station = _interactor.IsSlotMenuOpen ? _interactor.Target as StorageStation : null;
            if (station != null && station.TryGetComponent<FridgeDoorView>(out var door))
                door.KeepOpen();

            _amount = Swing.Step(_amount, station != null && !station.IsRefrigerated, _bendSeconds, _straightenSeconds, Time.deltaTime);
            Show(_amount);
        }

        /// <summary>Poses the chef, 0 upright to 1 bent. Public so it can be previewed outside Play mode.</summary>
        public void Show(float amount) =>
            _waist.localRotation = Quaternion.Euler(Mathf.SmoothStep(0f, 1f, amount) * _bendAngle, 0f, 0f);
    }
}
