using System;
using Game.Simulation.CloseUp;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.CloseUp
{
    /// <summary>
    /// A player's close-up session. Opening switches the player to the CloseUp controls at
    /// once (no dead frames: input during the panel's grow-in already reaches the technique),
    /// renders the technique into this player's own texture, and Exit (B / keyboard D or Esc)
    /// returns to the kitchen. The panel itself is drawn by presentation.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerCloseUp : MonoBehaviour
    {
        private const string GameplayMap = "Gameplay";
        private const string CloseUpMap = "CloseUp";

        private PlayerInput _playerInput;
        private InputAction _aim;
        private InputAction _primary;
        private InputAction _secondary;
        private InputAction _tertiary;
        private InputAction _heatDown;
        private InputAction _heatUp;
        private InputAction _exit;
        private RenderTexture _texture;
        private KitchenServices _services;

        public bool IsOpen => Station != null;

        /// <summary>The station whose close-up is open, or null.</summary>
        public CloseUpStation Station { get; private set; }

        /// <summary>What the technique camera renders into; the panel shows it.</summary>
        public RenderTexture Texture => _texture;

        public KitchenServices Services => _services;

        public int PlayerIndex => _playerInput.playerIndex;

        public event Action<CloseUpStation> Opened;
        public event Action<CloseUpStation> Closed;

        public void Bind(KitchenServices services)
        {
            _services = services;
        }

        /// <returns>False if the station is busy with the other player (or has no technique).</returns>
        public bool TryOpen(CloseUpStation station)
        {
            if (IsOpen || _services == null || !station.TryClaim(this))
                return false;

            EnsureTexture();
            Station = station;
            _playerInput.SwitchCurrentActionMap(CloseUpMap);
            station.Technique.Begin(_texture);
            Opened?.Invoke(station);
            return true;
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            var station = Station;
            station.Technique.Finish();
            station.Release(this);
            Station = null;
            _playerInput.SwitchCurrentActionMap(GameplayMap);
            Closed?.Invoke(station);
        }

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
        }

        // Start, not Awake: PlayerInput has set up this player's own copy of the actions by now.
        private void Start()
        {
            var map = _playerInput.actions.FindActionMap(CloseUpMap, throwIfNotFound: true);
            _aim = map.FindAction("Aim", true);
            _primary = map.FindAction("Primary", true);
            _secondary = map.FindAction("Secondary", true);
            _tertiary = map.FindAction("Tertiary", true);
            _heatDown = map.FindAction("HeatDown", true);
            _heatUp = map.FindAction("HeatUp", true);
            _exit = map.FindAction("Exit", true);
        }

        private void Update()
        {
            if (!IsOpen)
                return;

            if (_exit.WasPressedThisFrame())
            {
                Close();
                return;
            }

            var aim = _aim.ReadValue<Vector2>();
            var input = new CloseUpInput(
                new System.Numerics.Vector2(aim.x, aim.y),
                _primary.WasPressedThisFrame(),
                _secondary.IsPressed(),
                _tertiary.WasPressedThisFrame(),
                _heatDown.ReadValue<float>(),
                _heatUp.ReadValue<float>());

            var technique = Station.Technique;
            technique.HandleInput(input, Time.unscaledDeltaTime);
            technique.Tick(Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            Close();
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }

        private void EnsureTexture()
        {
            var size = _services.Tuning.CloseUpTextureSize;
            if (_texture != null && _texture.width == size)
                return;

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }

            _texture = new RenderTexture(size, size, 24) { name = $"CloseUp_P{PlayerIndex + 1}" };
        }
    }
}
