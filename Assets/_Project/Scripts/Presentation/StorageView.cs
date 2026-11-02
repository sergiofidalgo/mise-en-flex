using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows a storage unit's stock from the overhead camera: each bin's sample food is shown
    /// while the slot has stock and hidden once it's empty, so running out is visible at a glance.
    /// </summary>
    public sealed class StorageView : MonoBehaviour
    {
        [SerializeField] private StorageStation _storage;

        [Tooltip("Sample food on each bin, in slot order; null for unused slots.")]
        [SerializeField] private GameObject[] _samples = System.Array.Empty<GameObject>();

        private void Update()
        {
            var unit = _storage.Unit;
            if (unit == null)
                return;

            for (var i = 0; i < _samples.Length && i < unit.Slots.Count; i++)
            {
                if (_samples[i] != null)
                    _samples[i].SetActive(unit.Slots[i].Count > 0);
            }
        }
    }
}
