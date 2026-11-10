using System.Collections.Generic;
using System.Linq;
using Game.Gameplay.Stations;
using Game.Simulation.Cleaning;
using Game.Simulation.Cooking;
using UnityEngine;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Gameplay.Cleaning
{
    /// <summary>
    /// Connects the kitchen to its dirt and fires (Phase 5, decision 2026-11-10): says where in the
    /// room things happened, so <see cref="MessMaker"/> can leave dirt there, and takes food lost to
    /// a fire out of the kitchen. Plain C#, made by the composition root.
    /// </summary>
    public sealed class KitchenMess
    {
        // How far from a hob a counter can be and still count as beside it, metres between centres.
        private const float BesideReach = 1.15f;

        // How far onto the counter the soot reaches from the side nearest the hob.
        private const float SootInset = 0.3f;

        private readonly KitchenServices _services;

        public KitchenMess(KitchenServices services)
        {
            _services = services;
            services.Fires.FoodBurnt += HandleFoodBurnt;
            services.Fires.Ignited += HandleIgnited;
            services.Fires.PutOut += HandlePutOut;
        }

        /// <summary>Where a hob is: its own station, or the grill whose zone it is. Null if it's gone.</summary>
        public Interactable StationOf(Hob hob)
        {
            foreach (var station in _services.Stations.All)
            {
                if (station is HobStation hobStation && hobStation.Hob == hob)
                    return station;
                if (station is GrillStation grill && grill.Grill != null && (grill.Grill.Hot.Source == hob || grill.Grill.Cool.Source == hob))
                    return station;
            }
            return null;
        }

        /// <summary>A spot on a table or counter, in its own axes, from a point in the room.</summary>
        public static (float X, float Z) OnStation(Interactable station, Vector3 world)
        {
            var local = station.transform.InverseTransformPoint(world);
            return (local.x, local.z);
        }

        /// <summary>Where a spot is in the room: on its station's top, or on the floor.</summary>
        public static Vector3 WorldOf(DirtSpot spot, float floorHeight = 0f)
        {
            if (spot.Host is Interactable station && station != null)
            {
                var top = station.ItemAnchor != null ? station.ItemAnchor.position.y : station.transform.position.y + 0.9f;
                var world = station.transform.TransformPoint(new Vector3(spot.X, 0f, spot.Z));
                return new Vector3(world.x, top, world.z);
            }
            return new Vector3(spot.X, floorHeight, spot.Z);
        }

        /// <summary>A guest finished eating at <paramref name="station"/>, their plate at <paramref name="plate"/>.</summary>
        public void DinerFinished(Interactable station, Vector3 plate)
        {
            var (x, z) = OnStation(station, plate);
            _services.Mess.DinerFinished(DirtSurface.Table, station, x, z);
        }

        /// <summary>A plate was dropped or broken here.</summary>
        public void PlateBroken(Vector3 where) => _services.Mess.PlateBroken(where.x, where.z);

        /// <summary>A guest walked through the room here.</summary>
        public void GuestWalked(Vector3 where) => _services.Mess.GuestWalked(where.x, where.z);

        /// <summary>A station was sold: its dirt goes with it.</summary>
        public void Sold(Interactable station)
        {
            _services.Dirt.RemoveOn(station);
            if (station is HobStation hob && hob.Hob != null)
                _services.Fires.Forget(hob.Hob);
        }

        // Soot from food burnt on the heat: on the counter beside the hob, else the floor in front.
        private void HandleFoodBurnt(Hob hob, Ingredient food)
        {
            var station = StationOf(hob);
            if (station == null)
                return;
            var at = station.transform.position;
            var counter = CounterBeside(station);
            if (counter != null)
            {
                var toward = at - counter.transform.position;
                toward.y = 0f;
                var soot = counter.transform.position + (toward.sqrMagnitude > 1e-4f ? toward.normalized * SootInset : Vector3.zero);
                var (x, z) = OnStation(counter, soot);
                _services.Mess.PanBurnt(counter, x, z);
                return;
            }
            var front = InFront(station);
            _services.Mess.PanBurnt(null, front.x, front.z);
        }

        // The food is lost: out of the kitchen, view and all.
        private void HandleIgnited(Hob hob, IReadOnlyList<Ingredient> food)
        {
            foreach (var ingredient in food)
            {
                if (_services.Cooking.Contains(ingredient))
                    _services.Cooking.Remove(ingredient);
                _services.ItemViews.Release(ingredient);
            }
        }

        private void HandlePutOut(Hob hob)
        {
            var station = StationOf(hob);
            if (station == null)
                return;
            var front = InFront(station);
            _services.Mess.FireOut(front.x, front.z);
        }

        private CounterStation CounterBeside(Interactable station)
        {
            var at = station.transform.position;
            return _services.Stations.All.OfType<CounterStation>()
                .Where(c => c != station && Flat(c.transform.position - at) <= BesideReach)
                .OrderBy(c => Flat(c.transform.position - at))
                .FirstOrDefault();
        }

        /// <summary>The floor in front of a station: the nearest place someone could stand.</summary>
        public Vector3 InFront(Interactable station)
        {
            var at = station.transform.position;
            var free = _services.NewLayout().NearestFree(new SimVector2(at.x, at.z));
            return free.HasValue ? new Vector3(free.Value.X, 0f, free.Value.Y) : at + station.transform.forward * 0.8f;
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
