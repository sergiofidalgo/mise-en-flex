using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows one of a person's prebuilt looks, picked at random when they appear, so a dining room
    /// fills with different people from one prefab (concept v9.2: the archetype is the clothes,
    /// the person is rolled). Cosmetic only: nothing in the simulation depends on the pick.
    /// </summary>
    public sealed class CharacterLookPicker : MonoBehaviour
    {
        [SerializeField] private GameObject[] _looks = System.Array.Empty<GameObject>();

        private void Awake()
        {
            if (_looks.Length == 0)
                return;
            Show(Random.Range(0, _looks.Length));
        }

        /// <summary>Shows look <paramref name="index"/> and hides the others.</summary>
        public void Show(int index)
        {
            for (var i = 0; i < _looks.Length; i++)
                if (_looks[i] != null)
                    _looks[i].SetActive(i == index);
        }
    }
}
