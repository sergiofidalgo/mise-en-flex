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
        [Tooltip("The recipes on offer and how price moves customers. Without it, the service config's single dish is the menu.")]
        [SerializeField] private CookbookConfig _cookbook;
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
            // Opened straight from the Editor, with no menu: carry on with the last campaign.
            if (!CampaignLaunch.Decided)
                CampaignLaunch.ChooseForDirectPlay();
            PlayerSettings.Load(_realism).ApplyTo(_realism);

            // Continuing: the furniture goes where it was before anything binds (decision 2026-11-08).
            var save = CampaignLaunch.WantsSave ? SaveFile.Read() : null;
            _loaded = save;
            var placed = save != null ? CampaignSnapshot.ApplyLayout(save, FindStations(), _furniture) : null;
            var stations = new StationRegistry(FindStations());

            var cooking = new CookingSimulation(_tuning.AmbientTemperature, _realism.SpoilageMultiplier);
            var fee = _serviceConfig.ToFeeSettings();
            var days = save != null
                ? DayCycle.Continuing(save.Day, save.Balance, fee, save.DaysTowardsFee, Ledger.FromArray(save.Period), save.History)
                : new DayCycle(_staffConfig.StartingMoney, fee);
            days.PhaseChanged += HandlePhaseChanged;
            _reviewLines.Load(save?.RecentLines);
            var service = NewService(days.Day);
            Services = new KitchenServices(cooking, new ItemViewRegistry(_itemCatalog), _itemCatalog, _equipment, _tuning, _realism, service, _serviceConfig, days, new StaffRoster(), _staffConfig, stations, _floorPlan);
            Services.Staff.Candidates = _staffConfig.CandidatesFor(days.Day);
            Services.ReviewLines = _reviewLines;
            Services.Cookbook = NewCookbook(save);

            foreach (var station in stations.All)
                station.Bind(Services);

            if (save != null)
                CampaignSnapshot.RestoreContents(save, placed, Services);
            // Reviews saved as English sentences are traced to their templates once, to show in any
            // language; the next save keeps what was found (decision 2026-11-09).
            ReviewTextRecovery.Recover(Services.ReviewHistory, _serviceConfig.StyleVoice(), _serviceConfig.GenericVoice());
            // Who might come back today, now the history is loaded.
            Services.Service.Settings.Reviews.Guests?.Rebuild(Services.ReviewHistory, days.Day);
        }

        // This campaign's recipes and prices; whatever it unlocks joins the shared cookbook too.
        private Game.Simulation.Cookbook.RecipeBook NewCookbook(Game.Simulation.Saving.CampaignSave save)
        {
            if (_cookbook == null)
                return null;
            var book = _cookbook.NewBook(_itemCatalog);
            if (save != null)
                book.Load(save.Recipes, save.Prices);
            foreach (var id in book.UnlockedIds())
                SharedCookbookFile.Add(id);
            book.Unlocked += recipe => SharedCookbookFile.Add(recipe.Id);
            return book;
        }

        // Review and reply lines used lately, across days and sessions, so words don't repeat.
        private readonly LineMemory _reviewLines = new LineMemory();

        // The campaign as it was loaded, for what the card keeps across sessions (play time, cooks).
        private Game.Simulation.Saving.CampaignSave _loaded;
        private float _sessionSeconds;

        /// <summary>The slot this campaign is saved in.</summary>
        public int Slot => CampaignLaunch.Slot;

        /// <summary>Posts a reply under a review, once, and saves it straight away.</summary>
        public bool ReplyTo(Game.Simulation.Saving.ReviewRecord review, ReviewReply reply)
        {
            var settings = _serviceConfig.ToReplySettings();
            if (!ReviewReplies.Reply(review, reply, settings))
                return false;
            // Written for this review in the style's voice, in the mood the day has left the owner in:
            // each reply already posted today and each walk-out tonight wears on them (decision 2026-11-09).
            var day = Services.Days.Day;
            var repliedToday = Services.ReviewHistory.Count(r => r.Day == day && r.Reply >= 0) - 1;
            var mood = ReviewVoices.MoodAfter(repliedToday, Services.Days.LastService?.WalkOuts ?? 0, settings.TiredAfter, settings.FedUpAfter);
            ReviewReplies.WriteReply(review, reply, _serviceConfig.StyleVoice(), _serviceConfig.GenericVoice(), _reviewLines, _serviceConfig.RestaurantName, mood, GuestNames.FirstOf(review));
            SaveCampaign();
            return true;
        }

        /// <summary>Today's reviews join the listing as the day ends, before it's saved; today's orders join the tally.</summary>
        public void ListTodaysReviews()
        {
            foreach (var customer in Services.Service.Customers)
            {
                if (customer.Order == null || (customer.Satisfaction == null && !customer.WalkedOut))
                    continue;
                var dish = Game.Simulation.Saving.SaveConversions.OrderName(customer.Order);
                Services.DishTally[dish] = Services.DishTally.TryGetValue(dish, out var count) ? count + 1 : 1;
            }
            var day = Services.Days.Day;
            foreach (var review in Services.Service.Reviews)
            {
                var id = review.Customer?.Id ?? 0;
                var (slot, initial) = _serviceConfig.GuestSlot(id, day);
                Services.ReviewHistory.Add(Game.Simulation.Saving.SaveConversions.ToRecord(review, day, _serviceConfig.GuestName(id, day), slot, initial));
            }
        }

        /// <summary>True if quitting now would lose something: anything since Review started is unsaved.</summary>
        public bool HasUnsavedDay => CampaignLaunch.Decided && Services.Days.Phase != DayPhase.Review;

        /// <summary>This campaign's file, in its slot (decision 2026-11-08: three slots).</summary>
        public SaveFile SaveFile => _saveFile ??= SaveSlots.File(CampaignLaunch.Slot);

        private SaveFile _saveFile;

        /// <summary>Saves the restaurant as it stands. Called as Review starts, once the day's results are in.</summary>
        public void SaveCampaign()
        {
            var players = PlayerInput.all.Select(p => p.GetComponent<PlayerInteractor>()).Where(p => p != null);
            var save = CampaignSnapshot.Capture(Services, players);

            // What the slot card shows in the main menu.
            save.RestaurantName = _serviceConfig.RestaurantName;
            save.StyleName = _serviceConfig.StyleName;
            save.Area = _serviceConfig.Area;
            save.PlayedSeconds = (_loaded?.PlayedSeconds ?? 0f) + _sessionSeconds;
            save.LastPlayedTicks = System.DateTime.Now.Ticks;
            save.Cooks = (_loaded?.Cooks ?? new System.Collections.Generic.List<int>())
                .Concat(PlayerInput.all.Select(p => p.playerIndex)).Distinct().OrderBy(i => i).ToList();
            try
            {
                SaveFile.Write(save);
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
            _sessionSeconds += Time.unscaledDeltaTime;

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

        private ServiceSimulation NewService(int day)
        {
            var settings = _serviceConfig.ToSettings(day);
            settings.Reviews.Memory = _reviewLines;
            // Guests who posted on earlier days might come back (built again once a save is loaded).
            if (Services != null)
                settings.Reviews.Guests?.Rebuild(Services.ReviewHistory, day);
            return new ServiceSimulation(settings, 0, _realism.CustomerPatience); // seats open with the doors
        }

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
                // Prices can change right up to opening, so the menu is set as the doors open.
                if (Services.Cookbook != null && Services.Service.Customers.Count == 0)
                    Services.Service.SetMenu(Services.Cookbook.BuildMenu());
                OpenSeats();
                return;
            }
            if (phase == DayPhase.Prep)
            {
                // Street vendors replace the morning delivery (decision 2026-11-10): a new campaign's
                // first morning finds the storeroom stocked, free and once. After that it's bought.
                Services.LastDelivery = Services.Days.Day == 1
                    ? Services.Stations.All.OfType<StorageStation>().SelectMany(s => s.StockStarter()).ToList()
                    : new System.Collections.Generic.List<(string, int)>();
                return;
            }
            if (phase != DayPhase.Plan)
                return;

            Services.ReplaceService(NewService(Services.Days.Day));
            Services.Staff.Candidates = _staffConfig.CandidatesFor(Services.Days.Day);
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
            {
                movement.FindFreeSpot = point => Services.NewLayout().NearestFree(point);
                movement.StepClear(); // off any furniture a saved layout put over their spawn point
            }
        }
    }
}
