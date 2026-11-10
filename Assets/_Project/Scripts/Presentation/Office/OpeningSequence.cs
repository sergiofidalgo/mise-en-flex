using Game.Presentation.Menu;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The first half-minute (docs/opening-flow.md; decisions 2026-11-09): the save notice on first
    /// launch, a fade into the street at the foot of the boss's tower, the camera's glide up the avenue
    /// to rest in the road with the tower ahead (A skips it, every time), the main menu, and once a
    /// campaign is chosen the climb: on up the avenue, up the tower's glass, over onto the floor's
    /// wide shot (the user's diagram, 2026-11-09). Then the follow camera takes
    /// over, joining opens, and the player who chose in the menu is already in as player 1.
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
        [Tooltip("The street and avenue 120 m below, with all their people, cars and lights: switched off once the camera lands, as nobody sees them from the floor (the 60 fps budget with four players).")]
        [SerializeField] private GameObject _streetLevel;
        [Tooltip("The flight from the street to the stop where the menu shows; the last pose is the stop.")]
        [SerializeField] private CameraPose[] _flight = System.Array.Empty<CameraPose>();
        [Tooltip("Through these from the stop, then onto the floor's wide shot.")]
        [SerializeField] private CameraPose[] _climb = System.Array.Empty<CameraPose>();

        [Header("Timing (seconds)")]
        [SerializeField, Min(0.5f)] private float _saveNotice = 3.5f;
        [SerializeField, Min(0.05f)] private float _fade = 0.5f;
        [SerializeField, Min(1f)] private float _flightSeconds = 15f;
        [SerializeField, Min(1f)] private float _climbSeconds = 13f;
        [Tooltip("How far through the climb (0–1) the camera starts turning from the tower's glass onto the floor's view.")]
        [SerializeField, Range(0.5f, 0.98f)] private float _turnFrom = 0.86f;
        [SerializeField, Min(0.05f)] private float _menuFade = 0.45f;

        [Header("Haze")]
        [Tooltip("Fog distances during the flight and the menu, thinner so the tower shows from the street.")]
        [SerializeField] private Vector2 _streetFog = new Vector2(170f, 650f);

        private Stage _stage;
        private float _clock;
        private Vector2 _officeFog;
        private CameraPose[] _climbPath;
        private Quaternion _landingRotation;

        // Back from the kitchen (the pause menu's Quit to the office, decision 2026-11-10): the same
        // players rejoin on their own controllers (PlayerJoinController), so the opening and the menu
        // are skipped and the camera starts on the floor.
        private bool _returning;

        private void Awake()
        {
            _returning = Game.Gameplay.Saving.CampaignLaunch.Decided && Game.Gameplay.PlayerRoster.HasCarried;
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
            if (_returning)
            {
                _hud.Veil = 0f;
                // Straight onto the floor, not a glide up from the street.
                _camera.transform.position = _follow.Wanted();
                Land();
                return;
            }
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
                    _menu.SetShown(true, Mathf.Clamp01(_clock / _menuFade));
                    // A slow drift, so the city lives behind the menu.
                    var stop = _flight[_flight.Length - 1];
                    var drift = new Vector3(Mathf.Sin(_clock * 0.15f), Mathf.Sin(_clock * 0.11f) * 0.5f, 0f) * 1.2f;
                    Place(new CameraPose(stop.Position + drift, stop.Target), null);
                    break;
                case Stage.Climb:
                    _menu.SetShown(false, 1f - Mathf.Clamp01(_clock / _menuFade));
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
            // From wherever the camera is now (drifting at the stop), up the tower, onto the wide shot.
            var landing = _follow.Wanted();
            _landingRotation = Quaternion.Euler(55f, 0f, 0f);
            var path = new CameraPose[_climb.Length + 2];
            path[0] = new CameraPose(_camera.transform.position, _flight[_flight.Length - 1].Target);
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
            if (device != null && !_returning)
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
