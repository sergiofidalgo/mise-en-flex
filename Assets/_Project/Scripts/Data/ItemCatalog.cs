using System;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// All ingredient types the kitchen knows, looked up by type id.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Item Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [SerializeField] private IngredientDefinition[] _ingredients = Array.Empty<IngredientDefinition>();

        /// <returns>The definition for <paramref name="typeId"/>, or null if the catalog doesn't know it.</returns>
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
        public GameObject GetPrefab(string typeId) => Find(typeId)?.Prefab;
    }
}
