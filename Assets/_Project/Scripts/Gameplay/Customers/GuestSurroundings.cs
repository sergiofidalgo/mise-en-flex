using System;
using System.Collections.Generic;
using Game.Simulation.Quality;
using Game.Simulation.Service;
using UnityEngine;

namespace Game.Gameplay.Customers
{
    /// <summary>
    /// Connects the room to the guests (decisions 2026-11-11): reads where the customer door and the
    /// wheelie bin stand each time a guest is served, so it keeps working wherever either is moved,
    /// and which pieces stand outside their usual room. The rules are in <see cref="Surroundings"/>.
    /// </summary>
    public sealed class GuestSurroundings
    {
        /// <summary>The street's wheelie bin, by the name GreyboxStreet gives it.</summary>
        public const string BinName = "TrashContainer";

        private static readonly IReadOnlyList<OutOfPlacePiece> Nothing = Array.Empty<OutOfPlacePiece>();

        private readonly KitchenServices _services;
        private readonly Transform _door;
        private Transform _bin;
        private Collider _binBody;

        public GuestSurroundings(KitchenServices services, Transform door, Transform bin)
        {
            _services = services;
            _door = door;
            Bin = bin;
        }

        /// <summary>The bin, or null for none. Settable so a movable bin can hand over its own object.</summary>
        public Transform Bin
        {
            get => _bin;
            set
            {
                _bin = value;
                _binBody = value != null ? value.GetComponentInChildren<Collider>() : null;
            }
        }

        /// <summary>
        /// Pieces outside their usual room: appliances in the dining room, tables in the kitchen,
        /// each with the floor point beside it where its extra mess lands. Null for none; the
        /// composition root reads it from the layout (<see cref="LayoutEditor.OutOfTheirRooms"/>).
        /// </summary>
        public Func<IReadOnlyList<OutOfPlacePiece>> OutOfPlace { get; set; }

        // Metres in front of a piece's front edge where its extra mess lands: on the floor where
        // people stand to use it, not under it, so a mop can reach it.
        private const float MessInFront = 0.35f;

        /// <summary>The layout's out-of-place pieces as the simulation reads them.</summary>
        public static IReadOnlyList<OutOfPlacePiece> Describe(IReadOnlyList<Stations.Interactable> pieces)
        {
            if (pieces == null || pieces.Count == 0)
                return Nothing;
            var described = new List<OutOfPlacePiece>(pieces.Count);
            foreach (var piece in pieces)
            {
                if (piece == null)
                    continue;
                var kind = piece is Stations.TableStation ? OutOfPlaceKind.TableInKitchen : OutOfPlaceKind.ApplianceInDiningRoom;
                var at = piece.transform.position + piece.transform.forward * (piece.HalfSize.Y + MessInFront);
                described.Add(new OutOfPlacePiece(kind, at.x, at.z));
            }
            return described;
        }

        /// <summary>Metres from the door to the bin's nearest side, on the ground; infinity without both.</summary>
        public float BinDistance
        {
            get
            {
                if (_door == null || _bin == null)
                    return float.PositiveInfinity;
                var door = _door.position;
                // Its nearest side rather than its middle: a long bin's end can be by the door.
                var near = _binBody != null && _binBody.enabled ? _binBody.ClosestPoint(door) : _bin.position;
                return Vector2.Distance(new Vector2(door.x, door.z), new Vector2(near.x, near.z));
            }
        }

        /// <summary>
        /// The guest's score with their surroundings applied, and the open kitchen's extra mess made.
        /// Called as the plate reaches them, after dirt.
        /// </summary>
        public DishScore Apply(DishScore score, Customer customer)
        {
            var service = _services.Service;
            var pieces = OutOfPlace?.Invoke() ?? Nothing;
            var (appliances, tables) = Surroundings.Count(pieces);
            var own = Customer.Rate(score.OverallBeforeSurroundings, customer.Order);
            var effect = Surroundings.For(
                BinDistance, appliances, tables,
                Surroundings.NightStars(service.Customers, own),
                customer.Patience, service.Settings.Reviews.LowPatience,
                _services.Tuning.Surroundings);
            _services.Mess.OpenKitchenGuest(pieces);
            return score.WithSurroundings(effect);
        }
    }
}
