using System.Collections.Generic;

namespace Game.Simulation.Days
{
    /// <summary>
    /// What happens when a player's controller goes (decision 2026-11-09). During a service the
    /// game waits for it: everything stands still until it's back, or until someone still playing
    /// chooses to carry on without them. Outside service nothing waits; the ready-up just leaves
    /// them out (<see cref="ReadyCheck.IsEveryoneReady(IReadOnlyCollection{int})"/>). Whoever's
    /// been carried on without is waited for again only if they come back and drop out again.
    /// </summary>
    public sealed class ControllerPause
    {
        private readonly HashSet<int> _lost = new HashSet<int>();
        private readonly HashSet<int> _excused = new HashSet<int>();
        private readonly List<int> _waitingFor = new List<int>();

        /// <summary>True while the service waits for a lost controller.</summary>
        public bool Paused => _waitingFor.Count > 0;

        /// <summary>The players being waited for, lowest slot first.</summary>
        public IReadOnlyList<int> WaitingFor => _waitingFor;

        public bool IsLost(int player) => _lost.Contains(player);

        /// <summary>Takes in who's lost their controller this frame, and whether a service is on.</summary>
        public void Update(IEnumerable<(int Player, bool Lost)> players, bool inService)
        {
            _lost.Clear();
            foreach (var (player, lost) in players)
            {
                if (lost)
                    _lost.Add(player);
                else
                    _excused.Remove(player); // back: a later drop is waited for again
            }

            _waitingFor.Clear();
            if (!inService)
                return;
            foreach (var player in _lost)
                if (!_excused.Contains(player))
                    _waitingFor.Add(player);
            _waitingFor.Sort();
        }

        /// <summary>Someone still playing chose to carry on without everyone being waited for.</summary>
        public void CarryOn()
        {
            foreach (var player in _waitingFor)
                _excused.Add(player);
            _waitingFor.Clear();
        }
    }
}
