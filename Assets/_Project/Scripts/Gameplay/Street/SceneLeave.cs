using System;
using System.Collections.Generic;
using Game.Gameplay.Customers;
using Game.Gameplay.Stations;
using Game.Simulation.Street;
using UnityEngine;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Gameplay.Street
{
    /// <summary>
    /// The street has no walls at its ends: a chef can walk off along the pavement or the road, or
    /// away through the park (the user's idea, 2026-11-12: "let the player leave the scene but also
    /// send them back again with a funny message"). Past the scene's edge they stop, one of the
    /// boss's bodyguards jogs in, tucks them under an arm and carries them back to the restaurant's
    /// doorstep (<see cref="Escort"/>). It costs the seconds; what they carried comes back with
    /// them. Each chef is fetched on their own; the others play on.
    /// </summary>
    public sealed class SceneLeave : MonoBehaviour
    {
        [SerializeField] private CustomerDirector _director;
        [Tooltip("The bodyguard who fetches them: PF_Bodyguard.")]
        [SerializeField] private GameObject _guardPrefab;

        [Header("The scene's edges")]
        [Tooltip("Past these a chef has left: west and east along the street (X), south into the park (Z).")]
        [SerializeField] private float _west = -17.5f;
        [SerializeField] private float _east = 13.5f;
        [SerializeField] private float _south = -22.5f;

        [Header("Fetching them back")]
        [Tooltip("Seconds they stand out there before he comes.")]
        [SerializeField, Min(0f)] private float _waitSeconds = 1.2f;
        [Tooltip("Metres beyond them he comes in from.")]
        [SerializeField, Min(0f)] private float _comesFrom = 4f;
        [Tooltip("His pace, in metres a second.")]
        [SerializeField, Min(0.1f)] private float _speed = 6f;
        [SerializeField, Min(0f)] private float _putDownSeconds = 0.5f;
        [Tooltip("Metres out from the door they're set down.")]
        [SerializeField, Min(0f)] private float _doorstep = 0.9f;

        private sealed class Trip
        {
            public Escort Escort;
            public GameObject Guard;
            public float Floor;
            public bool Released;
        }

        private readonly Dictionary<PlayerInteractor, Trip> _trips = new Dictionary<PlayerInteractor, Trip>();
        private readonly List<PlayerInteractor> _done = new List<PlayerInteractor>();

        /// <summary>A chef walked out of the scene.</summary>
        public event Action<PlayerInteractor> Left;
        /// <summary>A chef was set down on the doorstep again, and is the player's.</summary>
        public event Action<PlayerInteractor> Returned;

        /// <summary>True while this chef is out of the scene or being carried back.</summary>
        public bool IsAway(PlayerInteractor chef) => _trips.TryGetValue(chef, out var trip) && !trip.Released;

        private void Update()
        {
            var edges = new SceneEdges(_west, _east, _south);
            foreach (var chef in ChefHold.Chefs())
            {
                if (!ChefHold.IsFree(chef) || _trips.ContainsKey(chef))
                    continue;
                var at = Floor(chef.transform.position);
                if (edges.HasLeft(at))
                    Fetch(chef, at, edges.Outward(at));
            }

            _done.Clear();
            foreach (var pair in _trips)
            {
                var chef = pair.Key;
                var trip = pair.Value;
                trip.Escort.Tick(Time.deltaTime);
                Pose(chef, trip);
                if (!trip.Released && trip.Escort.ChefFree)
                {
                    trip.Released = true;
                    ChefHold.Release(chef, trip.Escort.Doorstep, DoorFacing());
                    Returned?.Invoke(chef);
                }
                if (trip.Escort.State == Escort.Phase.Done)
                    _done.Add(chef);
            }
            foreach (var chef in _done)
            {
                if (_trips[chef].Guard != null)
                    Destroy(_trips[chef].Guard);
                _trips.Remove(chef);
            }
        }

        private void Fetch(PlayerInteractor chef, SimVector2 at, SimVector2 outward)
        {
            var settings = new EscortSettings { WaitSeconds = _waitSeconds, ComesFrom = _comesFrom, Speed = _speed, PutDownSeconds = _putDownSeconds };
            var door = _director != null ? _director.Door : null;
            var doorstep = door != null ? Floor(door.position - door.forward * _doorstep) : SimVector2.Zero;
            var trip = new Trip { Escort = new Escort(at, doorstep, outward, settings), Floor = chef.transform.position.y };
            if (_guardPrefab != null)
            {
                trip.Guard = Instantiate(_guardPrefab, transform);
                trip.Guard.name = "Bodyguard_Fetching";
                // Only his body and his walk: nothing here seats him, pushes him or sends him anywhere.
                foreach (var agent in trip.Guard.GetComponents<WalkingAgent>())
                    agent.enabled = false;
                foreach (var body in trip.Guard.GetComponentsInChildren<Collider>())
                    body.enabled = false;
                trip.Guard.SetActive(false);
            }
            ChefHold.Take(chef);
            _trips[chef] = trip;
            Left?.Invoke(chef);
        }

        private void Pose(PlayerInteractor chef, Trip trip)
        {
            var escort = trip.Escort;
            var heading = Quaternion.LookRotation(new Vector3(escort.Heading.X, 0f, escort.Heading.Y), Vector3.up);
            var guardAt = new Vector3(escort.Guard.X, trip.Floor, escort.Guard.Y);
            if (trip.Guard != null)
            {
                if (trip.Guard.activeSelf != escort.GuardShown)
                    trip.Guard.SetActive(escort.GuardShown);
                trip.Guard.transform.SetPositionAndRotation(guardAt, heading);
            }
            if (trip.Released)
                return;
            if (escort.Carried)
            {
                // Under his arm: lying forward along his way at his hip, head first, feet trailing.
                var tucked = heading * Quaternion.Euler(80f, 0f, 0f);
                var hip = guardAt + heading * new Vector3(0.42f, 1.05f, -0.75f);
                chef.transform.SetPositionAndRotation(hip, tucked);
            }
            else if (escort.State == Escort.Phase.PuttingDown)
                chef.transform.SetPositionAndRotation(new Vector3(escort.Chef.X, trip.Floor, escort.Chef.Y), Quaternion.LookRotation(DoorFacing(), Vector3.up));
        }

        // Set down facing the restaurant's door.
        private Vector3 DoorFacing()
        {
            var door = _director != null ? _director.Door : null;
            var facing = door != null ? door.forward : Vector3.forward;
            facing.y = 0f;
            return facing.sqrMagnitude > 1e-6f ? facing.normalized : Vector3.forward;
        }

        private static SimVector2 Floor(Vector3 v) => new SimVector2(v.x, v.z);
    }
}
