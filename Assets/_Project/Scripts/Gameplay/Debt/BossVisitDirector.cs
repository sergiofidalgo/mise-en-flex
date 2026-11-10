using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Gameplay.Stations;
using Game.Simulation.Campaign;
using Game.Simulation.Days;
using UnityEngine;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Gameplay.Debt
{
    /// <summary>
    /// The morning after a missed collection, the boss comes in person (§15.3; "comic menace", the
    /// user's call, 2026-11-10): he and his two bodyguards walk in from the street door, he says his
    /// piece, and depending on the step:
    /// <list type="bullet">
    /// <item>a warning: nothing else;</item>
    /// <item>a repossession: the guards carry out appliances, most valuable first, until they cover
    /// the debt, each credited at resale value;</item>
    /// <item>with one restaurant, step 3 strips it to the bare minimum;</item>
    /// <item>with more, step 3 takes the restaurant that missed the collection;</item>
    /// <item>erasure ends the campaign.</item>
    /// </list>
    /// Everyone walks on the furniture's free floor, so nobody passes through a counter, and a piece
    /// being carried is packed down to a crate the guard holds close, so it never sweeps through
    /// anything either. Nobody readies up or rearranges the room while he's in.
    /// </summary>
    public sealed class BossVisitDirector : MonoBehaviour
    {
        // Within this distance a visitor has reached a point on the way.
        private const float ArriveDistance = 0.08f;

        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private LayoutEditor _layoutEditor;
        [Tooltip("Where visitors come in from the street (the customers' door).")]
        [SerializeField] private Transform _door;
        [SerializeField] private VisitorAgent _bossPrefab;
        [SerializeField] private VisitorAgent _guardPrefab;
        [Tooltip("Metres per second: the boss takes his time.")]
        [SerializeField, Min(0.1f)] private float _bossSpeed = 1.5f;
        [SerializeField, Min(0.1f)] private float _guardSpeed = 2.4f;
        [Tooltip("How far into the room the boss stands, from the door (m).")]
        [SerializeField, Min(0.5f)] private float _standIn = 2.4f;
        [Tooltip("How far either side of him, and behind, his guards stand (m).")]
        [SerializeField] private Vector2 _guardOffset = new Vector2(0.95f, 0.45f);
        [Tooltip("Real seconds each spoken line stays up.")]
        [SerializeField, Min(0.5f)] private float _secondsPerLine = 2.6f;
        [Tooltip("Seconds a guard takes to pack a piece into a crate.")]
        [SerializeField, Min(0.05f)] private float _packSeconds = 0.5f;
        [Tooltip("The size a packed piece shrinks to (m), small enough to carry close without clipping.")]
        [SerializeField, Min(0.1f)] private float _crateSize = 0.42f;
        [Tooltip("Where a guard holds the crate (local to him, m).")]
        [SerializeField] private Vector3 _carryAt = new Vector3(0f, 1.05f, 0.34f);
        [Tooltip("Metres past the door they walk before they're gone.")]
        [SerializeField, Min(0.2f)] private float _outside = 1.4f;
        [Tooltip("Give up on a walk after this many seconds (someone is in the way for good).")]
        [SerializeField, Min(1f)] private float _walkTimeout = 12f;

        private readonly List<VisitorAgent> _visitors = new List<VisitorAgent>();
        private Game.Simulation.Movement.StationLayout _layout;

        /// <summary>True from the moment the boss walks in until the last of them has left.</summary>
        public bool Busy { get; private set; }

        /// <summary>What this visit is for; None between visits.</summary>
        public BossVisit Visit { get; private set; }

        /// <summary>The boss in the room, or null.</summary>
        public VisitorAgent BossAgent { get; private set; }

        /// <summary>What he's saying right now; empty when quiet. Shown in a bubble over him.</summary>
        public string Saying { get; private set; } = "";

        /// <summary>Raised as a guard packs a piece up, with the piece: for feedback.</summary>
        public event Action<Interactable> Packed;

        /// <summary>Raised as a piece is carried out of the door, with what it knocked off the debt.</summary>
        public event Action<float> CarriedOut;

        /// <summary>The boss has taken the restaurant the players are in: they go back to the office.</summary>
        public event Action LeftForOffice;

        /// <summary>The campaign has been erased: back to the start.</summary>
        public event Action Erased;

        private void Start()
        {
            _root.Services.Days.PhaseChanged += HandlePhaseChanged;
            if (_root.Services.Days.Phase == DayPhase.Plan)
                BeginMorning();
        }

        private void OnDestroy()
        {
            if (_root != null && _root.Services != null)
                _root.Services.Days.PhaseChanged -= HandlePhaseChanged;
            if (_layoutEditor != null)
                _layoutEditor.Closed = false;
        }

        private void HandlePhaseChanged(DayPhase phase)
        {
            if (phase == DayPhase.Plan)
                BeginMorning();
        }

        // Anything he does in person happens the next morning (§15.3); with nothing to do, he may
        // still ring if today's collection isn't covered yet.
        private void BeginMorning()
        {
            var pending = _root.Services.Boss.Pending;
            if (pending != BossVisit.None && _bossPrefab != null && _guardPrefab != null && _door != null)
                StartCoroutine(Run(pending));
            else
                RingIfNear();
        }

        private void RingIfNear()
        {
            var days = _root.Services.Days;
            if (BossCalls.ShouldWarnNear(days.DaysUntilFee, days.NextCollection))
                _root.Phone.Ring(BossCallKind.Near, BossCalls.Lines(BossCallKind.Near));
        }

        private IEnumerator Run(BossVisit visit)
        {
            Busy = true;
            Visit = visit;
            if (_layoutEditor != null)
                _layoutEditor.Closed = true;
            var services = _root.Services;
            _layout = services.NewLayout();

            // In they come, the boss first and a guard either side of him.
            var inward = Flat(_door.forward);
            var side = Vector3.Cross(Vector3.up, inward);
            var stand = Free(_door.position + inward * _standIn);
            BossAgent = Spawn(_bossPrefab, "Boss");
            var left = Spawn(_guardPrefab, "Bodyguard_L");
            var right = Spawn(_guardPrefab, "Bodyguard_R");
            var leftSpot = Free(stand - side * _guardOffset.x - inward * _guardOffset.y);
            var rightSpot = Free(stand + side * _guardOffset.x - inward * _guardOffset.y);
            yield return All(Walk(BossAgent, stand, _bossSpeed), Walk(left, leftSpot, _guardSpeed, 0.4f), Walk(right, rightSpot, _guardSpeed, 0.8f));
            yield return FaceAll(KitchenCentre());

            yield return Say(BossCalls.VisitLines(visit));

            switch (visit)
            {
                case BossVisit.Repossession:
                {
                    var taken = Repossession.ForShortfall(Appliances(), services.Days.OwedToBoss);
                    if (taken.Count == 0)
                        yield return Say(new[] { BossCalls.Asides[0], BossCalls.Asides[1] });
                    yield return CarryOut(taken, left, right, leftSpot, rightSpot);
                    break;
                }
                case BossVisit.StripToMinimum:
                    yield return CarryOut(Repossession.StripToMinimum(Appliances()), left, right, leftSpot, rightSpot);
                    yield return Say(new[] { BossCalls.Asides[2] });
                    break;
                case BossVisit.TakeRestaurant:
                    if (_root.LoseRestaurant(services.Boss.PendingAt))
                    {
                        yield return Leave();
                        Finish(stayBusy: true); // the room is his now: nobody readies up on the way out
                        LeftForOffice?.Invoke();
                        yield break;
                    }
                    yield return Say(new[] { BossCalls.Asides[3] });
                    break;
                case BossVisit.Erasure:
                    _root.EraseCampaign();
                    Saying = "";
                    yield return new WaitForSeconds(0.6f);
                    Finish(stayBusy: true);
                    Erased?.Invoke();
                    yield break;
            }

            services.Boss.Visited();
            yield return Leave();
            Finish();
            RingIfNear();
        }

        private void Finish(bool stayBusy = false)
        {
            Saying = "";
            Visit = BossVisit.None;
            BossAgent = null;
            foreach (var visitor in _visitors)
                if (visitor != null)
                    Destroy(visitor.gameObject);
            _visitors.Clear();
            if (stayBusy)
                return;
            if (_layoutEditor != null)
                _layoutEditor.Closed = false;
            Busy = false;
        }

        // ---- Repossession ----

        // Every clear piece in the room, as the bodyguards see it. Pieces with food, pans or plates on
        // them stay: they'd only make a mess of the suits (and of the save).
        private List<Appliance> Appliances()
        {
            var stations = _root.Services.Stations.All;
            var room = new List<Appliance>();
            for (var i = 0; i < stations.Count; i++)
            {
                var station = stations[i];
                if (station is StallStation || station is StreetDoor)
                    continue;
                // A piece built into another (a board in a counter) goes only with it, so neither is
                // taken: carrying one out would leave the other working in thin air.
                var builtIn = station.GetComponentsInChildren<Interactable>().Length > 1
                    || (station.transform.parent != null && station.transform.parent.GetComponentInParent<Interactable>() != null);
                var forSale = !string.IsNullOrEmpty(station.FurnitureId) && station.IsClear && !builtIn;
                var price = _layoutEditor != null ? _layoutEditor.SellPriceOf(station) : 0f;
                room.Add(new Appliance(i, RoleOf(station), price, forSale && price > 0f));
            }
            return room;
        }

        private static ApplianceRole RoleOf(Interactable station)
        {
            switch (station)
            {
                case HobStation _:
                case GrillStation _:
                    return ApplianceRole.Hob;
                case PrepStation _:
                    return ApplianceRole.Board;
                case StorageStation _:
                    return ApplianceRole.Storage;
                case TableStation _:
                    return ApplianceRole.Table;
                case ServingStation _:
                    return ApplianceRole.Pass;
                default:
                    return ApplianceRole.Other;
            }
        }

        // One piece at a time, the guards taking turns, so it reads as a procession out of the door.
        private IEnumerator CarryOut(IReadOnlyList<Appliance> taken, VisitorAgent left, VisitorAgent right, Vector3 leftSpot, Vector3 rightSpot)
        {
            var stations = _root.Services.Stations.All.ToList();
            var pieces = taken.Select(a => (Station: stations[a.Index], a.Resale)).ToList();
            for (var i = 0; i < pieces.Count; i++)
            {
                var (station, resale) = pieces[i];
                if (station == null)
                    continue;
                var guard = i % 2 == 0 ? left : right;
                var home = i % 2 == 0 ? leftSpot : rightSpot;
                yield return WalkTo(guard, station);
                guard.Face(Flat(station.transform.position - guard.transform.position));
                yield return Pack(guard, station);
                _layout = _root.Services.NewLayout(); // the floor it stood on is free now
                yield return Walk(guard, Free(_door.position + Flat(_door.forward) * 0.6f), _guardSpeed);
                yield return WalkStraight(guard, _door.position - Flat(_door.forward) * _outside, _guardSpeed);
                Destroy(station.gameObject);
                _root.Services.Days.CreditRepossessed(resale);
                CarriedOut?.Invoke(resale);
                yield return WalkStraight(guard, _door.position + Flat(_door.forward) * 0.6f, _guardSpeed);
                yield return Walk(guard, home, _guardSpeed);
            }
            if (pieces.Count > 0)
                yield return FaceAll(KitchenCentre());
        }

        // The piece lifts, shrinks into the guard's arms as a crate and goes with him. It's out of
        // the kitchen (no one can use or move it) from the moment he touches it.
        private IEnumerator Pack(VisitorAgent guard, Interactable station)
        {
            if (_layoutEditor != null)
                _layoutEditor.Repossess(station);
            foreach (var collider in station.GetComponentsInChildren<Collider>())
                collider.enabled = false;
            Packed?.Invoke(station);

            var bounds = Bounds(station.gameObject);
            var largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.01f);
            var fromScale = station.transform.localScale;
            var toScale = fromScale * Mathf.Min(1f, _crateSize / largest);
            var fromPosition = station.transform.position;
            var fromRotation = station.transform.rotation;
            // Where the crate's centre ends up, with the piece's pivot (at its base) below it.
            var pivotBelowCentre = (bounds.center - fromPosition) * (toScale.x / Mathf.Max(fromScale.x, 1e-4f));
            for (var t = 0f; t < 1f; t += Time.deltaTime / _packSeconds)
            {
                var eased = t * t * (3f - 2f * t);
                var hands = guard.transform.TransformPoint(_carryAt) - pivotBelowCentre;
                station.transform.localScale = Vector3.Lerp(fromScale, toScale, eased);
                station.transform.position = Vector3.Lerp(fromPosition, hands, eased) + Vector3.up * Mathf.Sin(Mathf.PI * eased) * 0.3f;
                station.transform.rotation = Quaternion.Slerp(fromRotation, guard.transform.rotation, eased);
                yield return null;
            }
            station.transform.SetParent(guard.transform, true);
            station.transform.localScale = toScale;
            station.transform.position = guard.transform.TransformPoint(_carryAt) - pivotBelowCentre;
            station.transform.rotation = guard.transform.rotation;
        }

        private static Bounds Bounds(GameObject piece)
        {
            var renderers = piece.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(piece.transform.position, Vector3.one);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        // ---- Walking ----

        private VisitorAgent Spawn(VisitorAgent prefab, string name)
        {
            var agent = Instantiate(prefab, _door.position, Quaternion.LookRotation(Flat(_door.forward)));
            agent.name = name;
            agent.Slide = _layout.Slide;
            _visitors.Add(agent);
            return agent;
        }

        private IEnumerator Leave()
        {
            Saying = "";
            var outside = _door.position - Flat(_door.forward) * _outside;
            var doorstep = Free(_door.position + Flat(_door.forward) * 0.5f);
            var walks = new List<IEnumerator>();
            for (var i = 0; i < _visitors.Count; i++)
            {
                var visitor = _visitors[i];
                var speed = visitor == BossAgent ? _bossSpeed : _guardSpeed;
                walks.Add(Then(Walk(visitor, doorstep, speed, i * 0.35f), WalkStraight(visitor, outside, speed)));
            }
            yield return All(walks.ToArray());
        }

        // Along the free floor to a point, after an optional delay.
        private IEnumerator Walk(VisitorAgent agent, Vector3 target, float speed, float delay = 0f)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            var path = _layout.FindPath(Floor(agent.transform.position), Floor(target));
            if (path == null)
            {
                yield return WalkStraight(agent, target, speed);
                yield break;
            }
            foreach (var point in path)
                yield return WalkStraight(agent, new Vector3(point.X, agent.transform.position.y, point.Y), speed);
        }

        // To a free side of a station, to pick it up.
        private IEnumerator WalkTo(VisitorAgent agent, Interactable station)
        {
            var path = _layout.FindPathTo(Floor(agent.transform.position), station.Footprint);
            if (path == null)
            {
                var spot = _layout.StandingSpot(station.Footprint);
                if (spot.HasValue)
                    yield return Walk(agent, new Vector3(spot.Value.X, 0f, spot.Value.Y), _guardSpeed);
                yield break;
            }
            foreach (var point in path)
                yield return WalkStraight(agent, new Vector3(point.X, agent.transform.position.y, point.Y), _guardSpeed);
        }

        private IEnumerator WalkStraight(VisitorAgent agent, Vector3 target, float speed)
        {
            var started = Time.time;
            while (agent != null && !agent.WalkTowards(target, speed * Time.deltaTime, ArriveDistance))
            {
                if (Time.time - started > _walkTimeout)
                    yield break;
                yield return null;
            }
        }

        private IEnumerator FaceAll(Vector3 point)
        {
            for (var t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                foreach (var visitor in _visitors)
                    if (visitor != null)
                        visitor.Face(Flat(point - visitor.transform.position));
                yield return null;
            }
        }

        private IEnumerator Say(IReadOnlyList<string> lines)
        {
            foreach (var line in lines)
            {
                Saying = line;
                yield return new WaitForSeconds(_secondsPerLine);
            }
            Saying = "";
        }

        // Runs several walks side by side until they've all finished.
        private IEnumerator All(params IEnumerator[] walks)
        {
            var running = walks.Select(w => StartCoroutine(w)).ToList();
            foreach (var walk in running)
                yield return walk;
        }

        private static IEnumerator Then(IEnumerator first, IEnumerator second)
        {
            yield return first;
            yield return second;
        }

        private Vector3 KitchenCentre()
        {
            var plan = _root.Services.FloorPlan;
            var middle = (plan.KitchenAreaMin + plan.KitchenAreaMax) * 0.5f;
            return new Vector3(middle.X, 0f, middle.Y);
        }

        private Vector3 Free(Vector3 point)
        {
            var free = _layout.NearestFree(Floor(point));
            return free.HasValue ? new Vector3(free.Value.X, point.y, free.Value.Y) : point;
        }

        private static SimVector2 Floor(Vector3 world) => new SimVector2(world.x, world.z);

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
        }
    }
}
