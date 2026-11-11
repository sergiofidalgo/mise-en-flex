using System;
using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Stations;
using Game.Simulation.Cleaning;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Cleaning
{
    /// <summary>What the chef pulls out to clean (game design §15.8, decision 2026-11-07).</summary>
    public enum CleaningTool
    {
        None,

        /// <summary>Tables and counters: spin the stick round.</summary>
        Cloth,

        /// <summary>The floor: spin the stick round, slower and heavier.</summary>
        Mop,

        /// <summary>A pan fire: wag the stick from side to side.</summary>
        Extinguisher,
    }

    /// <summary>
    /// A chef cleaning or putting out a fire (decision 2026-11-10): with empty hands, hold X by dirt
    /// or a burning hob and the chef pulls out a cloth, a mop or the extinguisher, stays put, and the
    /// stick spins (cloth, mop) or sweeps side to side (extinguisher) to clean (decision 2026-11-11).
    /// Letting go of X, or B, stops; what was wiped stays wiped. While X is still held the chef stays
    /// planted (PlayerInteractor.IsPlanted): the stick turns them on the spot and the next job in
    /// reach starts by itself, as at the sink. The extinguisher is always to hand, with nothing to buy.
    ///
    /// X does the station's own thing first (flip, ring the bell); cleaning is what it does when the
    /// station has nothing to do.
    /// </summary>
    public sealed class ChefCleaning : MonoBehaviour
    {
        private const string UseActionName = "Use";

        // Where the mop reaches from: a step in front of the chef's feet.
        private const float MopAhead = 0.45f;

        // How far, and how nearly in front (cosine of the angle), a burning hob can be put out from.
        private const float FireReach = 1.6f;
        private const float FireFacing = 0.3f;

        private PlayerInteractor _chef;
        private KitchenServices _services;
        private InputAction _use;
        private int _starts;

        /// <summary>The game being played, or null when not cleaning.</summary>
        public TraceGame Game { get; private set; }

        public CleaningTool Tool { get; private set; }

        /// <summary>The dirt being wiped, or null (putting out a fire).</summary>
        public DirtSpot Spot { get; private set; }

        /// <summary>The burning hob being put out, or null.</summary>
        public HobStation Fire { get; private set; }

        /// <summary>The chef doing it.</summary>
        public PlayerInteractor Chef => _chef;

        public bool IsCleaning => Game != null;

        /// <summary>Where the path lies in the room: on the dirt, or over the pan on fire.</summary>
        public Vector3 Where
        {
            get
            {
                if (Fire != null)
                    return Fire.ItemAnchor != null ? Fire.ItemAnchor.position : Fire.transform.position;
                return Spot != null ? KitchenMess.WorldOf(Spot) : transform.position;
            }
        }

        /// <summary>0..1: how far through the job: the spot wiped, or the fire put out.</summary>
        public float Progress
        {
            get
            {
                if (Fire != null)
                    return _services.Fires.PutOutProgress(Fire.Hob);
                return Spot != null ? 1f - Spot.Remaining : 0f;
            }
        }

        /// <summary>A patch came clean or a fire went out, with where it was.</summary>
        public event Action<ChefCleaning, Vector3> Finished;

        /// <summary>Every chef cleaning right now, for the views.</summary>
        public static IReadOnlyList<ChefCleaning> All => Active;

        private static readonly List<ChefCleaning> Active = new List<ChefCleaning>();

        public void Bind(PlayerInteractor chef, KitchenServices services)
        {
            _chef = chef;
            _services = services;
        }

        private void OnEnable() => Active.Add(this);

        private void OnDisable()
        {
            Stop();
            Active.Remove(this);
        }

        /// <summary>
        /// Adds "X wipe" or "X mop" to a station's prompt when X would clean here: when the station's
        /// own X has nothing to do (a greyed-out line gives way to it).
        /// </summary>
        public void DescribeActions(Interactable target, List<PromptLine> lines)
        {
            if (IsCleaning)
                return;
            foreach (var line in lines)
            {
                if (line.Control == PromptControl.West && line.Available)
                    return;
            }
            var tool = Available(target);
            // A fire's prompt is the hob's own.
            if (tool == CleaningTool.None || tool == CleaningTool.Extinguisher)
                return;
            lines.RemoveAll(line => line.Control == PromptControl.West);
            lines.Add(new PromptLine(PromptControl.West, Verb(tool), _chef.Hands.IsEmpty));
        }

        /// <summary>What X would start here, for the prompt.</summary>
        public CleaningTool Available(Interactable target) => Find(target, out _, out _);

        /// <summary>Starts cleaning or putting out a fire, if there's any to do.</summary>
        public bool TryStart(Interactable target)
        {
            if (Game != null || _services == null || !_chef.Hands.IsEmpty || !_chef.CanAct)
                return false;
            var tool = Find(target, out var spot, out var fire);
            if (tool == CleaningTool.None)
                return false;
            // Looked up now, not at Bind: by the first press, PlayerInput has this player's own copy.
            if (TryGetComponent(out PlayerInput input))
                _use = input.actions[UseActionName];
            Begin(tool, spot, fire);
            return true;
        }

        private void Begin(CleaningTool tool, DirtSpot spot, HobStation fire)
        {
            Tool = tool;
            Spot = spot;
            Fire = fire;
            // Each job starts somewhere new round its shape, so no two look alike.
            var start = (_starts++ * 0.37f) % 1f;
            // The minigame complexity dial bends the trace too (decision 2026-11-10).
            var realism = _services.Realism;
            Game = new TraceGame(ShapeOf(tool), global::Game.Simulation.Realism.MinigameComplexity.Scale(_services.Tuning.TraceGame, realism.MinigameComplexity, realism.Complexity), start);
            _chef.IsBusy = true;
            _chef.BackOut = Stop;

            var toward = Where - transform.position;
            toward.y = 0f;
            if (toward.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.LookRotation(toward, Vector3.up);
        }

        public void Stop()
        {
            if (Game == null)
                return;
            Game = null;
            Tool = CleaningTool.None;
            Spot = null;
            Fire = null;
            if (_chef != null)
            {
                _chef.IsBusy = false;
                _chef.BackOut = null;
            }
        }

        private void Update()
        {
            if (Game == null)
                return;

            // Hold X: letting go stops. So does the day ending, or the fire going out another way.
            var held = _use != null && _use.IsPressed();
            var gone = Fire != null ? !_services.Fires.IsBurning(Fire.Hob) : Spot == null || !Contains(Spot);
            if (!held || !_chef.CanAct || gone)
            {
                Stop();
                return;
            }

            var stick = _chef.MoveInput;
            var work = Game.Tick(Time.deltaTime, new System.Numerics.Vector2(stick.x, stick.y));
            if (work <= 0f)
                return;

            var where = Where;
            var done = Fire != null ? _services.Fires.Spray(Fire.Hob, work) : _services.Dirt.Clean(Spot, work);
            if (!done)
                return;

            Finished?.Invoke(this, where);
            // Still holding X, the chef stays planted and the next job in reach starts by itself
            // (PlayerInteractor.IsPlanted), whatever its tool; turning finds more.
            Stop();
        }

        private bool Contains(DirtSpot spot)
        {
            foreach (var each in _services.Dirt.Spots)
            {
                if (each == spot)
                    return true;
            }
            return false;
        }

        // A fire first, then dirt on what the chef faces, then the floor at their feet.
        private CleaningTool Find(Interactable target, out DirtSpot spot, out HobStation fire)
        {
            spot = null;
            fire = null;
            if (_services == null)
                return CleaningTool.None;

            if (target is HobStation hob && _services.Fires.IsBurning(hob.Hob))
            {
                fire = hob;
                return CleaningTool.Extinguisher;
            }
            // A fire is forgiving to aim at: any burning hob within reach in front of the chef will
            // do, even with the counter beside it highlighted.
            fire = BurningInReach();
            if (fire != null)
                return CleaningTool.Extinguisher;

            if (target != null)
            {
                var best = float.MaxValue;
                foreach (var each in _services.Dirt.On(target))
                {
                    var distance = (KitchenMess.WorldOf(each) - transform.position).sqrMagnitude;
                    if (distance >= best)
                        continue;
                    best = distance;
                    spot = each;
                }
                if (spot != null)
                    return CleaningTool.Cloth;
            }

            var feet = transform.position + transform.forward * MopAhead;
            spot = _services.Dirt.NearestOnFloor(feet.x, feet.z, _services.Tuning.MopReach);
            return spot != null ? CleaningTool.Mop : CleaningTool.None;
        }

        private HobStation BurningInReach()
        {
            HobStation best = null;
            var bestDistance = FireReach;
            foreach (var station in _services.Stations.All)
            {
                if (!(station is HobStation hob) || !_services.Fires.IsBurning(hob.Hob))
                    continue;
                var toward = hob.transform.position - transform.position;
                toward.y = 0f;
                var distance = toward.magnitude;
                if (distance > bestDistance || (distance > 0.01f && Vector3.Dot(toward / distance, transform.forward) < FireFacing))
                    continue;
                best = hob;
                bestDistance = distance;
            }
            return best;
        }

        public static TraceShape ShapeOf(CleaningTool tool) => tool switch
        {
            CleaningTool.Mop => TraceShape.WideCircle,
            CleaningTool.Extinguisher => TraceShape.Sweep,
            _ => TraceShape.Circle,
        };

        /// <summary>The prompt's word for what X does with this tool.</summary>
        public static string Verb(CleaningTool tool) => tool switch
        {
            CleaningTool.Mop => Tr.Get("mop"),
            CleaningTool.Extinguisher => Tr.Get("put out fire"),
            _ => Tr.Get("wipe"),
        };
    }
}
