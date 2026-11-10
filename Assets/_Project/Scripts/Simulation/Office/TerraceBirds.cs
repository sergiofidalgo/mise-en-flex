using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation.Office
{
    /// <summary>Tuning for the terrace's pigeons. Distances in metres, times in seconds.</summary>
    public sealed class BirdSettings
    {
        /// <summary>A chef this close (across the ground) startles a perched bird.</summary>
        public float StartleDistance = 2.5f;
        /// <summary>The longest a startled bird waits before taking off: each draws its own, so a group doesn't leave in step.</summary>
        public float StartleDelayMax = 0.2f;
        /// <summary>A perched bird this close to one that's been startled goes too, so they leave as a little flock.</summary>
        public float FlockDistance = 3f;
        /// <summary>How long the flight away takes, from the perch to out of sight.</summary>
        public float FlyOffSeconds = 2.5f;
        /// <summary>How far across the ground the flight away goes.</summary>
        public float FlyOffDistance = 25f;
        /// <summary>How the flight climbs: the arc's rise. With <see cref="FlyOffDip"/> it peaks at about 5.5 m.</summary>
        public float FlyOffRise = 7.5f;
        /// <summary>How far below the perch the flight ends, dropping out of sight towards the roofs.</summary>
        public float FlyOffDip = 8f;
        /// <summary>The way out, across the ground (X, Z): over the rail. Flights lean this way, so a bird flies off the terrace rather than into the office.</summary>
        public Vector2 Outward = new Vector2(0f, 1f);
        /// <summary>How much flights lean the way out, against straight away from the chef.</summary>
        public float OutwardWeight = 1f;
        /// <summary>The shortest a bird stays away. Less than zero: it never comes back.</summary>
        public float AwayMin = 20f;
        /// <summary>The longest it stays away.</summary>
        public float AwayMax = 40f;
        /// <summary>How long the glide back to the perch takes.</summary>
        public float ReturnSeconds = 2.5f;
        /// <summary>A bird only lands when no chef is this close to its perch: wider than the startle distance, so it doesn't land and flee at once.</summary>
        public float LandClearDistance = 4f;
    }

    /// <summary>
    /// One pigeon on the terrace (user's request, 2026-11-10): it pecks on its perch until a chef
    /// comes close, then flies off over the rail and out of sight, and glides back to the same perch a
    /// while later once nobody's standing by it. Its flight is one arc, out and up and then down
    /// towards the roofs; the way back is the same arc in reverse, slowing to land.
    /// </summary>
    public sealed class TerraceBird
    {
        public enum Phase { Perched, Startled, FlyingOff, Away, Returning }

        private readonly BirdSettings _settings;
        private readonly Random _random;
        private float _timer;
        // Where along its arc it is: 0 on the perch, 1 out of sight.
        private float _along;
        // How far through the glide back it is, 0–1.
        private float _landing;

        public TerraceBird(Vector3 perch, BirdSettings settings, Random random)
        {
            Perch = perch;
            Position = perch;
            _settings = settings;
            _random = random;
        }

        public Vector3 Perch { get; }
        public Phase State { get; private set; } = Phase.Perched;
        public Vector3 Position { get; private set; }
        /// <summary>Where along its flight it is: 0 on the perch, 1 out of sight.</summary>
        public float Along => _along;
        /// <summary>Which way across the ground (X, Z) the flight goes.</summary>
        public Vector2 Direction { get; private set; }
        public bool Visible => State != Phase.Away;
        public bool Flying => State == Phase.FlyingOff || State == Phase.Returning;

        private Vector2 PerchOnGround => new Vector2(Perch.X, Perch.Z);
        private Vector2 OnGround => new Vector2(Position.X, Position.Z);

        /// <summary>
        /// Startles it, if it's on its perch: it takes off after a short random wait, the way that
        /// leads away from <paramref name="from"/> and out. Returns whether it was startled now.
        /// </summary>
        public bool Startle(Vector2 from)
        {
            if (State != Phase.Perched)
                return false;
            State = Phase.Startled;
            _timer = (float)_random.NextDouble() * Math.Max(0f, _settings.StartleDelayMax);
            Direction = FleeDirection(PerchOnGround, from, _settings.Outward, _settings.OutwardWeight);
            return true;
        }

        /// <summary>Advances it, given how near the nearest chef is to it and to its perch.</summary>
        public void Tick(float deltaTime, Vector2? nearestChef)
        {
            switch (State)
            {
                case Phase.Perched:
                    if (nearestChef.HasValue && Vector2.Distance(nearestChef.Value, PerchOnGround) < _settings.StartleDistance)
                        Startle(nearestChef.Value);
                    break;
                case Phase.Startled:
                    _timer -= deltaTime;
                    if (_timer <= 0f)
                    {
                        State = Phase.FlyingOff;
                        _along = 0f;
                    }
                    break;
                case Phase.FlyingOff:
                    _along += deltaTime / Math.Max(0.01f, _settings.FlyOffSeconds);
                    if (_along >= 1f)
                    {
                        _along = 1f;
                        State = Phase.Away;
                        _timer = _settings.AwayMin < 0f ? float.PositiveInfinity
                            : _settings.AwayMin + (float)_random.NextDouble() * Math.Max(0f, _settings.AwayMax - _settings.AwayMin);
                    }
                    break;
                case Phase.Away:
                    _timer -= deltaTime;
                    var clear = !nearestChef.HasValue || Vector2.Distance(nearestChef.Value, PerchOnGround) >= _settings.LandClearDistance;
                    if (_timer <= 0f && clear)
                    {
                        State = Phase.Returning;
                        _landing = 0f;
                    }
                    break;
                case Phase.Returning:
                    // A chef stepping up to it as it comes in sends it off again, from where it is.
                    if (nearestChef.HasValue && Vector2.Distance(nearestChef.Value, OnGround) < _settings.StartleDistance)
                    {
                        State = Phase.FlyingOff;
                        break;
                    }
                    _landing += deltaTime / Math.Max(0.01f, _settings.ReturnSeconds);
                    // Back along the arc, quick at first and slowing to land: 1 − (1 − t)² eases out.
                    var eased = 1f - (1f - Math.Min(1f, _landing)) * (1f - Math.Min(1f, _landing));
                    _along = 1f - eased;
                    if (_landing >= 1f)
                    {
                        _along = 0f;
                        State = Phase.Perched;
                    }
                    break;
            }
            Position = Arc(Perch, Direction, _along, _settings);
        }

        /// <summary>
        /// Where along its flight it is, from the perch (0) to out of sight (1): straight on across
        /// the ground, and up in a parabola that peaks early and ends below the perch. The height is
        /// rise × 4t(1 − t), a hump that's 0 at both ends, less dip × t², which pulls the end down.
        /// </summary>
        public static Vector3 Arc(Vector3 perch, Vector2 direction, float along, BirdSettings settings)
        {
            var t = Math.Max(0f, Math.Min(1f, along));
            var across = direction * (settings.FlyOffDistance * t);
            var up = settings.FlyOffRise * 4f * t * (1f - t) - settings.FlyOffDip * t * t;
            return perch + new Vector3(across.X, up, across.Y);
        }

        /// <summary>
        /// The way to fly: away from the chef, leaning the way out. Straight out if the chef is right
        /// on top of it, or the two cancel.
        /// </summary>
        public static Vector2 FleeDirection(Vector2 bird, Vector2 chef, Vector2 outward, float outwardWeight)
        {
            var away = bird - chef;
            away = away.LengthSquared() > 1e-6f ? Vector2.Normalize(away) : Vector2.Zero;
            var way = outward.LengthSquared() > 1e-6f ? Vector2.Normalize(outward) : Vector2.UnitY;
            var lean = away + way * outwardWeight;
            return lean.LengthSquared() > 1e-6f ? Vector2.Normalize(lean) : way;
        }
    }

    /// <summary>The terrace's pigeons together: each reacts to the nearest chef, and a startled one takes its neighbours with it.</summary>
    public sealed class TerraceBirds
    {
        private readonly BirdSettings _settings;
        private readonly List<TerraceBird> _birds = new List<TerraceBird>();
        private readonly Queue<TerraceBird> _spreading = new Queue<TerraceBird>();

        public TerraceBirds(IEnumerable<Vector3> perches, BirdSettings settings, int seed)
        {
            _settings = settings;
            var random = new Random(seed);
            foreach (var perch in perches)
                _birds.Add(new TerraceBird(perch, settings, random));
        }

        public IReadOnlyList<TerraceBird> Birds => _birds;

        /// <summary>Advances them all, given where the chefs are on the ground (X, Z).</summary>
        public void Tick(float deltaTime, IReadOnlyList<Vector2> chefs)
        {
            foreach (var bird in _birds)
            {
                var wasPerched = bird.State == TerraceBird.Phase.Perched;
                // Away, what matters is who's by its perch; otherwise who's by the bird.
                var at = bird.State == TerraceBird.Phase.Away ? bird.Perch : bird.Position;
                var chef = Nearest(chefs, new Vector2(at.X, at.Z));
                bird.Tick(deltaTime, chef);
                if (wasPerched && bird.State == TerraceBird.Phase.Startled)
                    Spread(bird, chef.Value);
            }
        }

        // A startled bird startles its perched neighbours, and they theirs.
        private void Spread(TerraceBird first, Vector2 chef)
        {
            _spreading.Enqueue(first);
            while (_spreading.Count > 0)
            {
                var from = _spreading.Dequeue();
                foreach (var other in _birds)
                    if (other.State == TerraceBird.Phase.Perched
                        && Vector2.Distance(new Vector2(other.Perch.X, other.Perch.Z), new Vector2(from.Perch.X, from.Perch.Z)) < _settings.FlockDistance
                        && other.Startle(chef))
                        _spreading.Enqueue(other);
            }
        }

        private static Vector2? Nearest(IReadOnlyList<Vector2> chefs, Vector2 to)
        {
            Vector2? nearest = null;
            var best = float.MaxValue;
            foreach (var chef in chefs)
            {
                var distance = Vector2.DistanceSquared(chef, to);
                if (distance < best)
                {
                    best = distance;
                    nearest = chef;
                }
            }
            return nearest;
        }
    }
}
