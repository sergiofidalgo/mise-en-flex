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
        [Tooltip("The floor's own HUD (the player bar): hidden until the camera lands (the user's note, 2026-11-10).")]
        [SerializeField] private GameObject[] _hudAfterLanding = System.Array.Empty<GameObject>();
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

        // In case statics survive between runs in the Editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => StartAtMenu = false;

        private void Awake()
        {
            _returning = Game.Gameplay.Saving.CampaignLaunch.Decided && Game.Gameplay.PlayerRoster.HasCarried;
            _officeFog = new Vector2(RenderSettings.fogStartDistance, RenderSettings.fogEndDistance);
            SetFog(0f);
            LampLighting.SetBandMix(1f);
            if (_follow != null)
                _follow.enabled = false;
            _players.DisableJoining();
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
                    if (_clock > SkipGuard && PressedA())
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
            _players.EnableJoining();
            // The player who chose in the menu is in already, on the controller they chose with.
            var device = MainMenu.LastDevice;
            if (device != null && !_returning)
                _players.JoinPlayer(-1, -1, device is Gamepad ? "Gamepad" : "Keyboard&Mouse", device);
        }

        // The floor's HUD draws itself (PlayerBarHud builds its own canvas): its canvases are
        // switched off until the camera lands, so "A Join" never shows over the street or the climb.
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
