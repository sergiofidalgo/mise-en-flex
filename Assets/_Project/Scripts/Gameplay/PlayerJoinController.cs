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

        // Players from before a restart come back on their own devices.
        private void Start() => PlayerRejoin.Consume(_manager);

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

                // CharacterControllers don't see transform teleports until physics syncs;
                // without this the first Move() would snap the player back to the origin.
                Physics.SyncTransforms();
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
