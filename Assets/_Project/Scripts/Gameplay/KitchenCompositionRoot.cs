using Game.Data;
using Game.Gameplay.Items;
using Game.Gameplay.Stations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay
{
    /// <summary>
    /// Creates the kitchen scene's shared services and hands them to whatever needs them,
    /// instead of singletons. Players get theirs as they join.
    /// </summary>
    public sealed class KitchenCompositionRoot : MonoBehaviour
    {
        [SerializeField] private ItemCatalog _itemCatalog;
        [SerializeField] private TuningConfig _tuning;
        [SerializeField] private PlayerInputManager _playerInputManager;

        private ItemViewRegistry _itemViews;
        private Interactable[] _interactables;

        private void Awake()
        {
            _itemViews = new ItemViewRegistry(_itemCatalog);
            _interactables = FindObjectsByType<Interactable>(FindObjectsSortMode.None);
        }

        private void OnEnable()
        {
            _playerInputManager.onPlayerJoined += BindPlayer;
        }

        private void OnDisable()
        {
            _playerInputManager.onPlayerJoined -= BindPlayer;
        }

        private void BindPlayer(PlayerInput player)
        {
            if (player.TryGetComponent(out PlayerInteractor interactor))
                interactor.Bind(_interactables, _itemViews, _tuning);
        }
    }
}
