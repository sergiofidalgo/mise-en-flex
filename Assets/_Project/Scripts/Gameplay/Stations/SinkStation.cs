using Game.Data;
using System.Collections.Generic;
using Game.Simulation.Items;
using Game.Gameplay.Saving;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// The sink: drop a dirty plate in, wash it, and it comes out clean in your hands, to carry to the
    /// plate stack (decision 2026-11-08). Holding X with empty hands washes by hand, like every
    /// cleaning job (decision 2026-11-11): the chef stays put and circling the stick scrubs the plate
    /// (<see cref="WashGame"/>) until it's done, X is let go, or B backs out. One chef at a time, one
    /// plate at a time.
    /// </summary>
    public sealed class SinkStation : Interactable, IUsable, IStationPrompt, ISavedContents
    {
        private KitchenServices _services;
        private PlayerInteractor _washer;

        public Sink Sink { get; private set; }

        /// <summary>The game being played, or null when nobody's washing by hand.</summary>
        public WashGame Game { get; private set; }

        /// <summary>The chef washing by hand, or null.</summary>
        public PlayerInteractor Washer => _washer;

        public override IItemContainer Container => Sink;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            Sink = new Sink();
            Sink.Received += HandleReceived;
        }

        public IEnumerable<(int Slot, Item Item)> SavedItems
        {
            get
            {
                if (Sink?.Washing != null)
                    yield return (0, Sink.Washing);
            }
        }

        public void ClearForLoad()
        {
        }

        public bool RestoreItem(int slot, Item item)
        {
            if (!Sink.CanAccept(item))
                return false;
            Sink.Put(item);
            return true;
        }

        private void OnDestroy()
        {
            if (Sink != null)
                Sink.Received -= HandleReceived;
        }

        // Plates in the sink are shown by its view, not each as an item.
        private void HandleReceived(Plate plate) => _services.ItemViews.Release(plate);

        public bool TryUse(PlayerInteractor player)
        {
            if (_washer != null || Sink.DirtyCount == 0 || !player.Hands.IsEmpty)
                return false;

            _washer = player;
            _washer.IsBusy = true;
            _washer.BackOut = StopWashing;
            // The minigame complexity dial bends the balance game (decision 2026-11-10).
            var realism = _services.Realism;
            Game = new WashGame(global::Game.Simulation.Realism.MinigameComplexity.Scale(_services.Tuning.WashGame, realism.MinigameComplexity, realism.Complexity));
            return true;
        }

        private void Update()
        {
            if (Game == null)
                return;

            // Their chef let go of X, left the game, the phase ended, or a helper finished the plate.
            if (_washer == null || !_washer.IsUseHeld || !_washer.CanAct || Sink.DirtyCount == 0)
            {
                StopWashing();
                return;
            }

            var stick = _washer.MoveInput;
            var washed = Game.Tick(UnityEngine.Time.deltaTime, new System.Numerics.Vector2(stick.x, stick.y));
            var clean = Sink.Wash(washed);
            if (clean == null)
                return;

            // Straight into the washer's hands, ready to take to the stack.
            var washer = _washer;
            StopWashing();
            washer.ReceiveItem(clean, ItemAnchor.position);
        }

        private void StopWashing()
        {
            if (_washer != null)
            {
                _washer.IsBusy = false;
                _washer.BackOut = null;
            }
            _washer = null;
            Game = null;
        }

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            // While washing, no card (decision 2026-11-09): the ring on the water is the interface,
            // an animated stick shows the circling when the player seems stuck, and letting go of X,
            // or B, stops, as everywhere.
            if (_washer == player)
                return;

            var held = player.Hands.Current;
            if (_washer != null)
                lines.Add(new PromptLine(PromptControl.West, Tr.Get("busy"), false));
            else if (Sink.DirtyCount > 0)
                lines.Add(new PromptLine(PromptControl.West, Tr.Get("wash"), held == null));
            else
                lines.Add(new PromptLine(PromptControl.West, Tr.Get("nothing to wash"), false));

            if (held != null)
                lines.Add(new PromptLine(PromptControl.South, Sink.HasRoom ? Tr.Get("put in sink") : Tr.Get("sink is busy"), Sink.CanAccept(held)));
        }
    }
}
