using Game.Simulation.Service;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game.Gameplay
{
    /// <summary>
    /// Runs one service from the players' side: the kitchen is live but closed until someone
    /// presses Pause (Menu on a controller, Esc on the keyboard), and once the service has
    /// finished the same button plays it again by reloading the scene (decision 2026-11-03).
    /// </summary>
    public sealed class ServiceLoop : MonoBehaviour
    {
        private const string PauseAction = "Pause";

        [SerializeField] private KitchenCompositionRoot _root;

        // Menu also joins: a player joining with it shouldn't open the restaurant in the same press.
        private bool _hadPlayersLastFrame;

        private void Update()
        {
            if (_hadPlayersLastFrame && AnyonePressedPause())
            {
                if (!_root.ServiceStarted)
                {
                    _root.StartService();
                }
                else if (_root.Services.Service.Phase == ServicePhase.Finished)
                {
                    PlayerRejoin.Remember();
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
            }

            _hadPlayersLastFrame = PlayerInput.all.Count > 0;
        }

        private static bool AnyonePressedPause()
        {
            foreach (var player in PlayerInput.all)
            {
                var action = player.actions.FindAction(PauseAction);
                if (action != null && action.WasPressedThisFrame())
                    return true;
            }
            return false;
        }
    }
}
