using System;
using System.Numerics;

namespace Game.Simulation.Street
{
    /// <summary>Tuning for a chef knocked down by a car. Distances in metres, times in seconds.</summary>
    public sealed class KnockdownSettings
    {
        /// <summary>How long they're in the air.</summary>
        public float FlightSeconds = 0.9f;
        /// <summary>How high the arc goes.</summary>
        public float Height = 2.4f;
        /// <summary>How far along the car's way they're thrown.</summary>
        public float Distance = 4f;
        /// <summary>How many times they turn head over heels on the way.</summary>
        public float Tumbles = 2f;
        /// <summary>How long they lie there seeing stars.</summary>
        public float DazedSeconds = 1.5f;
        /// <summary>How long getting back up takes.</summary>
        public float GetUpSeconds = 0.35f;
    }

    /// <summary>
    /// A chef hit by a car (the user's idea, 2026-11-12: "hit and runs against players in a funny
    /// way if they happen to be in the wrong place at the wrong time"): thrown along the car's way
    /// in a high tumbling arc, down flat on the nearer pavement, dazed for a moment, then up again.
    /// Nobody is hurt; it costs the seconds and whatever food they were carrying.
    /// </summary>
    public sealed class Knockdown
    {
        public enum Phase { Flying, Dazed, GettingUp, Done }

        private readonly KnockdownSettings _settings;
        private readonly Vector2 _from;
        private float _time;

        public Knockdown(Vector2 from, Vector2 landing, KnockdownSettings settings)
        {
            _from = from;
            Landing = landing;
            _settings = settings;
            var way = landing - from;
            Direction = way.LengthSquared() > 1e-6f ? Vector2.Normalize(way) : Vector2.UnitX;
        }

        /// <summary>Where they come down, on the ground (X, Z).</summary>
        public Vector2 Landing { get; }
        /// <summary>Which way they fly, across the ground.</summary>
        public Vector2 Direction { get; }

        public Phase State =>
            _time < _settings.FlightSeconds ? Phase.Flying
            : _time < _settings.FlightSeconds + _settings.DazedSeconds ? Phase.Dazed
            : _time < _settings.FlightSeconds + _settings.DazedSeconds + _settings.GetUpSeconds ? Phase.GettingUp
            : Phase.Done;

        // How far through the flight, 0–1.
        private float Flight => Math.Min(1f, _time / Math.Max(0.01f, _settings.FlightSeconds));

        /// <summary>Where they are: across the ground evenly, up and down in a parabola (height × 4t(1 − t)).</summary>
        public Vector3 Position
        {
            get
            {
                var t = Flight;
                var ground = Vector2.Lerp(_from, Landing, t);
                return new Vector3(ground.X, _settings.Height * 4f * t * (1f - t), ground.Y);
            }
        }

        /// <summary>Degrees turned head over heels so far, about the axis across their flight.</summary>
        public float Tumble => 360f * _settings.Tumbles * Flight;

        /// <summary>How flat on the ground they are, 0 standing to 1 lying: down from the moment they land, easing back up at the end.</summary>
        public float Lying
        {
            get
            {
                if (State == Phase.Flying)
                    return 0f;
                if (State == Phase.Dazed)
                    return 1f;
                var up = (_time - _settings.FlightSeconds - _settings.DazedSeconds) / Math.Max(0.01f, _settings.GetUpSeconds);
                return Math.Max(0f, 1f - up);
            }
        }

        public void Tick(float deltaTime) => _time += deltaTime;

        /// <summary>
        /// Where a chef hit at <paramref name="chef"/> by a car heading <paramref name="carDirection"/>
        /// comes down: thrown <paramref name="distance"/> metres along the car's way, onto whichever
        /// pavement is nearer, and never past either end of the scene (or they'd land out of it).
        /// </summary>
        public static Vector2 LandingFor(Vector2 chef, Vector2 carDirection, float distance, float nearPavementZ, float farPavementZ, float westEnd, float eastEnd)
        {
            var along = Math.Abs(carDirection.X) > 1e-3f ? Math.Sign(carDirection.X) : 1f;
            var x = Math.Max(westEnd, Math.Min(eastEnd, chef.X + along * distance));
            var z = Math.Abs(chef.Y - nearPavementZ) <= Math.Abs(chef.Y - farPavementZ) ? nearPavementZ : farPavementZ;
            return new Vector2(x, z);
        }

        /// <summary>
        /// Whether a car reaches a chef: the chef's body (a circle) against the car's footprint, a
        /// box <paramref name="carHalfSize"/> (half its width, half its length) about its middle,
        /// its length along <paramref name="carForward"/>.
        /// </summary>
        public static bool Reaches(Vector2 chef, float chefRadius, Vector2 carMiddle, Vector2 carForward, Vector2 carHalfSize)
        {
            var forward = carForward.LengthSquared() > 1e-6f ? Vector2.Normalize(carForward) : Vector2.UnitX;
            var side = new Vector2(forward.Y, -forward.X);
            var offset = chef - carMiddle;
            var along = Math.Abs(Vector2.Dot(offset, forward));
            var across = Math.Abs(Vector2.Dot(offset, side));
            return along <= carHalfSize.Y + chefRadius && across <= carHalfSize.X + chefRadius;
        }

