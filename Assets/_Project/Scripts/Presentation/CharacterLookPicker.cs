using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows one of a person's prebuilt looks, picked at random when they appear, so a dining room
    /// fills with different people from one prefab (concept v9.2: the archetype is the clothes,
    /// the person is rolled). Each look also has its own walking pace (a retiree is slower), which
    /// changes how long they take to reach a seat; nothing else in the simulation depends on the pick.
    /// </summary>
    public sealed class CharacterLookPicker : MonoBehaviour
    {
        [SerializeField] private GameObject[] _looks = System.Array.Empty<GameObject>();
        [Tooltip("Each look's walking pace relative to the usual speed, in the same order: part of the character.")]
        [SerializeField] private float[] _paces = System.Array.Empty<float>();

        /// <summary>How many looks there are.</summary>
        public int Count => _looks.Length;

        /// <summary>The look shown now.</summary>
        public int Shown { get; private set; } = -1;

        private void Awake()
        {
            if (_looks.Length == 0)
                return;
            Show(Random.Range(0, _looks.Length));
        }

        // A customer wears the look the simulation gave them, which their review card shows
        // (2026-11-11). Agents get their customer just after they're made, so this waits for Start.
        private void Start()
        {
            if (TryGetComponent<Game.Gameplay.Customers.CustomerAgent>(out var agent) && agent.Customer != null && agent.Customer.Look >= 0)
                Show(agent.Customer.Look % Mathf.Max(1, _looks.Length));
        }

        /// <summary>Shows look <paramref name="index"/> and hides the others, and walks at its pace.</summary>
        public void Show(int index)
        {
            Shown = index;
            for (var i = 0; i < _looks.Length; i++)
                if (_looks[i] != null)
                    _looks[i].SetActive(i == index);
            if (index < _paces.Length && TryGetComponent<Game.Gameplay.WalkingAgent>(out var walker))
                walker.SpeedFactor = _paces[index];
        }
    }
}
