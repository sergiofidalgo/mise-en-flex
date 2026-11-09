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

            // Players past the last spawn point would share one and start inside each other.
            var spawns = _spawnPoints?.Length ?? 0;
            if (spawns < _settings.MaxPlayers)
                Debug.LogWarning($"{name}: {spawns} spawn points for up to {_settings.MaxPlayers} players. Rebuild the scene (Tools/Game/...) to get one each.", this);
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

        // Players who came from the last scene (the office) join again at once, on their own
        // controllers and in their old order, after every scene object has subscribed (OnEnable).
        private void Start()
        {
            foreach (var seat in PlayerRoster.Take())
            {
                if (_manager.playerCount >= _settings.MaxPlayers)
                    break;
                _manager.JoinPlayer(-1, -1, seat.Scheme, seat.Devices);
            }
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

                // A rearranged kitchen can have furniture over a spawn point. Whichever of this and
                // the composition root's binding runs second sees both the spot and the floor plan.
                if (player.TryGetComponent(out PlayerMovement movement))
                    movement.StepClear();
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
