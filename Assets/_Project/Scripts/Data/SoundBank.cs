using System;
using UnityEngine;

namespace Game.Data
{
    /// <summary>One sound effect: a few takes of it, and how it's played.</summary>
    [Serializable]
    public sealed class SoundCue
    {
        [Tooltip("Takes to choose from, so repeats don't sound machine-like.")]
        [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;
        [Tooltip("Random pitch change either way, as a fraction: 0.05 is about a semitone across the whole range.")]
        [SerializeField, Range(0f, 0.5f)] private float _pitchVariation;
        [Tooltip("Seconds that must pass before it can play again, so a busy kitchen doesn't turn to mush.")]
        [SerializeField, Min(0f)] private float _minInterval;

        public SoundCue()
        {
        }

        public SoundCue(float volume, float pitchVariation, float minInterval)
        {
            _volume = volume;
            _pitchVariation = pitchVariation;
            _minInterval = minInterval;
        }

        public AudioClip[] Clips => _clips;
        public float Volume => _volume;
        public float PitchVariation => _pitchVariation;
        public float MinInterval => _minInterval;
    }

    /// <summary>The kitchen's sounds and how they're mixed. This is where sound gets tuned.</summary>
    [CreateAssetMenu(menuName = "Game/Sound Bank", fileName = "SoundBank")]
    public sealed class SoundBank : ScriptableObject
    {
        [Header("Bell")]
        // Hardly any pitch change: it should always read as the same bell.
        [SerializeField] private SoundCue _bell = new SoundCue(0.8f, 0.01f, 0.15f);

        [Header("Chopping")]
        [Tooltip("A cut in the sweet spot.")]
        [SerializeField] private SoundCue _chopClean = new SoundCue(0.8f, 0.06f, 0.04f);
        [Tooltip("A cut a little early or late.")]
        [SerializeField] private SoundCue _chopOff = new SoundCue(0.8f, 0.06f, 0.04f);
        [Tooltip("A cut well off the beat.")]
        [SerializeField] private SoundCue _chopMiss = new SoundCue(0.7f, 0.05f, 0.04f);

        [Header("Door")]
        // Quiet on purpose: the door gets used constantly during service.
        [SerializeField] private SoundCue _doorOpen = new SoundCue(0.35f, 0.08f, 0.3f);
        [SerializeField] private SoundCue _doorClose = new SoundCue(0.45f, 0.06f, 0.3f);

        [Header("Sizzle")]
        [Tooltip("Food landing or being flipped in a hot pan.")]
        [SerializeField] private SoundCue _sizzleHit = new SoundCue(0.7f, 0.05f, 0.15f);
        [Tooltip("Plays on repeat for each sizzling pan.")]
        [SerializeField] private AudioClip _sizzleLoop;
        [Tooltip("°C where food starts to sizzle: water boiling off. Searing starts around 140 °C.")]
        [SerializeField] private float _sizzleFrom = 110f;
        [Tooltip("°C where the sizzle is at its loudest and highest. The pan tops out near 280 °C.")]
        [SerializeField] private float _sizzleFull = 240f;
        [Tooltip("Volume at the first sizzle: low, but there's no mistaking it for silence.")]
        [SerializeField, Range(0f, 1f)] private float _sizzleQuietVolume = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _sizzleLoudVolume = 0.65f;
        [SerializeField, Range(0.25f, 2f)] private float _sizzleLowPitch = 0.8f;
        [SerializeField, Range(0.25f, 2f)] private float _sizzleHighPitch = 1.2f;
        [Tooltip("How much volume can change per second, so it never pops in or out.")]
        [SerializeField, Min(0.01f)] private float _sizzleFadeRate = 2.5f;
        [Tooltip("The most pans heard at once: the loudest ones.")]
        [SerializeField, Min(1)] private int _maxSizzles = 3;

        [Header("Fridge")]
        [SerializeField] private SoundCue _fridgeOpen = new SoundCue(0.45f, 0.04f, 0.2f);
        [Tooltip("Its thump sits 0.26 s into the clip, timed to the door's 0.3 s swing shut.")]
        [SerializeField] private SoundCue _fridgeClose = new SoundCue(0.45f, 0.04f, 0.2f);

