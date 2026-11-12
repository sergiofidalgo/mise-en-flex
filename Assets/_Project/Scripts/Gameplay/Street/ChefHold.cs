using System.Collections.Generic;
using Game.Gameplay.Stations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Street
{
    /// <summary>
    /// Taking a chef out of the player's hands for a moment (thrown by a car, carried by a
    /// bodyguard) and giving them back. While held, their movement and their body are switched
    /// off, so whoever holds them moves them by hand; a chef whose movement is off is somebody's
    /// already, and nothing else takes them.
    /// </summary>
    public static class ChefHold
    {
        private static readonly List<PlayerInteractor> Found = new List<PlayerInteractor>();

        /// <summary>Every chef in the scene now.</summary>
        public static IReadOnlyList<PlayerInteractor> Chefs()
        {
            Found.Clear();
            foreach (var player in PlayerInput.all)
                if (player != null && player.TryGetComponent(out PlayerInteractor chef))
                    Found.Add(chef);
            return Found;
        }

        /// <summary>True if the chef is the player's to move: nobody holds them.</summary>
        public static bool IsFree(PlayerInteractor chef) =>
            chef != null && chef.TryGetComponent(out PlayerMovement movement) && movement.enabled && movement.IsReady;

        public static void Take(PlayerInteractor chef)
        {
            chef.GetComponent<PlayerMovement>().enabled = false;
            if (chef.TryGetComponent(out CharacterController body))
                body.enabled = false;
            chef.IsBusy = true;
        }

        /// <summary>Stands them on the floor at a point, facing a way, and hands them back.</summary>
        public static void Release(PlayerInteractor chef, System.Numerics.Vector2 at, Vector3 facing)
        {
            var movement = chef.GetComponent<PlayerMovement>();
            facing.y = 0f;
            if (facing.sqrMagnitude > 1e-6f)
                chef.transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            if (chef.TryGetComponent(out CharacterController body))
                body.enabled = true;
            movement.enabled = true;
            movement.Teleport(at);
            chef.IsBusy = false;
        }
    }
}
