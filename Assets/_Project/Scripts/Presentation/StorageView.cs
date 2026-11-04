using System;
using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows a storage unit's stock from the overhead camera: each bin shows a sample of what its
    /// slot holds right now, and nothing once it's empty, so running out is visible at a glance.
    /// </summary>
    public sealed class StorageView : MonoBehaviour
    {
        [Serializable]
        public sealed class Sample
        {
            [SerializeField] private int _slot;
            [SerializeField] private string _typeId;
            [SerializeField] private GameObject _shape;

            public int Slot => _slot;
            public string TypeId => _typeId;
            public GameObject Shape => _shape;
        }

        [SerializeField] private StorageStation _storage;

        [Tooltip("One shape per bin per ingredient; the one matching what's in the slot shows.")]
        [SerializeField] private Sample[] _samples = Array.Empty<Sample>();

        private void Update()
        {
            var unit = _storage.Unit;
            if (unit == null)
                return;

            foreach (var sample in _samples)
            {
                var show = sample.Slot < unit.Slots.Count && unit.Slots[sample.Slot].TypeId == sample.TypeId;
                if (sample.Shape.activeSelf != show)
                    sample.Shape.SetActive(show);
            }
        }
    }
}
