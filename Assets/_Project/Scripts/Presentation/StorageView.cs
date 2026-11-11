using System;
using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows a storage unit's stock from the overhead camera: each bin shows a sample of what its
    /// slot holds right now, and nothing once it's empty, so running out is visible at a glance. The
    /// sample shows the age of the oldest portion, the one taken next (decision 2026-11-11,
    /// <see cref="FreshnessLook"/>): stale stock dulls, spoiled stock goes mouldy. Once every slot
    /// is full, the same 🚫 as the bin's pops up over it (the user's note, 2026-11-11), so players
    /// know to put food somewhere else.
    /// </summary>
    public sealed class StorageView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Serializable]
        public sealed class Sample
        {
            [SerializeField] private int _slot;
            [SerializeField] private string _typeId;
            [SerializeField] private GameObject _shape;
            [Tooltip("Fuzzy mould spots on the sample, shown while the oldest portion is spoiled. Optional.")]
            [SerializeField] private GameObject _mould;

            public int Slot => _slot;
            public string TypeId => _typeId;
            public GameObject Shape => _shape;
            public GameObject Mould => _mould;

            [NonSerialized] public Renderer[] Renderers;
            [NonSerialized] public Color[] Raw;
            [NonSerialized] public float ShownFreshness = 1f;
        }

        [SerializeField] private StorageStation _storage;

        [Tooltip("Where the stale and spoiled lines are. Empty: 35% and 10%.")]
        [SerializeField] private Game.Data.TuningConfig _tuning;

        [Tooltip("One shape per bin per ingredient; the one matching what's in the slot shows.")]
        [SerializeField] private Sample[] _samples = Array.Empty<Sample>();

        [Tooltip("The 🚫 over the unit once every slot is full.")]
        [SerializeField] private FullSign _fullSign = new FullSign();

        private MaterialPropertyBlock _block;

        private float StaleBelow => _tuning != null ? _tuning.StaleBelow : 0.35f;
        private float SpoiledBelow => _tuning != null ? _tuning.SpoiledBelow : 0.1f;

        private void Awake()
        {
            _fullSign.Build(_storage);
            _block = new MaterialPropertyBlock();
            foreach (var sample in _samples)
            {
                sample.Renderers = sample.Shape != null ? sample.Shape.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
                sample.Raw = new Color[sample.Renderers.Length];
                for (var i = 0; i < sample.Renderers.Length; i++)
                {
                    var material = sample.Renderers[i].sharedMaterial;
                    sample.Raw[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
                }
            }
        }

        private void LateUpdate()
        {
            _fullSign.Tick(_storage.Unit != null && _storage.Unit.IsFull);
        }

        private void Update()
        {
            var unit = _storage.Unit;
            if (unit == null)
                return;

            foreach (var sample in _samples)
            {
                // Cooked food shows itself (its own view stays in the bin), not the raw sample.
                var slot = sample.Slot < unit.Slots.Count ? unit.Slots[sample.Slot] : null;
                var show = slot != null && slot.TypeId == sample.TypeId && !slot.HoldsCooked;
                if (sample.Shape.activeSelf != show)
                    sample.Shape.SetActive(show);

                var freshness = show ? slot.Stock[0].Freshness : 1f;
                var mouldy = show && FreshnessLook.StageOf(freshness, StaleBelow, SpoiledBelow) == FreshnessLook.Stage.Mouldy;
                if (sample.Mould != null && sample.Mould.activeSelf != mouldy)
                    sample.Mould.SetActive(mouldy);
                if (show && Mathf.Abs(freshness - sample.ShownFreshness) > 0.002f)
                    Age(sample, freshness);
            }
        }

        private void Age(Sample sample, float freshness)
        {
            sample.ShownFreshness = freshness;
            for (var i = 0; i < sample.Renderers.Length; i++)
            {
                sample.Renderers[i].GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, FreshnessLook.Tint(sample.Raw[i], freshness, StaleBelow, SpoiledBelow));
                sample.Renderers[i].SetPropertyBlock(_block);
            }
        }
    }
}
