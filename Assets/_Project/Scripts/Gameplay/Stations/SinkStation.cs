using System.Collections.Generic;
using Game.Simulation.Items;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// The sink: drop dirty plates in, take clean ones back to the stack. Use starts washing by hand
    /// with empty hands: the chef stays put and the stick balances the point of the
    /// <see cref="WashGame"/> until they press Use again or nothing's left to wash. One chef at a time.
    /// </summary>
    public sealed class SinkStation : Interactable, IUsable, IStationPrompt
    {
        private KitchenServices _services;
        private PlayerInteractor _washer;
        private int _nextSeed;

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

        private void OnDestroy()
        {
            if (Sink != null)
                Sink.Received -= HandleReceived;
        }

        // Plates in the sink are shown by its view, not each as an item.
        private void HandleReceived(Plate plate) => _services.ItemViews.Release(plate);

        public bool TryUse(PlayerInteractor player)
        {
            if (_washer == player)
            {
                StopWashing();
                return true;
            }
            if (_washer != null || Sink.DirtyCount == 0 || !player.Hands.IsEmpty)
                return false;

            _washer = player;
            _washer.IsBusy = true;
            Game = new WashGame(_services.Tuning.WashGame, _nextSeed++);
            return true;
        }

        private void Update()
        {
            if (Game == null)
                return;

            // Their chef left the game, the phase ended, or someone finished the pile.
            if (_washer == null || !_washer.CanAct || Sink.DirtyCount == 0)
            {
                StopWashing();
                return;
            }

            var stick = _washer.MoveInput;
            var washed = Game.Tick(UnityEngine.Time.deltaTime, new System.Numerics.Vector2(stick.x, stick.y));
            if (Sink.Wash(washed) != null)
                Game.NewPlate();
        }

        private void StopWashing()
        {
            if (_washer != null)
                _washer.IsBusy = false;
            _washer = null;
            Game = null;
        }

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            if (_washer == player)
            {
                lines.Add(new PromptLine(PromptControl.West, "stop washing"));
                return;
            }

            var held = player.Hands.Current;
            if (_washer != null)
                lines.Add(new PromptLine(PromptControl.West, "busy", false));
            else if (Sink.DirtyCount > 0)
                lines.Add(new PromptLine(PromptControl.West, $"wash ({Sink.DirtyCount})", held == null));
            else
                lines.Add(new PromptLine(PromptControl.West, "nothing to wash", false));

            if (held != null)
                lines.Add(new PromptLine(PromptControl.South, "drop plate", Sink.CanAccept(held)));
            else if (Sink.CanTake)
                lines.Add(new PromptLine(PromptControl.South, $"take clean ({Sink.CleanCount})"));
        }
    }
}
