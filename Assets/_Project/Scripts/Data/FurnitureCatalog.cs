using System;
using UnityEngine;

namespace Game.Data
{
    /// <summary>What can be bought for the kitchen and sold back. Prices are tuned here.</summary>
    [CreateAssetMenu(menuName = "Game/Furniture Catalog", fileName = "FurnitureCatalog")]
    public sealed class FurnitureCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string _id;
            [SerializeField] private string _displayName;
            [SerializeField, Min(0f)] private float _price;
            [Tooltip("The kitchen can't work without one, so the last can't be sold.")]
            [SerializeField] private bool _keepOne;
            [SerializeField] private GameObject _prefab;

            public string Id => _id;
            public string DisplayName => _displayName;
            public float Price => _price;
            public bool KeepOne => _keepOne;
            public GameObject Prefab => _prefab;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        [Tooltip("The share of the price that selling pays back.")]
        [SerializeField, Range(0f, 1f)] private float _sellFraction = 0.5f;

        public Entry[] Entries => _entries;

        public float SellFraction => _sellFraction;

        /// <summary>The entry with this id, or null: anything not listed isn't for sale.</summary>
        public Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            foreach (var entry in _entries)
            {
                if (entry.Id == id)
                    return entry;
            }
            return null;
        }
    }
}