        /// <summary>
        /// Whether a car is about to reach a chef, for its horn: the chef is ahead of it in its lane,
        /// within <paramref name="seconds"/> of driving at <paramref name="speed"/>.
        /// </summary>
        public static bool BearsDownOn(Vector2 chef, float chefRadius, Vector2 carMiddle, Vector2 carForward, Vector2 carHalfSize, float speed, float seconds)
        {
            var forward = carForward.LengthSquared() > 1e-6f ? Vector2.Normalize(carForward) : Vector2.UnitX;
            var side = new Vector2(forward.Y, -forward.X);
            var offset = chef - carMiddle;
            var ahead = Vector2.Dot(offset, forward);
            var across = Math.Abs(Vector2.Dot(offset, side));
            return ahead > carHalfSize.Y && ahead <= carHalfSize.Y + chefRadius + speed * seconds && across <= carHalfSize.X + chefRadius;
        }
    }

    /// <summary>Tuning for fetching back a chef who walked off. Distances in metres, times in seconds.</summary>
    public sealed class EscortSettings
    {
        /// <summary>How long the chef stands out there before anyone comes.</summary>
        public float WaitSeconds = 1.2f;
        /// <summary>How far beyond the chef the bodyguard comes in from.</summary>
        public float ComesFrom = 4f;
        /// <summary>The bodyguard's pace, with or without a chef under his arm.</summary>
        public float Speed = 6f;
        /// <summary>How long setting the chef down takes.</summary>
        public float PutDownSeconds = 0.5f;
    }

    /// <summary>
    /// A chef who walked out of the scene is brought back (the user's idea, 2026-11-12: "let the
    /// player leave the scene but also send them back again with a funny message"): they stand
    /// where they stopped for a moment, one of the boss's bodyguards jogs in from further out,
    /// tucks them under an arm, carries them to the restaurant's doorstep, sets them down, and
    /// jogs back the way he came. It costs the seconds; what they carried comes back with them.
    /// </summary>
    public sealed class Escort
    {
        public enum Phase { Waiting, Fetching, Carrying, PuttingDown, Leaving, Done }

        private readonly EscortSettings _settings;
        private readonly Vector2 _chefAt;
        private readonly Vector2 _from;
        private float _timer;

        /// <param name="chefAt">Where the chef stopped, outside the scene.</param>
        /// <param name="doorstep">Where they're set down.</param>
        /// <param name="outward">Which way is further out from there: the bodyguard comes in from that side.</param>
        public Escort(Vector2 chefAt, Vector2 doorstep, Vector2 outward, EscortSettings settings)
        {
            _settings = settings;
            _chefAt = chefAt;
            Doorstep = doorstep;
            var away = outward.LengthSquared() > 1e-6f ? Vector2.Normalize(outward) : Vector2.UnitX;
            _from = chefAt + away * settings.ComesFrom;
            Guard = _from;
            Chef = chefAt;
            _timer = settings.WaitSeconds;
        }

        public Phase State { get; private set; } = Phase.Waiting;
        public Vector2 Doorstep { get; }
        /// <summary>Where the bodyguard is.</summary>
        public Vector2 Guard { get; private set; }
        /// <summary>Where the chef is.</summary>
        public Vector2 Chef { get; private set; }
        /// <summary>Which way the bodyguard is heading.</summary>
        public Vector2 Heading { get; private set; } = Vector2.UnitX;
        /// <summary>True while the bodyguard is in the scene.</summary>
        public bool GuardShown => State != Phase.Waiting && State != Phase.Done;
        /// <summary>True while the chef is under his arm.</summary>
        public bool Carried => State == Phase.Carrying;
        /// <summary>True from the moment the chef is on their own feet again.</summary>
        public bool ChefFree => State == Phase.Leaving || State == Phase.Done;

        public void Tick(float deltaTime)
        {
            switch (State)
            {
                case Phase.Waiting:
                    _timer -= deltaTime;
                    if (_timer <= 0f)
                        State = Phase.Fetching;
                    break;
                case Phase.Fetching:
                    if (Walk(_chefAt, deltaTime))
                        State = Phase.Carrying;
                    break;
                case Phase.Carrying:
                    var there = Walk(Doorstep, deltaTime);
                    Chef = Guard;
                    if (there)
                    {
                        Chef = Doorstep;
                        State = Phase.PuttingDown;
                        _timer = _settings.PutDownSeconds;
                    }
                    break;
                case Phase.PuttingDown:
                    _timer -= deltaTime;
                    if (_timer <= 0f)
                        State = Phase.Leaving;
                    break;
                case Phase.Leaving:
                    if (Walk(_from, deltaTime))
                        State = Phase.Done;
                    break;
            }
        }

        // Moves the bodyguard towards a point at his pace; true once he's there.
        private bool Walk(Vector2 to, float deltaTime)
        {
            var way = to - Guard;
            var distance = way.Length();
            var step = _settings.Speed * deltaTime;
            if (distance <= step || distance < 1e-4f)
            {
                Guard = to;
                return true;
            }
            Heading = way / distance;
            Guard += Heading * step;
            return false;
        }
    }

    /// <summary>The scene's edges for a chef on foot: past them they've left, and are fetched back.</summary>
    public readonly struct SceneEdges
    {
        public SceneEdges(float west, float east, float south)
        {
            West = west;
            East = east;
            South = south;
        }

        /// <summary>The furthest west a chef is still in the scene (X); beyond the pavement's end.</summary>
        public float West { get; }
        public float East { get; }
        /// <summary>The furthest south (Z): into the park across the road.</summary>
        public float South { get; }

        public bool HasLeft(Vector2 chef) => chef.X < West || chef.X > East || chef.Y < South;

        /// <summary>Which way is further out from where a chef left: the bodyguard comes in from there.</summary>
        public Vector2 Outward(Vector2 chef) =>
            chef.X < West ? -Vector2.UnitX : chef.X > East ? Vector2.UnitX : -Vector2.UnitY;
    }
}