        [Header("Fire detector")]
        // Clearly audible but not painful: it may go off often in a chaotic service.
        [SerializeField] private SoundCue _alarmBeep = new SoundCue(0.4f, 0f, 0f);
        [Tooltip("Seconds between beeps: shorter is more frantic.")]
        [SerializeField, Min(0.1f)] private float _alarmInterval = 1f;
        [Tooltip("How close to burnt food must be, as a fraction of 'burnt' browning. Matches PanView's thick smoke, so the two arrive together.")]
        [SerializeField, Range(0f, 1f)] private float _alarmFrom = 0.75f;
        [Tooltip("°C below which it stops, even with burnt food in the pan: a cooling pan has stopped doing harm.")]
        [SerializeField] private float _alarmHotEnough = 140f;

        [Header("Footsteps")]
        // Very quiet on purpose: if you notice them after a minute of play, they're too loud.
        [SerializeField] private SoundCue _step = new SoundCue(0.14f, 0.07f, 0f);
        [Tooltip("Metres walked per step. At the 4.2 m/s walking speed, 1.2 is 3.5 steps a second.")]
        [SerializeField, Min(0.1f)] private float _stepStride = 1.2f;
        [Tooltip("Metres per second below which a chef counts as standing still.")]
        [SerializeField, Min(0f)] private float _stepMinSpeed = 0.5f;
        [Tooltip("How far into a stride a chef starts, as a fraction: high, so the first step comes as soon as they set off.")]
        [SerializeField, Range(0f, 1f)] private float _stepFirstStride = 0.75f;
        [Tooltip("Pitch difference between chefs, so each sounds a little different.")]
        [SerializeField, Range(0f, 0.3f)] private float _stepPitchPerChef = 0.08f;

        [Header("Small sounds")]
        [Tooltip("Something set down on or in anything.")]
        [SerializeField] private SoundCue _clack = new SoundCue(0.4f, 0.08f, 0.05f);
        [Tooltip("Someone paying. The interval makes a table paying together one sound.")]
        [SerializeField] private SoundCue _till = new SoundCue(0.45f, 0.02f, 0.5f);
        [Tooltip("Customers coming in. The interval makes a party arriving together one sound.")]
        [SerializeField] private SoundCue _chime = new SoundCue(0.5f, 0.02f, 1f);

        [Header("Mix")]
        [Tooltip("How far sounds pan with their place on screen: 0 is centred, 1 is hard left and right at the screen's edges.")]
        [SerializeField, Range(0f, 1f)] private float _stereoWidth = 0.5f;

        public SoundCue Bell => _bell;
        public SoundCue ChopClean => _chopClean;
        public SoundCue ChopOff => _chopOff;
        public SoundCue ChopMiss => _chopMiss;
        public SoundCue DoorOpen => _doorOpen;
        public SoundCue DoorClose => _doorClose;
        public SoundCue SizzleHit => _sizzleHit;
        public AudioClip SizzleLoop => _sizzleLoop;
        public float SizzleFrom => _sizzleFrom;
        public float SizzleFull => _sizzleFull;
        public float SizzleQuietVolume => _sizzleQuietVolume;
        public float SizzleLoudVolume => _sizzleLoudVolume;
        public float SizzleLowPitch => _sizzleLowPitch;
        public float SizzleHighPitch => _sizzleHighPitch;
        public float SizzleFadeRate => _sizzleFadeRate;
        public int MaxSizzles => _maxSizzles;
        public SoundCue FridgeOpen => _fridgeOpen;
        public SoundCue FridgeClose => _fridgeClose;
        public SoundCue AlarmBeep => _alarmBeep;
        public float AlarmInterval => _alarmInterval;
        public float AlarmFrom => _alarmFrom;
        public float AlarmHotEnough => _alarmHotEnough;
        public SoundCue Step => _step;
        public float StepStride => _stepStride;
        public float StepMinSpeed => _stepMinSpeed;
        public float StepFirstStride => _stepFirstStride;
        public float StepPitchPerChef => _stepPitchPerChef;
        public SoundCue Clack => _clack;
        public SoundCue Till => _till;
        public SoundCue Chime => _chime;
        public float StereoWidth => _stereoWidth;
    }
}
