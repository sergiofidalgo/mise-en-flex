using Game.Presentation.Menu;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The first half-minute (docs/opening-flow.md; decisions 2026-11-09 and 2026-11-10): the save
    /// notice on first launch, a fade into the street close to the boss's tower, the camera's short
    /// approach to its main entrance through the crowd (A skips it, every time), a beat to take in
    /// the bustle, the main menu, and once a campaign is chosen the chef walks in and into the lift,
    /// which the camera follows up the glass and over onto the floor's wide shot. Then the follow
    /// camera takes over, joining opens, and the player who chose in the menu is already in as
    /// player 1: the chef who rode up.
    /// </summary>
    public sealed class OpeningSequence : MonoBehaviour
    {
        private enum Stage { SaveNotice, FadeIn, Flight, Menu, Entering, Climb, Landed }

        private const string SaveNoticeKey = "Opening.SaveNoticeShown";

        [SerializeField] private Camera _camera;
        [SerializeField] private OfficeCamera _follow;
        [SerializeField] private MainMenu _menu;
        [SerializeField] private PlayerInputManager _players;
        [SerializeField] private OpeningHud _hud;
        [Tooltip("The glass lift on the tower's front, which rides up with the camera on the climb; optional.")]
        [SerializeField] private OpeningElevator _elevator;
        [Tooltip("The chef who walks in and rides up once a campaign is chosen; optional.")]
        [SerializeField] private OpeningChef _chef;
        [Tooltip("The street and avenue 120 m below, with all their people, cars and lights: switched off once the camera lands, as nobody sees them from the floor (the 60 fps budget with four players).")]
        [SerializeField] private GameObject _streetLevel;
        [Tooltip("The flight from the street to the stop where the menu shows; the last pose is the stop.")]
        [SerializeField] private CameraPose[] _flight = System.Array.Empty<CameraPose>();
        [Tooltip("Through these from the stop, then onto the floor's wide shot.")]
        [SerializeField] private CameraPose[] _climb = System.Array.Empty<CameraPose>();

        [Header("Timing (seconds)")]
        [SerializeField, Min(0.5f)] private float _saveNotice = 3.5f;
        [SerializeField, Min(0.05f)] private float _fade = 0.5f;
        [SerializeField, Min(1f)] private float _flightSeconds = 8f;
        [SerializeField, Min(1f)] private float _climbSeconds = 12f;
        [Tooltip("How far through the climb (0–1) the camera starts turning from the tower's glass onto the floor's view.")]
        [SerializeField, Range(0.5f, 0.98f)] private float _turnFrom = 0.86f;
        [SerializeField, Min(0.05f)] private float _menuFade = 0.45f;
        [Tooltip("How long the camera rests on the busy entrance before the menu comes up (A skips).")]
        [SerializeField, Min(0f)] private float _menuDelay = 2f;
        [Tooltip("How far the camera eases in while it watches the chef walk to the lift (m).")]
        [SerializeField] private Vector3 _enterPush = new Vector3(-1.2f, 0.4f, 3f);

        [Header("Haze")]
        [Tooltip("Fog distances during the flight and the menu, thinner so the tower shows from the street.")]
        [SerializeField] private Vector2 _streetFog = new Vector2(170f, 650f);

        private Stage _stage;
        private float _clock;
        private Vector2 _officeFog;
        private CameraPose[] _climbPath;
        private Quaternion _landingRotation;
        private Vector3 _enterFrom;

        private void Awake()
        {
            _officeFog = new Vector2(RenderSettings.fogStartDistance, RenderSettings.fogEndDistance);
            SetFog(0f);
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
            _hud.Veil = 1f;
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
                    // A slow drift, so the city lives behind the menu.
                    var stop = _flight[_flight.Length - 1];
                    var drift = new Vector3(Mathf.Sin(_clock * 0.15f), Mathf.Sin(_clock * 0.11f) * 0.5f, 0f) * 1.2f;
                    Place(new CameraPose(stop.Position + drift, stop.Target), null);
                    break;
                case Stage.Entering:
                    // The menu goes; the camera eases in and watches the chef walk in to the lift.
                    _menu.SetShown(false, 1f - Mathf.Clamp01(_clock / _menuFade));
                    var rest = _flight[_flight.Length - 1];
                    var look = Mathf.SmoothStep(0f, 0.75f, _clock / 1.2f);
                    var push = OpeningPath.Ease(Mathf.Clamp01(_clock / 5f));
                    Place(new CameraPose(_enterFrom + _enterPush * push, Vector3.Lerp(rest.Target, _chef.Head, look)), null);
                    if (_chef.Aboard && _clock > 0.8f)
                        StartClimb();
                    break;
                case Stage.Climb:
                    _menu.SetShown(false, 0f);
                    var t = Mathf.Clamp01(_clock / _climbSeconds);
                    var eased = OpeningPath.Ease(t);
                    var pose = OpeningPath.SampleEven(_climbPath, eased);
                    // Over the top of the glass, the camera tips onto the floor's own view, looking down at the follow camera's angle.
                    var turn = Mathf.Clamp01((eased - _turnFrom) / (1f - _turnFrom));
                    Place(pose, Quaternion.Slerp(pose.Rotation, _landingRotation, OpeningPath.Ease(turn)));
                    SetFog(eased);
                    if (_elevator != null)
                        _elevator.Track(_camera.transform.position.y);
                    if (t >= 1f)
                        Land();
                    break;
            }
        }

        private void Fly(float time) => Place(OpeningPath.SampleEven(_flight, OpeningPath.Ease(time / _flightSeconds)), null);

        private void HandleLaunching(int slot, bool continuing)
        {
            _enterFrom = _camera.transform.position;
            if (_chef != null)
            {
                _chef.Begin();
                Enter(Stage.Entering);
                return;
            }
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
            Enter(Stage.Landed);
            _menu.SetShown(false, 0f);
            SetFog(1f);
            _follow.enabled = true;
            if (_elevator != null)
                _elevator.Release();
            if (_streetLevel != null)
                _streetLevel.SetActive(false);
            _players.EnableJoining();
            _hud.JoinShown = true;
            _players.onPlayerJoined += HandleJoined;
            _players.onPlayerLeft += HandleJoined;
            // The player who chose in the menu is in already, on the controller they chose with.
            var device = MainMenu.LastDevice;
            if (device != null)
                _players.JoinPlayer(-1, -1, device is Gamepad ? "Gamepad" : "Keyboard&Mouse", device);
            _hud.SetJoined(_players.playerCount);
        }

        private void HandleJoined(PlayerInput player) => _hud.SetJoined(_players.playerCount);

        private void Enter(Stage stage)
        {
            _stage = stage;
            _clock = 0f;
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
