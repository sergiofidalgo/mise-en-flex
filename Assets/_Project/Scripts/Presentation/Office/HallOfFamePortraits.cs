using Game.Gameplay.Saving;
using UnityEngine;

namespace Game.Presentation.Office
{
    /// <summary>
    /// Dusty portraits of erased chefs in the hall of fame (§15.3): one shows for each campaign the
    /// boss has erased, oldest first, read from the cookbook file, which outlives campaigns.
    /// </summary>
    public sealed class HallOfFamePortraits : MonoBehaviour
    {
        [Tooltip("The portraits on the wall, in the order they fill.")]
        [SerializeField] private GameObject[] _slots = System.Array.Empty<GameObject>();

        private void Start()
        {
            var erased = SharedCookbookFile.Current.Erased?.Count ?? 0;
            for (var i = 0; i < _slots.Length; i++)
                if (_slots[i] != null)
                    _slots[i].SetActive(i < erased);
        }
    }
}
