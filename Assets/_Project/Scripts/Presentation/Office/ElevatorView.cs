using Game.Data;
using Game.Gameplay.Office;
using UnityEngine;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The elevator's feedback: the arrival bell (the loudest sound on the floor, so a join never
    /// goes unnoticed), the doors' slide, the floor dial's needle sweeping up to this floor as the
    /// car arrives, and the arrival lamp over the doors, lit from the bell until they shut.
    /// </summary>
    public sealed class ElevatorView : MonoBehaviour
    {
        [SerializeField] private OfficeElevator _elevator;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private SoundCue _bell = new SoundCue(1f, 0.01f, 0f);
        [SerializeField] private SoundCue _doors = new SoundCue(0.5f, 0.04f, 0f);
        [Tooltip("The dial's needle; it turns about its local Z.")]
        [SerializeField] private Transform _needle;
        [Tooltip("Needle angle while the car is away (lower floors) and at this floor.")]
        [SerializeField] private float _needleAway = 70f;
        [SerializeField] private float _needleHere = -60f;
        [Tooltip("Seconds for the needle to sweep up to this floor after the bell.")]
        [SerializeField, Min(0.01f)] private float _sweepSeconds = 0.35f;
        [SerializeField] private GameObject _arrivedLamp;

        private float _sweep;
        private bool _subscribed;

        private void Start()
        {
            _elevator.Doors.Rang += HandleRang;
            _elevator.Doors.StartedOpening += HandleDoorsMove;
            _elevator.Doors.StartedClosing += HandleDoorsMove;
            _subscribed = true;
            _sweep = 1f;
        }

        private void OnDestroy()
        {
            if (!_subscribed)
                return;
            _elevator.Doors.Rang -= HandleRang;
            _elevator.Doors.StartedOpening -= HandleDoorsMove;
            _elevator.Doors.StartedClosing -= HandleDoorsMove;
        }

        private void HandleRang()
        {
            Play(_bell);
            // A car already here (doors open) doesn't sweep from the bottom again.
            if (_elevator.Doors.Openness <= 0f)
                _sweep = 0f;
        }

        private void HandleDoorsMove() => Play(_doors);

        private void Update()
        {
            _sweep = Mathf.Min(1f, _sweep + Time.deltaTime / _sweepSeconds);
            if (_needle != null)
            {
                // Overshoots a little and settles, like a sprung needle.
                var t = 1f - Mathf.Pow(1f - _sweep, 3f);
                _needle.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(_needleAway, _needleHere, t));
            }
            if (_arrivedLamp != null)
                _arrivedLamp.SetActive(_elevator.Doors.State != Game.Simulation.Office.ElevatorDoors.DoorState.Closed);
        }

        private void Play(SoundCue cue)
        {
            if (_audio == null || cue.Clips == null || cue.Clips.Length == 0)
                return;
            var clip = cue.Clips[Random.Range(0, cue.Clips.Length)];
            _audio.pitch = 1f + Random.Range(-cue.PitchVariation, cue.PitchVariation);
            _audio.PlayOneShot(clip, cue.Volume);
        }
    }
}
