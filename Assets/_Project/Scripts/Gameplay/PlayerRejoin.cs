using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay
{
    /// <summary>
    /// Remembers who was playing across a scene reload, so restarting a service doesn't make
    /// everyone join again. Static because nothing else survives the reload; it only holds data
    /// between Remember and the next scene's Consume.
    /// </summary>
    public static class PlayerRejoin
    {
        private static readonly List<(string Scheme, InputDevice[] Devices)> Players = new List<(string, InputDevice[])>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Players.Clear();

        public static void Remember()
        {
            Players.Clear();
            foreach (var player in PlayerInput.all.OrderBy(p => p.playerIndex))
                Players.Add((player.currentControlScheme, player.devices.ToArray()));
        }

        /// <summary>Joins the remembered players on <paramref name="manager"/>, in their old order.</summary>
        public static void Consume(PlayerInputManager manager)
        {
            foreach (var (scheme, devices) in Players)
                manager.JoinPlayer(controlScheme: scheme, pairWithDevices: devices);
            Players.Clear();
        }
    }
}
