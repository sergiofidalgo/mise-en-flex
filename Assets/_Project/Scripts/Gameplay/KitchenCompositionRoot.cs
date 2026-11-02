using Game.Data;
using Game.Gameplay.CloseUp;
using Game.Gameplay.Items;
using Game.Gameplay.Stations;
using Game.Simulation.Cooking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay
{
    /// <summary>
    /// Creates the kitchen scene's shared services and hands them to whatever needs them,
    /// instead of singletons. Also runs the cooking simulation on the game clock.
    /// </summary>
    public sealed class KitchenCompositionRoot : MonoBehaviour
    {
        [SerializeField] private ItemCatalog _itemCatalog;
        [SerializeField] private TuningConfig _tuning;
        [SerializeField] private EquipmentTier _equipment;
        [SerializeField] private RealismSettings _realism;
        [SerializeField] private PlayerInputManager _playerInputManager;

        private Interactable[] _interactables;

        /// <summary>Available from Awake; presentation (e.g. the debug overlay) reads it.</summary>
        public KitchenServices Services { get; private set; }

        private void Awake()
        {
            var cooking = new CookingSimulation(_tuning.AmbientTemperature, _realism.SpoilageMultiplier);
            Services = new KitchenServices(cooking, new ItemViewRegistry(_itemCatalog), _itemCatalog, _equipment, _tuning);

            // TODO(Phase 4): stations will be placed and moved during Plan, so they must register
            // and unregister themselves instead of being collected once here.
            _interactables = FindObjectsByType<Interactable>(FindObjectsSortMode.None);
            foreach (var interactable in _interactables)
                interactable.Bind(Services);
            foreach (var closeUp in FindObjectsByType<CloseUpStation>(FindObjectsSortMode.None))
                closeUp.Bind(Services);
        }

        private void OnEnable()
        {
            _playerInputManager.onPlayerJoined += BindPlayer;
        }

        private void OnDisable()
        {
            _playerInputManager.onPlayerJoined -= BindPlayer;
        }

        private void Update()
        {
            // Dials are read every frame so Inspector edits apply live.
            var cooking = Services.Cooking;
            cooking.AmbientTemperature = _tuning.AmbientTemperature;
            cooking.SpoilageMultiplier = _realism.SpoilageMultiplier;
            cooking.Tick(Time.deltaTime * _tuning.TimeScale);
        }

        private void BindPlayer(PlayerInput player)
        {
            if (player.TryGetComponent(out PlayerInteractor interactor))
                interactor.Bind(_interactables, Services);
            if (player.TryGetComponent(out PlayerCloseUp closeUp))
                closeUp.Bind(Services);
        }
    }
}
