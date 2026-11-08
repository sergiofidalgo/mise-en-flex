using Game.Data;
using Game.Gameplay.Items;
using Game.Gameplay.Saving;
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
        [SerializeField] private FloorPlanConfig _floorPlan;
        [Tooltip("Rebuilds furniture bought in earlier days when a save is loaded.")]
        [SerializeField] private FurnitureCatalog _furniture;
        [SerializeField] private PlayerInputManager _playerInputManager;

        /// <summary>Available from Awake; presentation (e.g. the debug overlay) reads it.</summary>
        public KitchenServices Services { get; private set; }

        /// <summary>True once today's service has opened.</summary>
        public bool ServiceStarted => Services.Days.Phase == DayPhase.Service || Services.Days.Phase == DayPhase.Review;

        private void Awake()
        {
            // Continuing: the furniture goes where it was before anything binds (decision 2026-11-08).
            var save = CampaignLaunch.WantsSave ? SaveFile.Read() : null;
            var placed = save != null ? CampaignSnapshot.ApplyLayout(save, FindStations(), _furniture) : null;
            var stations = new StationRegistry(FindStations());

            var cooking = new CookingSimulation(_tuning.AmbientTemperature, _realism.SpoilageMultiplier);
            var days = save != null ? DayCycle.Continuing(save.Day, save.Balance) : new DayCycle(_staffConfig.StartingMoney);
            days.PhaseChanged += HandlePhaseChanged;
            var service = NewService(days.Day);
            Services = new KitchenServices(cooking, new ItemViewRegistry(_itemCatalog), _itemCatalog, _equipment, _tuning, _realism, service, _serviceConfig, days, new StaffRoster(), _staffConfig, stations, _floorPlan);
            Services.Staff.Candidates = _staffConfig.CandidatesFor(days.Day);

            foreach (var station in stations.All)
                station.Bind(Services);

            if (save != null)
            {
                CampaignSnapshot.RestoreContents(save, placed, Services);
                ReplaceBrokenPlates(); // the morning after the save, as if the night had just passed
            }

            // First launch of the session with a campaign on disk: the title card asks first.
            if (!CampaignLaunch.Decided)
            {
                var onDisk = SaveFile.Read();
                if (onDisk != null)
                    SavedDay = onDisk.Day;
                else
                    CampaignLaunch.StartNew();
            }
        }

        private void Start()
        {
            if (AwaitingLaunchChoice)
                _playerInputManager.DisableJoining();
        }

        /// <summary>True while the title card is up: nobody can join until Continue or New campaign is chosen.</summary>
        public bool AwaitingLaunchChoice => !CampaignLaunch.Decided;

        /// <summary>The day the saved campaign last played, for the title card.</summary>
        public int SavedDay { get; private set; }

        /// <summary>Loads the saved campaign: the scene starts again and puts the save back as it does.</summary>
        public void ChooseContinue()
        {
            CampaignLaunch.Continue();
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
        }

        /// <summary>Starts over in the fresh kitchen already here. The old file is kept aside, not read again.</summary>
        public void ChooseNewCampaign()
        {
            CampaignLaunch.StartNew();
            SaveFile.Retire();
            _playerInputManager.EnableJoining();
        }

        /// <summary>True if quitting now would lose something: anything since Review started is unsaved.</summary>
        public bool HasUnsavedDay => CampaignLaunch.Decided && Services.Days.Phase != DayPhase.Review;

        /// <summary>The one campaign file (decision 2026-11-06: no slots).</summary>
        public SaveFile SaveFile => _saveFile ??= new SaveFile();

        private SaveFile _saveFile;

        /// <summary>Saves the restaurant as it stands. Called as Review starts, once the day's results are in.</summary>
        public void SaveCampaign()
        {
            var players = PlayerInput.all.Select(p => p.GetComponent<PlayerInteractor>()).Where(p => p != null);
            var save = CampaignSnapshot.Capture(Services, players);
            try
            {
                SaveFile.Write(save);
                Services.ReviewHistory.Clear();
                Services.ReviewHistory.AddRange(save.Reviews);
            }
            catch (System.Exception exception)
            {
                // A failed save mustn't stop the day; the last good file is still there.
                Debug.LogError($"Couldn't save the campaign: {exception.Message}");
            }
        }

        private static System.Collections.Generic.List<Interactable> FindStations() =>
            FindObjectsByType<Interactable>(FindObjectsSortMode.InstanceID).Where(station => !(station is StreetDoor)).ToList();

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
                cooking.Tick(gameSeconds * _tuning.CookingSpeed);
            if (phase == DayPhase.Service)
                Services.Service.Tick(gameSeconds);
        }

        private ServiceSimulation NewService(int day) =>
            new ServiceSimulation(_serviceConfig.ToSettings(day), 0, _realism.CustomerPatience); // seats open with the doors

        /// <summary>
        /// Closes up after a service: the night passes, stored food too old to keep gets thrown out,
        /// and food left out stays where it is, aged at room temperature.
        /// </summary>
        public CloseReport CloseKitchen()
        {
            var cooking = Services.Cooking;
            cooking.SkipClosedTime(_tuning.OvernightSeconds);

            var spoiled = 0;
            foreach (var storage in Services.Stations.All.OfType<StorageStation>())
            {
                foreach (var ingredient in storage.Unit.RemoveSpoiled(_tuning.SpoiledBelow))
                {
                    cooking.Remove(ingredient);
                    spoiled++;
                }
            }

            // Left out: neither in storage nor put away in a container.
            var leftOut = cooking.Ingredients.Count(food => food.EnvironmentTemperature == null && food.FreshnessFactor >= 1f);
            return new CloseReport(spoiled, leftOut);
        }

        private void HandlePhaseChanged(DayPhase phase)
        {
            if (phase == DayPhase.Service)
            {
                OpenSeats();
                return;
            }
            if (phase == DayPhase.Prep)
            {
                Services.LastDelivery = Services.Stations.All.OfType<StorageStation>().SelectMany(s => s.TakeDelivery()).ToList();
                return;
            }
            if (phase != DayPhase.Plan)
                return;

            Services.ReplaceService(NewService(Services.Days.Day));
            Services.Staff.Candidates = _staffConfig.CandidatesFor(Services.Days.Day);

            ReplaceBrokenPlates();
        }

        // TODO(Phase 5, milestone 2): broken plates get replaced for free each morning until they can be bought.
        private void ReplaceBrokenPlates()
        {
            var stack = Services.Stations.All.OfType<PlateStackStation>().FirstOrDefault();
            var sink = Services.Stations.All.OfType<SinkStation>().FirstOrDefault();
            if (stack != null && sink != null)
                stack.Stack.AddNew(sink.Sink.TakeBrokenCount(), _equipment.PlateCapacity);
        }

        // Doors open: the seating is whatever tables there are by now.
        private void OpenSeats()
        {
            var tables = new System.Collections.Generic.List<int>();
            var next = 0;
            foreach (var table in Services.Stations.All.OfType<TableStation>())
            {
                next = table.AssignSeats(next);
                tables.Add(table.SeatCount);
            }
            Services.Service.OpenSeats(tables);
        }

        private void BindPlayer(PlayerInput player)
        {
            if (player.TryGetComponent(out PlayerInteractor interactor))
                interactor.Bind(Services.Stations.All, Services);
            if (player.TryGetComponent(out PlayerMovement movement))
                movement.FindFreeSpot = point => Services.NewLayout().NearestFree(point);
        }
    }
}
