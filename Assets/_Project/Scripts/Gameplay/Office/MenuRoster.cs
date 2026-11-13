using System.Collections.Generic;

namespace Game.Gameplay.Office
{
    /// <summary>
    /// Who's in before the floor (the user's calls, 2026-11-13): player 1 is in from the start, as
    /// whichever controller presses first, and only they drive the main menu. Anyone else joins with
    /// A while the menu is up or the lift is climbing, and does nothing else there. Everyone in
    /// rides up in the glass lift and joins the floor, in slot order, as the camera lands. Devices
    /// are input device ids, so this is plain C# and tested without Play mode.
    /// </summary>
    public sealed class MenuRoster
    {
        private readonly List<int> _devices = new List<int>();
        private readonly int _max;

        public MenuRoster(int maxPlayers)
        {
            _max = maxPlayers < 1 ? 1 : maxPlayers;
        }

        /// <summary>Everyone in, by slot: index 0 is player 1.</summary>
        public IReadOnlyList<int> Devices => _devices;

        public int Count => _devices.Count;

        /// <summary>True once player 1 is known.</summary>
        public bool HasDriver => _devices.Count > 0;

        /// <summary>Player 1's device, or -1 before anyone has pressed anything.</summary>
        public int Driver => _devices.Count > 0 ? _devices[0] : -1;

        /// <summary>The slot <paramref name="device"/> plays in, or -1 if it isn't in.</summary>
        public int SlotOf(int device) => _devices.IndexOf(device);

        /// <summary>
        /// Player 1 is whoever acts first: <paramref name="device"/> becomes player 1 if nobody is
        /// yet. Returns true if it did.
        /// </summary>
        public bool Claim(int device)
        {
            if (HasDriver || device < 0)
                return false;
            _devices.Add(device);
            return true;
        }

        /// <summary>
        /// A press of A on <paramref name="device"/>. Before player 1 is known it claims player 1 (and
        /// returns 0); after that, while <paramref name="joiningOpen"/>, a device that isn't in yet
        /// takes the next free slot and the slot is returned. Otherwise -1: already in, joining
        /// closed, or every slot taken.
        /// </summary>
        public int Press(int device, bool joiningOpen)
        {
            if (device < 0)
                return -1;
            if (Claim(device))
                return 0;
            if (!joiningOpen || _devices.Contains(device) || _devices.Count >= _max)
                return -1;
            _devices.Add(device);
            return _devices.Count - 1;
        }

        /// <summary>A controller that's gone: it leaves, and those after it move up a slot.</summary>
        public bool Remove(int device) => _devices.Remove(device);
    }
}
