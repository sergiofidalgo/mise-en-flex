using Game.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay
{
    /// <summary>
    /// Places players at spawn points as they join, and closes joining once the
    /// player limit from <see cref="CoopSettings"/> is reached.
    /// </summary>
    [RequireComponent(typeof(PlayerInputManager))]
    public sealed class PlayerJoinController : MonoBehaviour
    {
        [SerializeField] private CoopSettings _settings;
        [SerializeField] private Transform[] _spawnPoints;

        private PlayerInputManager _manager;

        private void Awake()
        {
            _manager = GetComponent<PlayerInputManager>();
        }

        private void OnEnable()
        {
            _manager.onPlayerJoined += HandlePlayerJoined;
            _manager.onPlayerLeft += HandlePlayerLeft;
        }

        private void OnDisable()
        {
            _manager.onPlayerJoined -= HandlePlayerJoined;
            _manager.onPlayerLeft -= HandlePlayerLeft;
        }

        private void HandlePlayerJoined(PlayerInput player)
        {
            if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                var spawn = _spawnPoints[player.playerIndex % _spawnPoints.Length];
                player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            }

            if (_manager.playerCount >= _settings.MaxPlayers)
                _manager.DisableJoining();
        }

        private void HandlePlayerLeft(PlayerInput player)
        {
            if (_manager.playerCount < _settings.MaxPlayers)
                _manager.EnableJoining();
        }
    }
}
