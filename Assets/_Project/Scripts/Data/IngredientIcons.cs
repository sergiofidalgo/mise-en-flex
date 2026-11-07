using System;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// A picture for each ingredient, shown in menus such as storage's RT menu so players see what
    /// they're choosing. Rendered from the food's own models by Tools/Game/Art/Render Ingredient Icons.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Ingredient Icons")]
    public sealed class IngredientIcons : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string _typeId;
            [SerializeField] private Sprite _icon;

            public Entry(string typeId, Sprite icon)
            {
                _typeId = typeId;
                _icon = icon;
            }

            public string TypeId => _typeId;
            public Sprite Icon => _icon;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        public Entry[] Entries
        {
            get => _entries;
            set => _entries = value ?? Array.Empty<Entry>();
        }

        /// <summary>The ingredient's picture, or null if it has none yet.</summary>
        public Sprite Find(string typeId)
        {
            foreach (var entry in _entries)
            {
                if (entry.TypeId == typeId)
                    return entry.Icon;
            }
            return null;
        }
    }
}
