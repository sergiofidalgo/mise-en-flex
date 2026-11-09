using Game.Data;
using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using UnityEngine;
using Game.Gameplay.Saving;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A charcoal grill (decisions 2026-11-07): two floor slots, a hot zone and a cool zone, always
    /// lit. A chef acts on the zone they face, from either long side: A puts food on the grate (two
    /// pieces a zone) or a pan on a whole zone, and takes the hotter piece off; X flips the hotter
    /// piece over onto the other zone, so they step across to follow it.
    /// </summary>
    public sealed class GrillStation : Interactable, IUsable, IStationPrompt, IPlayerFocused, ISavedContents
    {
        [Tooltip("Where a pan stands on the hot zone, and on the cool zone.")]
        [SerializeField] private Transform _hotCentre;
        [SerializeField] private Transform _coolCentre;
        [Tooltip("Where food lies on each zone's grate, one per piece.")]
        [SerializeField] private Transform[] _hotPlaces = System.Array.Empty<Transform>();
        [SerializeField] private Transform[] _coolPlaces = System.Array.Empty<Transform>();
        [Tooltip("Metres ahead of the chef that decide which zone they're facing.")]
        [SerializeField, Min(0f)] private float _reach = 0.6f;

        private Grill _grill;
        private KitchenServices _services;
        private GrillZone _focus;
        private ZoneContainer _container;

        public Grill Grill => _grill;

        public override IItemContainer Container => _container;

        public override Item HeldItem => _focus?.Current;

        public override bool IsClear => _grill != null && _grill.Hot.IsEmpty && _grill.Cool.IsEmpty;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            var equipment = services.Equipment;
            _grill = new Grill(services.Cooking, equipment.GrillGrate(true), equipment.GrillGrate(false), equipment.GrillZone(true), equipment.GrillZone(false));
            _focus = _grill.Hot;
            _container = new ZoneContainer(this);
        }

        public override void Retire(KitchenServices services) => _grill.Remove(services.Cooking);

        // Slot 0 is the hot zone, 1 the cool one.
        public IEnumerable<(int Slot, Item Item)> SavedItems
        {
            get
            {
                if (_grill == null)
                    yield break;
                var zones = new[] { _grill.Hot, _grill.Cool };
                for (var i = 0; i < zones.Length; i++)
                {
                    if (zones[i].StandingPan != null)
                        yield return (i, zones[i].StandingPan);
                    foreach (var piece in zones[i].Grate.Contents)
                        yield return (i, piece);
                }
            }
        }

        public void ClearForLoad()
        {
        }

        public bool RestoreItem(int slot, Item item)
        {
            var zone = slot == 1 ? _grill.Cool : _grill.Hot;
            if (!zone.CanAccept(item))
                return false;
            zone.Put(item);
            RefreshViews(transform.position);
            return true;
        }

        /// <summary>The zone in front of this chef: whichever side of the grill's middle they're facing.</summary>
        public GrillZone ZoneFacing(PlayerInteractor player)
        {
            var point = player.transform.position + player.transform.forward * _reach;
            return transform.InverseTransformPoint(point).x < 0f ? _grill.Hot : _grill.Cool;
        }

        public void FocusOn(PlayerInteractor player)
        {
            if (_grill != null)
                _focus = ZoneFacing(player);
        }

        public bool TryUse(PlayerInteractor player)
        {
            FocusOn(player);
            if (_grill == null || !_grill.TryFlip(_focus))
                return false;
            RefreshViews(transform.position);
            return true;
        }

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            if (_grill == null)
                return;
            var zone = ZoneFacing(player);
            var side = zone.IsHot ? Tr.Get("hot side") : Tr.Get("cool side");
            var otherSide = zone.IsHot ? Tr.Get("cool side") : Tr.Get("hot side");

            if (zone.StandingPan != null)
                lines.Add(new PromptLine(PromptControl.West, Tr.Get("flip"), zone.StandingPan.Contents.Count > 0));
            else if (zone.Hottest != null)
                lines.Add(new PromptLine(PromptControl.West, Tr.Format("flip onto {side}", ("side", otherSide)), _grill.Other(zone).HasRoomOnGrate));

            var held = player.Hands.Current;
            if (held == null && zone.Current != null)
                lines.Add(new PromptLine(PromptControl.South, Tr.Format("take {what}", ("what", Tr.Get(zone.Current.TypeId)))));
            else if (held is Pan)
                lines.Add(new PromptLine(PromptControl.South, Tr.Format("put pan on {side}", ("side", side)), zone.CanAccept(held)));
            else if (held is Ingredient)
                lines.Add(new PromptLine(PromptControl.South, Tr.Format("put on {side}", ("side", side)), zone.CanAccept(held)));
        }

        public override Transform AnchorFor(Item item)
        {
            foreach (var zone in new[] { _grill.Hot, _grill.Cool })
            {
                if (zone.StandingPan == item)
                    return zone.IsHot ? _hotCentre : _coolCentre;
                var places = zone.IsHot ? _hotPlaces : _coolPlaces;
                var index = IndexOf(zone.Grate.Contents, item);
                if (index >= 0 && index < places.Length)
                    return places[index];
            }
            return ItemAnchor;
        }

        private static int IndexOf(IReadOnlyList<Ingredient> list, Item item)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == item)
                    return i;
            }
            return -1;
        }

        // Everything on the grill to its place: food shifts along when a piece leaves or arrives.
        private void RefreshViews(Vector3 spawnAt)
        {
            var views = _services.ItemViews;
            var move = _services.Tuning.ItemMove;
            foreach (var zone in new[] { _grill.Hot, _grill.Cool })
            {
                if (zone.StandingPan != null)
                    views.Place(zone.StandingPan, AnchorFor(zone.StandingPan), move, spawnAt);
                foreach (var piece in zone.Grate.Contents)
                    views.Place(piece, AnchorFor(piece), move, spawnAt);
            }
        }

        // Interact as the focused zone sees it; views follow every change.
        private sealed class ZoneContainer : IItemContainer, IItemHolder
        {
            private readonly GrillStation _station;

            public ZoneContainer(GrillStation station) => _station = station;

            public Item Current => _station._focus.Current;

            public bool CanTake => _station._focus.CanTake;

            public bool CanAccept(Item item) => _station._focus.CanAccept(item);

            public Item Take()
            {
                var item = _station._focus.Take();
                _station.RefreshViews(_station.transform.position);
                return item;
            }

            public void Put(Item item)
            {
                _station._focus.Put(item);
                _station.RefreshViews(_station.transform.position);
            }
        }
    }

    /// <summary>A station that works differently depending on where the chef stands at it.</summary>
    public interface IPlayerFocused
    {
        /// <summary>Called just before this player's Interact or Use acts on it.</summary>
        void FocusOn(PlayerInteractor player);
    }
}
