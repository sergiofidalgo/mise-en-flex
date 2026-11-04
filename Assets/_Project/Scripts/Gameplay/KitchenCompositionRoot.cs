using Game.Data;
using Game.Gameplay.Items;
using Game.Gameplay.Stations;
using System.Linq;
using Game.Simulation.Cooking;
using Game.Simulation.Days;
using Game.Simulation.Service;
using Game.Simulation.Staff;
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
        [SerializeField] private StaffConfig _staffConfig;
        [SerializeField] private PlayerInputManager _playerInputManager;

        private Interactable[] _interactables;

        /// <summary>Available from Awake; presentation (e.g. the debug overlay) reads it.</summary>
        public KitchenServices Services { get; private set; }

        /// <summary>True once today's service has opened.</summary>
        public bool ServiceStarted => Services.Days.Phase == DayPhase.Service || Services.Days.Phase == DayPhase.Review;

        private int _seats;

        private void Awake()
        {
            // TODO(Phase 4): stations will be placed and moved during Plan, so they must register
            // and unregister themselves instead of being collected once here.
            _interactables = FindObjectsByType<Interactable>(FindObjectsSortMode.None);

            // One stool per pass counter (counter service, decision 2026-11-02).
            var seats = _interactables.OfType<PassStation>().Count();
            var cooking = new CookingSimulation(_tuning.AmbientTemperature, _realism.SpoilageMultiplier);
            _seats = seats;
            var days = new DayCycle(_staffConfig.StartingMoney);
            days.PhaseChanged += HandlePhaseChanged;
            var service = NewService(days.Day);
            Services = new KitchenServices(cooking, new ItemViewRegistry(_itemCatalog), _itemCatalog, _equipment, _tuning, _realism, service, _serviceConfig, days, new StaffRoster(), _staffConfig);
            Services.Staff.Candidates = _staffConfig.CandidatesFor(days.Day);

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

            // The kitchen is live while players work in it; Plan and Review happen between shifts.
            var phase = Services.Days.Phase;
            if (phase == DayPhase.Prep || phase == DayPhase.Service)
                cooking.Tick(gameSeconds);
            if (phase == DayPhase.Service)
                Services.Service.Tick(gameSeconds);
        }

        private ServiceSimulation NewService(int day) =>
            new ServiceSimulation(_serviceConfig.ToSettings(day), _seats, _realism.CustomerPatience);

        /// <summary>
        /// Closes up after a service: the night passes, stored food too old to keep gets thrown out,
        /// and food left out stays where it is, aged at room temperature.
        /// </summary>
        public CloseReport CloseKitchen()
        {
            var cooking = Services.Cooking;
            cooking.SkipClosedTime(_tuning.OvernightSeconds);

            var spoiled = 0;
            var stored = 0;
            foreach (var storage in _interactables.OfType<StorageStation>())
            {
                foreach (var ingredient in storage.Unit.RemoveSpoiled(_tuning.SpoiledBelow))
                {
                    cooking.Remove(ingredient);
                    spoiled++;
                }
                stored += storage.Unit.Slots.Sum(slot => slot.Count);
            }
            return new CloseReport(spoiled, cooking.Ingredients.Count - stored);
        }

        private void HandlePhaseChanged(DayPhase phase)
        {
            if (phase == DayPhase.Prep)
            {
                Services.LastDelivery = _interactables.OfType<StorageStation>().SelectMany(s => s.TakeDelivery()).ToList();
                return;
            }
            if (phase != DayPhase.Plan)
                return;

            Services.ReplaceService(NewService(Services.Days.Day));
            Services.Staff.Candidates = _staffConfig.CandidatesFor(Services.Days.Day);

            // TODO(Phase 5): broken plates get replaced for free each morning until they can be bought.
            var stack = _interactables.OfType<PlateStackStation>().FirstOrDefault();
            var sink = _interactables.OfType<SinkStation>().FirstOrDefault();
            if (stack != null && sink != null)
                stack.Stack.AddNew(sink.Sink.TakeBrokenCount(), _equipment.PlateCapacity);
        }

        private void BindPlayer(PlayerInput player)
        {
            if (player.TryGetComponent(out PlayerInteractor interactor))
                interactor.Bind(_interactables, Services);
        }
    }
}
