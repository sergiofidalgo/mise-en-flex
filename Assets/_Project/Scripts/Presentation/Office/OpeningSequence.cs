using Game.Data;
using Game.Gameplay.Office;
using Game.Presentation.Menu;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The first half-minute (docs/opening-flow.md; decisions 2026-11-09 and 2026-11-10): the save
    /// notice on first launch, a fade into the street close to the boss's tower, the camera's short
    /// approach to its main entrance through the crowd (A skips it, every time), the main menu, and
    /// the moment a campaign is chosen the camera rises beside the lift, the chef in it, up the
    /// glass and over onto the floor's wide shot (A skips that too). Then the follow
    /// camera takes over, joining opens, and the player who chose in the menu is already in as
    /// player 1: the chef who rode up.
    /// <para>Joining in the menu (the user's calls, 2026-11-13): player 1 is in from the start, as
    /// whichever controller presses first, and only they drive the menu and skip the climb. The
    /// player bar shows from the menu on, and anyone else's A there or on the climb joins them: their
    /// chef pops into the glass lift beside player 1's and rides up, and everyone joins the floor,
    /// in slot order, as the camera lands.</para>
    /// </summary>
    public sealed class OpeningSequence : MonoBehaviour
    {
        private enum Stage { SaveNotice, FadeIn, Flight, Menu, Climb, Landed }

        private const string SaveNoticeKey = "Opening.SaveNoticeShown";

        [SerializeField] private Camera _camera;
        [SerializeField] private OfficeCamera _follow;
        [SerializeField] private MainMenu _menu;
        [SerializeField] private PlayerInputManager _players;
        [SerializeField] private OpeningHud _hud;
        [Tooltip("The glass lift on the tower's front, which rides up with the camera on the climb; optional.")]
        [SerializeField] private OpeningElevator _elevator;
        [Tooltip("The helicopter that crosses the top of the tower as the camera crests it; optional.")]
        [SerializeField] private OpeningFlyby _flyby;
        [Tooltip("The couriers parked outside the restaurant, who get their orders and ride off once the camera stops.")]
        [SerializeField] private Game.Presentation.Office.Ambience.CourierDeparture[] _departures = System.Array.Empty<Game.Presentation.Office.Ambience.CourierDeparture>();
        [Tooltip("How far through the climb's time (0–1) the helicopter starts its pass.")]
        [SerializeField, Range(0f, 1f)] private float _flybyFrom = 0.55f;
        [Tooltip("Everything below the floor that only the opening sees (the street 120 m down with its people and lights, the tower's rooms, the lift): switched off once the camera lands, as nobody sees it from the floor (the 60 fps budget with four players).")]
        [SerializeField] private GameObject[] _offAfterLanding = System.Array.Empty<GameObject>();
        [Tooltip("The floor's own HUD (the money card): hidden until the camera lands (the user's note, 2026-11-10).")]
        [SerializeField] private GameObject[] _hudAfterLanding = System.Array.Empty<GameObject>();
        [Tooltip("The player bar: shown from the main menu on, where players join (2026-11-13).")]
        [SerializeField] private PlayerBarHud _bar;
        [SerializeField] private CoopSettings _coop;
        [Tooltip("The flight from the street to the stop where the menu shows; the last pose is the stop.")]
        [SerializeField] private CameraPose[] _flight = System.Array.Empty<CameraPose>();
        [Tooltip("Through these from the stop, then onto the floor's wide shot.")]
        [SerializeField] private CameraPose[] _climb = System.Array.Empty<CameraPose>();

        [Header("Timing (seconds)")]
        [SerializeField, Min(0.5f)] private float _saveNotice = 3.5f;
        [SerializeField, Min(0.05f)] private float _fade = 0.5f;
        [SerializeField, Min(1f)] private float _flightSeconds = 8f;
        [SerializeField, Min(1f)] private float _climbSeconds = 9f;
        [Tooltip("How far through the climb (0–1) the camera starts turning from the tower's glass onto the floor's view.")]
        [SerializeField, Range(0.3f, 0.98f)] private float _turnFrom = 0.55f;
        [SerializeField, Min(0.05f)] private float _menuFade = 0.45f;
        [Tooltip("How long the camera rests on the entrance before the menu comes up (A skips). Short: the user found two seconds too long.")]
        [SerializeField, Min(0f)] private float _menuDelay = 0.25f;

        [Header("Haze")]
        [Tooltip("Fog distances during the flight and the menu, thinner so the tower shows from the street.")]
        [SerializeField] private Vector2 _streetFog = new Vector2(170f, 650f);

        [Header("Lamps")]
        [Tooltip("Between these points of the climb's time (0–1) the street's flat-banded lamp light fades to the floor's smooth one.")]
        [SerializeField, Range(0f, 1f)] private float _bandsFadeFrom = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _bandsFadeTo = 0.8f;

        private Stage _stage;
        private float _clock;
        private Vector2 _officeFog;
        private CameraPose[] _climbPath;
        private Quaternion _landingRotation;
        // The press that chose the campaign mustn't also skip the climb.
        private const float SkipGuard = 0.4f;

        // Back from the kitchen (the pause menu's Quit to the office, decision 2026-11-10): the same
        // players rejoin on their own controllers (PlayerJoinController), so the opening and the menu
        // are skipped and the camera starts on the floor.
        private bool _returning;

        /// <summary>
        /// Set before loading the office to start its opening at the main menu: no save notice and no
        /// approach, the camera already at the entrance (the pause menu's "Quit to the main menu").
        /// </summary>
        public static bool StartAtMenu { get; set; }

        private bool _fadeIntoMenu;

        // Who's in before the floor, by input device id, and the devices themselves.
        private MenuRoster _roster;
        private readonly System.Collections.Generic.Dictionary<int, InputDevice> _devices = new System.Collections.Generic.Dictionary<int, InputDevice>();

        // In case statics survive between runs in the Editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            StartAtMenu = false;
            MainMenu.Driver = null;
        }

        private void Awake()
        {
            _returning = Game.Gameplay.Saving.CampaignLaunch.Decided && Game.Gameplay.PlayerRoster.HasCarried;
            _officeFog = new Vector2(RenderSettings.fogStartDistance, RenderSettings.fogEndDistance);
            SetFog(0f);
            LampLighting.SetBandMix(1f);
            if (_follow != null)
                _follow.enabled = false;
            _players.DisableJoining();
            _roster = new MenuRoster(_coop != null ? _coop.MaxPlayers : 4);
            if (!_returning)
                MainMenu.Driver = null; // whoever presses first is player 1
            _menu.Launching += HandleLaunching;
            Place(OpeningPath.SampleEven(_flight, 0f), null);
        }

        private void OnDestroy()
        {
            if (_menu != null)
                _menu.Launching -= HandleLaunching;
        }

        private void Start()
        {
            ShowFloorHud(false);
            ShowBar(false);
            if (_returning)
            {
                _hud.Veil = 0f;
                // Straight onto the floor, not a glide up from the street.
                _camera.transform.position = _follow.Wanted();
                Land();
                return;
            }
            _hud.Veil = 1f;
            if (StartAtMenu)
            {
                // Back from a game: straight to the menu, fading in on the entrance.
                StartAtMenu = false;
                _fadeIntoMenu = true;
                Place(_flight[_flight.Length - 1], null);
                Enter(Stage.Menu);
                _clock = _menuDelay;
                return;
            }
            if (PlayerPrefs.GetInt(SaveNoticeKey, 0) == 0)
            {
                Enter(Stage.SaveNotice);
                _hud.SaveNoticeShown = true;
            }
            else
            {
                Enter(Stage.FadeIn);
            }
        }

        private void Update()
        {
            _clock += Time.deltaTime;
            switch (_stage)
            {
                case Stage.SaveNotice:
                    if (_clock >= _saveNotice || PressedA())
                    {
                        PlayerPrefs.SetInt(SaveNoticeKey, 1);
                        _hud.SaveNoticeShown = false;
                        Enter(Stage.FadeIn);
                    }
                    break;
                case Stage.FadeIn:
                    _hud.Veil = 1f - _clock / _fade;
                    Fly(_clock);
                    if (_clock >= _fade)
                    {
                        _hud.Veil = 0f;
                        var flown = _clock;
                        Enter(Stage.Flight);
                        _clock = flown;
                        _hud.SkipShown = true;
                    }
                    break;
                case Stage.Flight:
                    Fly(_clock);
                    if (_clock >= _flightSeconds || PressedA())
                    {
                        Place(_flight[_flight.Length - 1], null);
                        _hud.SkipShown = false;
                        Enter(Stage.Menu);
                    }
                    break;
                case Stage.Menu:
                    // A beat on the bustle first, then the menu (A brings it at once).
                    if (_clock < _menuDelay && PressedA())
                        _clock = _menuDelay;
                    if (_clock < _menuDelay)
                        _menu.SetShown(false, 0f);
                    else
                        _menu.SetShown(true, Mathf.Clamp01((_clock - _menuDelay) / _menuFade));
                    if (_fadeIntoMenu)
                        _hud.Veil = 1f - Mathf.Clamp01((_clock - _menuDelay) / _fade);
                    // The camera holds still under the menu (the user's note, 2026-11-10): the street does the moving.
                    Place(_flight[_flight.Length - 1], null);
                    break;
                case Stage.Climb:
                    // The menu goes as the camera starts up beside the lift, the chef already in it
                    // (the user's note, 2026-11-10: no walk in between). A skips to the floor.
                    _menu.SetShown(false, 1f - Mathf.Clamp01(_clock / _menuFade));
                    _hud.SkipShown = _clock > SkipGuard;
                    if (_clock > SkipGuard && DriverPressedA())
                    {
                        Land();
                        break;
                    }
                    var t = Mathf.Clamp01(_clock / _climbSeconds);
                    // Over the top of the glass, the camera tips onto the floor's own view, looking down at the follow camera's angle.
                    var position = OpeningPath.Climb(_climbPath, _landingRotation, _turnFrom, t, out var rotation);
                    _camera.transform.SetPositionAndRotation(position, rotation);
                    SetFog(OpeningPath.ClimbEase(t));
                    LampLighting.SetBandMix(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_bandsFadeFrom, _bandsFadeTo, t)));
                    if (_elevator != null)
                        _elevator.Track(position.y, t);
                    if (_flyby != null && t >= _flybyFrom)
                        _flyby.Begin();
                    if (t >= 1f)
                        Land();
                    break;
            }
        }

        private void Fly(float time) => Place(OpeningPath.SampleEven(_flight, OpeningPath.Ease(time / _flightSeconds)), null);

        private void HandleLaunching(int slot, bool continuing)
        {
            StartClimb();
        }

        private void StartClimb()
        {
            // From wherever the camera is now, up the tower beside the lift, onto the wide shot.
            var landing = _follow.Wanted();
            _landingRotation = Quaternion.Euler(55f, 0f, 0f);
            var path = new CameraPose[_climb.Length + 2];
            path[0] = new CameraPose(_camera.transform.position, _camera.transform.position + _camera.transform.forward * 15f);
            for (var i = 0; i < _climb.Length; i++)
                path[i + 1] = _climb[i];
            path[path.Length - 1] = new CameraPose(landing, landing + _landingRotation * Vector3.forward * 30f);
            _climbPath = path;
            Enter(Stage.Climb);
        }

        private void Land()
        {
            // Skipped before the camera could have seen it: the helicopter stays on its lap.
            if (_flyby != null && _stage != Stage.Climb)
                _flyby.Cancel();
            Enter(Stage.Landed);
            _menu.SetShown(false, 0f);
            SetFog(1f);
            LampLighting.SetBandMix(0f); // the floor's lamps are smooth (the user's call, 2026-11-09)
            _follow.enabled = true;
            if (_elevator != null)
                _elevator.Release();
            foreach (var below in _offAfterLanding)
                if (below != null)
                    below.SetActive(false);
            _hud.SkipShown = false;
            // Wherever the camera was (a skip can come mid-walk or mid-climb), it's on the floor's view now.
            _camera.transform.SetPositionAndRotation(_follow.Wanted(), Quaternion.Euler(55f, 0f, 0f));
            ShowFloorHud(true);
            ShowBar(true);
            _players.EnableJoining();
            // Everyone in from the menu joins the floor, player 1 first, each in their own slot on
            // the controller they joined with: the chefs who rode up.
            if (!_returning)
            {
                ClaimLastDevice();
                var devices = _roster.Devices;
                for (var slot = 0; slot < devices.Count; slot++)
                    if (_devices.TryGetValue(devices[slot], out var device) && device != null && device.added)
                        _players.JoinPlayer(slot, -1, device is Gamepad ? "Gamepad" : "Keyboard&Mouse", device);
            }
            if (_bar != null)
                _bar.ClearStandIns();
        }

        // ---- Joining before the floor (2026-11-13) ----------------------------------------------

        private void LateUpdate()
        {
            if (_returning || _stage == Stage.Landed || _roster == null)
                return;
            // Whoever touched the menu first is player 1, whatever they pressed.
            ClaimLastDevice();
            var joiningOpen = _stage == Stage.Menu || _stage == Stage.Climb;
            foreach (var pad in Gamepad.all)
                if (pad.buttonSouth.wasPressedThisFrame)
                    Press(pad, joiningOpen);
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.xKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                Press(keyboard, joiningOpen);
        }

        private void Press(InputDevice device, bool joiningOpen)
        {
            var slot = _roster.Press(device.deviceId, joiningOpen);
            if (slot < 0)
                return;
            _devices[device.deviceId] = device;
            if (slot == 0)
            {
                MainMenu.Driver = device;
                return;
            }
            // Their chef pops into the lift beside player 1's, and their card fills on the bar.
            var chef = _elevator != null ? _elevator.AddRider(slot) : null;
            if (_bar != null && chef != null)
                _bar.Stand(slot, chef);
        }

        private void ClaimLastDevice()
        {
            var last = MainMenu.LastDevice;
            if (last == null || _roster.HasDriver || !_roster.Claim(last.deviceId))
                return;
            _devices[last.deviceId] = last;
            MainMenu.Driver = last;
        }

        // Player 1's A only: the climb's skip. Before player 1 is known, anyone's.
        private bool DriverPressedA()
        {
            var driver = MainMenu.Driver;
            if (driver == null)
                return PressedA();
            if (driver is Gamepad pad)
                return pad.buttonSouth.wasPressedThisFrame;
            return driver is Keyboard keys && (keys.xKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame);
        }

        // The player bar draws itself on its own canvas: off over the street, on from the menu,
        // with player 1 in from the start as the chef waiting in the lift.
        private void ShowBar(bool shown)
        {
            if (_bar == null)
                return;
            foreach (var canvas in _bar.GetComponentsInChildren<Canvas>(true))
                canvas.enabled = shown;
            if (shown && _stage != Stage.Landed && _elevator != null)
                _bar.Stand(0, _elevator.Rider);
        }

        // The floor's HUD draws itself (the money card builds its own canvas): its canvases are
        // switched off until the camera lands, so it never shows over the street or the climb.
        private void ShowFloorHud(bool shown)
        {
            foreach (var hud in _hudAfterLanding)
            {
                if (hud == null)
                    continue;
                foreach (var canvas in hud.GetComponentsInChildren<Canvas>(true))
                    canvas.enabled = shown;
            }
        }

        private void Enter(Stage stage)
        {
            _stage = stage;
            _clock = 0f;
            if (stage == Stage.Menu)
                ShowBar(true);
            if (stage == Stage.Menu)
                foreach (var courier in _departures)
                    if (courier != null)
                        courier.Begin();
        }

        private void Place(CameraPose pose, Quaternion? rotation)
        {
            _camera.transform.SetPositionAndRotation(pose.Position, rotation ?? pose.Rotation);
        }

        // Thin haze in the street and over the menu, the office's own once on the floor.
        private void SetFog(float towardsOffice)
        {
            RenderSettings.fogStartDistance = Mathf.Lerp(_streetFog.x, _officeFog.x, towardsOffice);
            RenderSettings.fogEndDistance = Mathf.Lerp(_streetFog.y, _officeFog.y, towardsOffice);
        }

        private static bool PressedA()
        {
            foreach (var pad in Gamepad.all)
                if (pad.buttonSouth.wasPressedThisFrame)
                    return true;
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.xKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
        }
    }
}
