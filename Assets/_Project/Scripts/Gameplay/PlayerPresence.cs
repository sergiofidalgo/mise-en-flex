using UnityEngine.InputSystem;

namespace Game.Gameplay
{
    /// <summary>Whether a player still has their controller.</summary>
    public static class PlayerPresence
    {
        /// <summary>
        /// True when a device they were playing on has gone (switched off, out of battery, out of
        /// Bluetooth range). The Input System keeps the player and pairs the device again by itself
        /// when it comes back, so this turns false on its own.
        /// </summary>
        public static bool IsLost(PlayerInput player) =>
            player.hasMissingRequiredDevices || (player.user.valid && player.user.lostDevices.Count > 0);
    }
}
