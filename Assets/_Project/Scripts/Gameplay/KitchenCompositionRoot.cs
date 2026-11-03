using Game.Data;
using Game.Gameplay.Items;
using Game.Gameplay.Stations;
using System.Linq;
using Game.Simulation.Cooking;
using Game.Simulation.Service;
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
        [SerializeField] private ServiceConfig _serviceConfig;
        [SerializeField] private PlayerInputManager _playerInputManager;

        private Interactable[] _interactables;

        /// <summary>Available from Awake; presentation (e.g. the debug overlay) reads it.</summary>
        public KitchenServices Services { get; private set; }

        public bool ServiceStarted { get; private set; }

        private void Awake()
        {
            // TODO(Phase 4): stations will be placed and moved during Plan, so they must register
            // and unregister themselves instead of being collected once here.
            _interactables = FindObjectsByType<Interactable>(FindObjectsSortMode.None);

            // One stool per pass counter (counter service, decision 2026-11-02).
            var seats = _interactables.OfType<PassStation>().Count();
            var cooking = new CookingSimulation(_tuning.AmbientTemperature, _realism.SpoilageMultiplier);
            var service = new ServiceSimulation(_serviceConfig.ToSettings(), seats, _realism.CustomerPatience);
            Services = new KitchenServices(cooking, new ItemViewRegistry(_itemCatalog), _itemCatalog, _equipment, _tuning, _realism, service, _serviceConfig);

            foreach (var interactable in _interactables)
                interactable.Bind(Services);
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
            Services.Service.PatienceMultiplier = _realism.CustomerPatience;
            var gameSeconds = Time.deltaTime * _tuning.TimeScale;
            cooking.Tick(gameSeconds);
            if (ServiceStarted)
                Services.Service.Tick(gameSeconds);
        }

        /// <summary>Opens the restaurant. Until then the kitchen is live, so players can preheat and prep.</summary>
        public void StartService() => ServiceStarted = true;

        private void BindPlayer(PlayerInput player)
        {
            if (player.TryGetComponent(out PlayerInteractor interactor))
                interactor.Bind(_interactables, Services);
        }
    }
}
