using System.Collections.Generic;
using Game.Simulation.Items;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A knife rack on a counter (decision 2026-11-07): several knives where one lying loose would
    /// take a whole counter. A takes one into the chef's tool slot (even while carrying food) and puts
    /// one back. Food can't go on it. It starts empty; knives are bought.
    /// </summary>
    public sealed class KnifeRackStation : CounterTopStation, IToolStation, IStationPrompt
    {
        [Tooltip("Where each knife hangs, one per place; their number is how many it holds.")]
        [SerializeField] private Transform[] _places = System.Array.Empty<Transform>();

        private Knife[] _held;
        private KitchenServices _services;

        // The rack takes no part in the Interact rules; A is handled as a tool exchange.
        public override IItemContainer Container => null;

        public override Item HeldItem => null;

        public override bool IsClear => KnifeCount == 0;

        public int Capacity => _places.Length;

        public int KnifeCount
        {
            get
            {
                var count = 0;
                if (_held != null)
                    foreach (var knife in _held)
                        if (knife != null)
                            count++;
                return count;
            }
        }

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        public override void Bind(KitchenServices services)
        {
            base.Bind(services);
            _services = services;
            _held ??= new Knife[_places.Length];
        }

        public override Transform AnchorFor(Item item)
        {
            for (var i = 0; i < _held.Length; i++)
            {
                if (_held[i] == item)
                    return _places[i];
            }
            return ItemAnchor;
        }

        /// <summary>Hangs a knife here, if there's a free place.</summary>
        public bool TryHang(Knife knife, Vector3 from)
        {
            for (var i = 0; i < _held.Length; i++)
            {
                if (_held[i] != null)
                    continue;
                _held[i] = knife;
                _services.ItemViews.Place(knife, _places[i], _services.Tuning.ItemMove, from);
                return true;
            }
            return false;
        }

        public bool TryExchangeTool(PlayerInteractor player)
        {
            if (player.Tool == null)
            {
                for (var i = 0; i < _held.Length; i++)
                {
                    if (_held[i] == null)
                        continue;
                    player.ReceiveTool(_held[i], _places[i].position);
                    _held[i] = null;
                    return true;
                }
                return false;
            }

            return player.Tool is Knife && KnifeCount < Capacity && TryHang((Knife)player.ReleaseTool(), player.transform.position);
        }

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            if (player.Tool == null)
                lines.Add(new PromptLine(PromptControl.South, "take knife", KnifeCount > 0));
            else if (player.Tool is Knife)
                lines.Add(new PromptLine(PromptControl.South, "hang knife", KnifeCount < Capacity));
        }
    }

    /// <summary>A station that hands tools to, and takes them back from, the chef's tool slot.</summary>
    public interface IToolStation
    {
        bool TryExchangeTool(PlayerInteractor player);
    }
}
