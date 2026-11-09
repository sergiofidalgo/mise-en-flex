using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

namespace Game.Gameplay
{
    /// <summary>
    /// Who's playing, carried from one scene to the next (decision 2026-11-09): the office's players
    /// walk into the kitchen as the same players, on the same controllers and in the same order (so
    /// the same colours and chefs), instead of joining again. Scenes come and go; input devices
    /// don't, so the roster keeps the devices and each player's control scheme.
    /// </summary>
    public static class PlayerRoster
    {
        /// <summary>One player: their control scheme and the devices paired to them.</summary>
        public readonly struct Seat
        {
            public readonly int Index;
            public readonly string Scheme;
            public readonly InputDevice[] Devices;

            public Seat(int index, string scheme, InputDevice[] devices)
            {
                Index = index;
                Scheme = scheme;
                Devices = devices;
            }
        }

        private static readonly List<Seat> Carried = new List<Seat>();

        /// <summary>Remembers these players for the next scene, replacing anyone remembered before.</summary>
        public static void Carry(IEnumerable<Seat> seats)
        {
            Carried.Clear();
            Carried.AddRange(seats.OrderBy(seat => seat.Index));
        }

        /// <summary>Remembers everyone playing now, for the next scene.</summary>
        public static void CarryEveryone() =>
            Carry(PlayerInput.all.Select(player => new Seat(player.playerIndex, player.currentControlScheme, player.devices.ToArray())));

        /// <summary>
        /// The players to join again, in their old order, once: the list empties. Anyone whose
        /// controller has gone (switched off, out of battery) is left out, to join as usual.
        /// </summary>
        public static List<Seat> Take()
        {
            var seats = Carried.Where(seat => seat.Devices.Length > 0 && seat.Devices.All(device => device != null && device.added)).ToList();
            Carried.Clear();
            return seats;
        }
    }
}
