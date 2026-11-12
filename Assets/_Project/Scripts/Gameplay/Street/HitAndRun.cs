using System;
using System.Collections.Generic;
using Game.Gameplay.Stations;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Street;
using UnityEngine;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Gameplay.Street
{
    /// <summary>
    /// The road is open to chefs, and cars don't stop (the user's idea, 2026-11-12: hit and runs,
    /// "in a funny way", for a chef "in the wrong place at the wrong time"). A car that reaches a
    /// chef throws them along its way onto the nearer pavement, where they lie dazed a moment and
    /// get up (<see cref="Knockdown"/>). Nobody is hurt. It costs the seconds and the food they
    /// carried: a plate, pan or tray stays in their hands, emptied, because the kitchen can't
    /// replace those mid-service. A car sounds its horn at a chef ahead of it first, so the hit is
    /// the player's own doing.
    /// </summary>
    public sealed class HitAndRun : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private RoadVehicle[] _vehicles = new RoadVehicle[0];

        [Header("Where they land")]
        [Tooltip("The Z of the middle of the pavement by the restaurant, and of the one across the road.")]
        [SerializeField] private float _nearPavementZ = -9.4f;
        [SerializeField] private float _farPavementZ = -18.2f;
        [Tooltip("The furthest west and east (X) anyone is thrown: inside the scene's ends.")]
        [SerializeField] private float _westEnd = -16f;
        [SerializeField] private float _eastEnd = 12f;

        [Header("The hit")]
        [Tooltip("Metres thrown along the car's way.")]
        [SerializeField, Min(0f)] private float _distance = 4f;
        [Tooltip("Metres: the top of the arc.")]
        [SerializeField, Min(0f)] private float _height = 2.4f;
        [SerializeField, Min(0.1f)] private float _flightSeconds = 0.9f;
        [Tooltip("Times they turn head over heels in the air.")]
        [SerializeField, Min(0f)] private float _tumbles = 2f;
        [Tooltip("Seconds lying on the pavement seeing stars.")]
        [SerializeField, Min(0f)] private float _dazedSeconds = 1.5f;
        [SerializeField, Min(0.01f)] private float _getUpSeconds = 0.35f;
        [Tooltip("A car slower than this (m/s) is as good as parked: it hits nobody.")]
        [SerializeField, Min(0f)] private float _slowest = 1f;

        [Header("The horn")]
        [Tooltip("Seconds of driving ahead of itself a car warns chefs in.")]
        [SerializeField, Min(0f)] private float _hornSeconds = 1f;
        [Tooltip("Seconds before the same car sounds it again.")]
        [SerializeField, Min(0f)] private float _hornEvery = 2.5f;

        // The middle of a chef's body above their feet: what they tumble about in the air.
        private const float BodyMiddle = 0.8f;

        private sealed class Fall
        {
            public Knockdown Knock;
            public float Floor;
            public bool Down;
        }

        private readonly Dictionary<PlayerInteractor, Fall> _falls = new Dictionary<PlayerInteractor, Fall>();
        private readonly List<PlayerInteractor> _done = new List<PlayerInteractor>();
        private readonly Dictionary<RoadVehicle, float> _hornAt = new Dictionary<RoadVehicle, float>();

        /// <summary>A car hit a chef: they're in the air.</summary>
        public event Action<PlayerInteractor, RoadVehicle> Struck;
        /// <summary>They came down, and lie dazed.</summary>
        public event Action<PlayerInteractor> Landed;
        /// <summary>They're up, and the player's again.</summary>
        public event Action<PlayerInteractor> Recovered;
        /// <summary>A car sounds its horn at a chef ahead of it.</summary>
        public event Action<RoadVehicle> Horn;

        /// <summary>How long a chef lies dazed, for whatever shows it.</summary>
        public float DazedSeconds => _dazedSeconds;

        /// <summary>True while this chef is in the air or on the ground from a hit.</summary>
        public bool IsDown(PlayerInteractor chef) => _falls.ContainsKey(chef);

        private void Update()
        {
            foreach (var chef in ChefHold.Chefs())
            {
                if (!ChefHold.IsFree(chef))
                    continue;
                var radius = chef.GetComponent<PlayerMovement>().BodyRadius;
                var at = Floor(chef.transform.position);
                foreach (var car in _vehicles)
                {
                    if (car == null || !car.isActiveAndEnabled || car.Speed < _slowest)
                        continue;
                    var middle = Floor(car.transform.position);
                    var forward = Floor(car.transform.forward);
                    if (Knockdown.Reaches(at, radius, middle, forward, ToSim(car.HalfSize)))
                    {
                        Strike(chef, car, at, forward);
                        break;
                    }
                    if (Knockdown.BearsDownOn(at, radius, middle, forward, ToSim(car.HalfSize), car.Speed, _hornSeconds)
                        && (!_hornAt.TryGetValue(car, out var last) || Time.time - last >= _hornEvery))
                    {
                        _hornAt[car] = Time.time;
                        Horn?.Invoke(car);
                    }
                }
            }

            _done.Clear();
            foreach (var pair in _falls)
            {
                var chef = pair.Key;
                var fall = pair.Value;
                fall.Knock.Tick(Time.deltaTime);
                Pose(chef, fall);
                if (!fall.Down && fall.Knock.State != Knockdown.Phase.Flying)
                {
                    fall.Down = true;
                    Landed?.Invoke(chef);
                }
                if (fall.Knock.State == Knockdown.Phase.Done)
                    _done.Add(chef);
            }
            foreach (var chef in _done)
            {
                var fall = _falls[chef];
                _falls.Remove(chef);
                ChefHold.Release(chef, fall.Knock.Landing, new Vector3(fall.Knock.Direction.X, 0f, fall.Knock.Direction.Y));
                Recovered?.Invoke(chef);
            }
        }

        private void Strike(PlayerInteractor chef, RoadVehicle car, SimVector2 at, SimVector2 carForward)
        {
            var settings = new KnockdownSettings
            {
                Distance = _distance, Height = _height, FlightSeconds = _flightSeconds, Tumbles = _tumbles,
                DazedSeconds = _dazedSeconds, GetUpSeconds = _getUpSeconds,
            };
            var landing = Knockdown.LandingFor(at, carForward, _distance, _nearPavementZ, _farPavementZ, _westEnd, _eastEnd);
            Spill(chef);
            var floor = chef.transform.position.y;
            ChefHold.Take(chef);
            _falls[chef] = new Fall { Knock = new Knockdown(at, landing, settings), Floor = floor };
            Struck?.Invoke(chef, car);
        }

        // In the air they turn head over heels about their middle; on the ground they lie on their
        // back, feet where they landed, and come upright again about their feet.
        private void Pose(PlayerInteractor chef, Fall fall)
        {
            var knock = fall.Knock;
            var along = Quaternion.LookRotation(new Vector3(knock.Direction.X, 0f, knock.Direction.Y), Vector3.up);
            var position = new Vector3(knock.Position.X, fall.Floor + knock.Position.Y, knock.Position.Z);
            if (knock.State == Knockdown.Phase.Flying)
            {
                var turned = along * Quaternion.Euler(knock.Tumble, 0f, 0f);
                var middle = position + Vector3.up * BodyMiddle;
                chef.transform.SetPositionAndRotation(middle - turned * (Vector3.up * BodyMiddle), turned);
            }
            else
                chef.transform.SetPositionAndRotation(position, along * Quaternion.Euler(-90f * knock.Lying, 0f, 0f));
        }

        // The food goes flying: off the plate, out of the pan, off the tray, out of their hands.
        // What holds food stays with them, empty.
        private void Spill(PlayerInteractor chef)
        {
            var held = chef.Hands.Current;
            if (held == null)
                return;
            if (held is IItemCollection holder)
                Empty(holder);
            else
                Lose(chef.Hands.Take());
            chef.RefreshHeld(chef.transform.position);
        }

        private void Empty(IItemCollection holder)
        {
            if (holder is Tray tray)
            {
                for (var i = tray.Items.Count - 1; i >= 0; i--)
                {
                    if (tray.Items[i] is IItemCollection inner)
                        Empty(inner);
                    else
                        Lose(tray.TakeAt(i));
                }
                return;
            }
            // A plate's or a pan's food, one piece at a time. Bounded, in case something holds what it won't give up.
            for (var guard = 0; guard < 32 && holder.CanTake; guard++)
                Lose(holder.Take());
        }

        private void Lose(Item item)
        {
            if (item == null)
                return;
            var services = _root.Services;
            if (item is Ingredient ingredient)
                services.Cooking.Remove(ingredient);
            services.ItemViews.Release(item);
        }

        private static SimVector2 Floor(Vector3 v) => new SimVector2(v.x, v.z);

        private static SimVector2 ToSim(Vector2 v) => new SimVector2(v.x, v.y);
    }
}
