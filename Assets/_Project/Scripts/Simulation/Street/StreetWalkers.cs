using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation.Street
{
    /// <summary>Tuning for the people who walk past the restaurant. Distances in metres, times in seconds; positions are (X, Z) on the ground.</summary>
    public sealed class WalkerSettings
    {
        /// <summary>Where they come in and leave: off screen beyond each end of the pavement.</summary>
        public float West = -18f;
        public float East = 14f;
        /// <summary>Each lane's Z. Even lanes walk east, odd ones west, so nobody meets head on.</summary>
        public float[] Lanes = { -8.35f, -9.85f };
        public float SpeedMin = 1f;
        public float SpeedMax = 1.7f;
        /// <summary>How long one stays away before coming round again as someone new.</summary>
        public float AwayMin = 2f;
        public float AwayMax = 16f;

        /// <summary>Someone this close (a chef, a guest, a reader at the board) makes them step aside.</summary>
        public float AvoidDistance = 1.3f;
        /// <summary>How far aside they step.</summary>
        public float SideStep = 0.7f;
        public float SideStepSpeed = 1.6f;
        /// <summary>The pavement's edges for them: they never step past these.</summary>
        public float MinZ = -10f;
        public float MaxZ = -8f;

        /// <summary>Whether there's a menu board to read.</summary>
        public bool HasBoard;
        /// <summary>Where the board stands, and where its reader does.</summary>
        public Vector2 Board;
        public Vector2 BoardSpot;
        /// <summary>The lane that passes the board; only its walkers stop.</summary>
        public int BoardLane;
        /// <summary>The share of that lane's walkers who stop to read.</summary>
        public float ReadChance = 0.3f;
        public float ReadMin = 3f;
        public float ReadMax = 7f;
    }

    /// <summary>
    /// One person walking past (the user's request, 2026-11-11: people roaming the street as in the
    /// city): in at one end of the pavement, along their lane, out at the other, and round again a
    /// while later as someone else. They step aside for whoever's in their way and never block
    /// anyone; some stop at the menu board, read it, and walk on.
    /// </summary>
    public sealed class StreetWalker
    {
        public enum Phase { Away, Walking, Reading }

        private readonly WalkerSettings _settings;
        private readonly Random _random;
        private float _timer;
        private float _speed;
        private float _x;
        private float _offset;
        private bool _willRead;

        public StreetWalker(int lane, WalkerSettings settings, Random random)
        {
            Lane = lane;
            _settings = settings;
            _random = random;
            Direction = lane % 2 == 0 ? 1 : -1;
        }

        public int Lane { get; }
        /// <summary>+1 walking east, −1 west.</summary>
        public int Direction { get; }
        public Phase State { get; private set; } = Phase.Away;
        public bool Visible => State != Phase.Away;
        /// <summary>How many times they've come in: each time it's a new person.</summary>
        public int Arrivals { get; private set; }
        public Vector2 Position => new Vector2(_x, LaneZ + _offset);
        /// <summary>Which way they face: along the street, or at the board while reading.</summary>
        public Vector2 Facing { get; private set; }

        private float LaneZ => _settings.Lanes[Lane];
        private float Start => Direction > 0 ? _settings.West : _settings.East;
        private float End => Direction > 0 ? _settings.East : _settings.West;

        /// <summary>Puts them part of the way along already, so the street isn't empty as the day begins.</summary>
        public void StartAlong(float share)
        {
            Enter();
            _x = Start + (End - Start) * Math.Max(0f, Math.Min(1f, share));
        }

        /// <summary>Keeps them away for <paramref name="seconds"/> before they first come in.</summary>
        public void StartAway(float seconds)
        {
            State = Phase.Away;
            _timer = seconds;
        }

        /// <summary>Advances them, given everyone to keep clear of and whether the board is free.</summary>
        public void Tick(float deltaTime, IReadOnlyList<Vector2> people, bool boardFree)
        {
            switch (State)
            {
                case Phase.Away:
                    _timer -= deltaTime;
                    if (_timer <= 0f)
                        Enter();
                    break;
                case Phase.Walking:
                    var before = _x;
                    _x += Direction * _speed * deltaTime;
                    Step(StepAside(people), deltaTime);
                    if (_willRead && Passed(before, _x, _settings.BoardSpot.X))
                    {
                        _willRead = false;
                        if (boardFree)
                        {
                            _x = _settings.BoardSpot.X;
                            State = Phase.Reading;
                            _timer = Between(_settings.ReadMin, _settings.ReadMax);
                        }
                    }
                    if ((End - _x) * Direction <= 0f)
                    {
                        State = Phase.Away;
                        _timer = Between(_settings.AwayMin, _settings.AwayMax);
                    }
                    break;
                case Phase.Reading:
                    // Up to the spot in front of the board, then stand and read.
                    Step(_settings.BoardSpot.Y - LaneZ, deltaTime);
                    var toBoard = _settings.Board - Position;
                    if (toBoard.LengthSquared() > 1e-6f)
                        Facing = Vector2.Normalize(toBoard);
                    _timer -= deltaTime;
                    if (_timer <= 0f)
                    {
                        State = Phase.Walking;
                        Facing = new Vector2(Direction, 0f);
                    }
                    break;
            }
        }

        private void Enter()
        {
            State = Phase.Walking;
            Arrivals++;
            _x = Start;
            _offset = 0f;
            _speed = Between(_settings.SpeedMin, _settings.SpeedMax);
            Facing = new Vector2(Direction, 0f);
            _willRead = _settings.HasBoard && Lane == _settings.BoardLane && _random.NextDouble() < _settings.ReadChance;
        }

        // How far off their lane they want to be: away from the nearest person in their way, the
        // nearer the further, and back onto the lane once they're past. Stepping off, not stopping,
        // so nobody standing in a lane can hold the street up.
        private float StepAside(IReadOnlyList<Vector2> people)
        {
            var here = Position;
            var want = 0f;
            var strongest = 0f;
            foreach (var person in people)
            {
                var distance = Vector2.Distance(person, here);
                if (distance >= _settings.AvoidDistance)
                    continue;
                // Full step by a third of the way in, so they're clear before they're alongside.
                var push = Math.Min(1f, 3f * (1f - distance / _settings.AvoidDistance));
                if (push <= strongest)
                    continue;
                strongest = push;
                var side = here.Y - person.Y;
                // Dead ahead: towards whichever edge leaves more room.
                if (Math.Abs(side) < 0.05f)
                    side = _settings.MaxZ - LaneZ > LaneZ - _settings.MinZ ? 1f : -1f;
                want = Math.Sign(side) * _settings.SideStep * push;
            }
            return Math.Max(_settings.MinZ - LaneZ, Math.Min(_settings.MaxZ - LaneZ, want));
        }

        private void Step(float wanted, float deltaTime)
        {
            var most = _settings.SideStepSpeed * deltaTime;
            _offset += Math.Max(-most, Math.Min(most, wanted - _offset));
        }

        private static bool Passed(float before, float after, float mark) =>
            (before - mark) * (after - mark) <= 0f && before != after;

        private float Between(float min, float max) => min + (float)_random.NextDouble() * Math.Max(0f, max - min);
    }

    /// <summary>Everyone walking past the restaurant. One reader at the menu board at a time; the rest walk round them.</summary>
    public sealed class StreetWalkers
    {
        private readonly List<StreetWalker> _walkers = new List<StreetWalker>();
        private readonly List<Vector2> _inTheWay = new List<Vector2>();

        /// <summary><paramref name="count"/> walkers shared out over the lanes, some already on their way.</summary>
        public StreetWalkers(int count, WalkerSettings settings, int seed)
        {
            var random = new Random(seed);
            for (var i = 0; i < count; i++)
            {
                var walker = new StreetWalker(i % settings.Lanes.Length, settings, random);
                // Half are mid-street as the day starts; the others drift in over the first while.
                if (i % 2 == 0)
                    walker.StartAlong((float)random.NextDouble());
                else
                    walker.StartAway((float)random.NextDouble() * settings.AwayMax);
                _walkers.Add(walker);
            }
        }

        public IReadOnlyList<StreetWalker> Walkers => _walkers;

        /// <summary>Advances them all, given where chefs, guests and staff stand on the ground (X, Z).</summary>
        public void Tick(float deltaTime, IReadOnlyList<Vector2> people)
        {
            foreach (var walker in _walkers)
            {
                _inTheWay.Clear();
                _inTheWay.AddRange(people);
                var boardFree = true;
                foreach (var other in _walkers)
                {
                    if (other == walker || other.State != StreetWalker.Phase.Reading)
                        continue;
                    boardFree = false;
                    _inTheWay.Add(other.Position);
                }
                walker.Tick(deltaTime, _inTheWay, boardFree);
            }
        }
    }

    /// <summary>When the street's lamps and the houses' windows are lit.</summary>
    public static class StreetHours
    {
        /// <summary>Whether it's dark at <paramref name="hour"/> on the clock (which may pass 24): from <paramref name="duskHour"/> until <paramref name="dawnHour"/>.</summary>
        public static bool Dark(float hour, float duskHour = 19f, float dawnHour = 7f)
        {
            var onClock = hour - 24f * (float)Math.Floor(hour / 24f);
            return onClock >= duskHour || onClock < dawnHour;
        }

        /// <summary>
        /// How hard the kitchen's extractor is working, 0–1, from each hob's heat (0–1) and whether
        /// food is on it: an empty hot hob counts for <paramref name="idleShare"/> of one that's
        /// cooking, and <paramref name="fullAt"/> hobs cooking flat out give the full plume.
        /// </summary>
        public static float Extraction(IReadOnlyList<(float Heat, bool Cooking)> hobs, float idleShare = 0.3f, float fullAt = 2f)
        {
            var total = 0f;
            foreach (var (heat, cooking) in hobs)
                total += Math.Max(0f, Math.Min(1f, heat)) * (cooking ? 1f : idleShare);
            return Math.Max(0f, Math.Min(1f, total / Math.Max(0.01f, fullAt)));
        }
    }
}
