using System;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Maps item type ids (as used by the simulation) to the prefab that shows them.
    /// Phase 0 placeholder for <c>IngredientDefinition</c>, which replaces it in Phase 1.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Item Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string _typeId;
            [SerializeField] private GameObject _prefab;

            public string TypeId => _typeId;
            public GameObject Prefab => _prefab;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        /// <returns>The prefab for <paramref name="typeId"/>, or null if the catalog doesn't know it.</returns>
        public GameObject GetPrefab(string typeId)
        {
            foreach (var entry in _entries)
            {
                if (entry.TypeId == typeId)
                    return entry.Prefab;
            }

            return null;
        }
    }
}
