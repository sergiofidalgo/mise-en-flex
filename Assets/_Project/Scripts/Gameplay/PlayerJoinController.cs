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
        // controllers and in their own slots, after every scene object has subscribed (OnEnable).
        // The slot is given, not taken in order: with P2's controller gone, P3 must stay P3 (their
        // colour and chef), and P2 takes the free slot back when they press A.
        private void Start()
        {
            IsRejoining = true;
            try
            {
                foreach (var seat in PlayerRoster.Take())
                {
                    if (_manager.playerCount >= _settings.MaxPlayers)
                        break;
                    _manager.JoinPlayer(seat.Index, -1, seat.Scheme, seat.Devices);
                }
            }
            finally
            {
                IsRejoining = false;
            }
        }

        /// <summary>
        /// True while players carried over from the last scene join again: they're not new, so
        /// they don't name themselves again (JoinNaming, 2026-11-13).
        /// </summary>
        public bool IsRejoining { get; private set; }

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
