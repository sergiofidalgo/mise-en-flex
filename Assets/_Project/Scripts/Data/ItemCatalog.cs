using System;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// All item types the kitchen knows, looked up by type id: ingredients (with their
    /// cooking data) and kitchenware such as pans and plates (prefab only).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Item Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Kitchenware
        {
            [SerializeField] private string _typeId;
            [SerializeField] private GameObject _prefab;

            public string TypeId => _typeId;
            public GameObject Prefab => _prefab;
        }

        [SerializeField] private IngredientDefinition[] _ingredients = Array.Empty<IngredientDefinition>();
        [SerializeField] private Kitchenware[] _kitchenware = Array.Empty<Kitchenware>();

        /// <returns>The ingredient definition for <paramref name="typeId"/>, or null.</returns>
        public IngredientDefinition Find(string typeId)
        {
            foreach (var ingredient in _ingredients)
            {
                if (ingredient != null && ingredient.TypeId == typeId)
                    return ingredient;
            }

            return null;
        }

        /// <returns>The prefab for <paramref name="typeId"/>, or null if the catalog doesn't know it.</returns>
        public GameObject GetPrefab(string typeId)
        {
            var ingredient = Find(typeId);
            if (ingredient != null)
                return ingredient.Prefab;

            foreach (var ware in _kitchenware)
            {
                if (ware != null && ware.TypeId == typeId)
                    return ware.Prefab;
            }

            return null;
        }
    }
}
