using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Gameplay.CloseUp
{
    /// <summary>
    /// Gives a station a close-up: owns one instance of its technique prefab and lets one
    /// player at a time use it (decision 2026-11-02).
    /// </summary>
    public sealed class CloseUpStation : MonoBehaviour
    {
        [SerializeField] private Interactable _station;

        [Tooltip("Prefab with a component implementing ICloseUpTechnique and its own camera.")]
        [SerializeField] private GameObject _techniquePrefab;

        public Interactable Station => _station;

        public ICloseUpTechnique Technique { get; private set; }

        /// <summary>The player currently in this close-up, or null.</summary>
        public PlayerCloseUp Occupant { get; private set; }

        public void Bind(KitchenServices services)
        {
            var instance = Instantiate(_techniquePrefab, transform);
            instance.name = _techniquePrefab.name;
            Technique = instance.GetComponent<ICloseUpTechnique>();
            if (Technique == null)
            {
                Debug.LogError($"[CloseUpStation] {_techniquePrefab.name} has no ICloseUpTechnique component.", this);
                return;
            }

            Technique.Setup(_station, services);
        }

        internal bool TryClaim(PlayerCloseUp player)
        {
            if (Technique == null || (Occupant != null && Occupant != player))
                return false;
            Occupant = player;
            return true;
        }

        internal void Release(PlayerCloseUp player)
        {
            if (Occupant == player)
                Occupant = null;
        }
    }
}
