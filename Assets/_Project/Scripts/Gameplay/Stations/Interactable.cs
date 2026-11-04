using System;
using System.Collections.Generic;
using Game.Simulation.Items;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// Base for anything a player can target with Interact. Knows who is targeting it so
    /// presentation can highlight it, and where items it holds should sit.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        [Tooltip("Where items on this station sit (e.g. the counter top).")]
        [SerializeField] private Transform _itemAnchor;

        [Tooltip("Its entry in the furniture catalogue. Empty: it isn't for sale.")]
        [SerializeField] private string _furnitureId;

        [Tooltip("Half its width and depth on the floor, if it isn't the standard square. Zero: standard.")]
        [SerializeField] private Vector2 _halfSize;

        private readonly List<PlayerInteractor> _targeters = new List<PlayerInteractor>();
        private bool _cantGoHere;

        /// <summary>What Interact takes from and puts into. Null until bound.</summary>
        public abstract IItemContainer Container { get; }

        public Transform ItemAnchor => _itemAnchor;

        public string FurnitureId => _furnitureId;

        /// <summary>Half its width and depth, or zero if it's the standard square.</summary>
        public System.Numerics.Vector2 HalfSize => new System.Numerics.Vector2(_halfSize.x, _halfSize.y);

        /// <summary>What it takes up on the floor, where it stands now.</summary>
        public Simulation.Movement.Footprint Footprint => new Simulation.Movement.Footprint(
            new System.Numerics.Vector2(transform.position.x, transform.position.z), transform.eulerAngles.y, HalfSize);

        /// <summary>It goes in the dining room rather than the kitchen.</summary>
        public virtual bool BelongsInDiningRoom => false;

        /// <summary>True if nothing would be lost by selling it.</summary>
        public virtual bool IsClear => false;

        /// <summary>It's been sold: anything it added to the simulation goes with it.</summary>
        public virtual void Retire(KitchenServices services)
        {
        }

        public bool IsTargeted => _targeters.Count > 0;

        /// <summary>Player index of the most recent player targeting this, or -1.</summary>
        public int TargeterPlayerIndex => _targeters.Count > 0 ? _targeters[_targeters.Count - 1].PlayerIndex : -1;

        public event Action HighlightChanged;

        /// <summary>Being carried in management mode, somewhere it can't be put down.</summary>
        public bool CantGoHere
        {
            get => _cantGoHere;
            set
            {
                if (_cantGoHere == value)
                    return;
                _cantGoHere = value;
                HighlightChanged?.Invoke();
            }
        }

        /// <summary>Raised when Interact or Use was pressed on this but nothing could happen.</summary>
        public event Action Denied;

        /// <summary>Called once by the composition root with the scene's services.</summary>
        public virtual void Bind(KitchenServices services)
        {
        }

        /// <summary>Where <paramref name="item"/> should sit while this station holds it.</summary>
        public virtual Transform AnchorFor(Item item) => _itemAnchor;

        /// <summary>The item standing on this station, if it shows one (counter top, pan on a hob).</summary>
        public virtual Item HeldItem => (Container as IItemHolder)?.Current;

        internal void AddTargeter(PlayerInteractor player)
        {
            if (_targeters.Contains(player))
                return;

            _targeters.Add(player);
            HighlightChanged?.Invoke();
        }

        internal void RemoveTargeter(PlayerInteractor player)
        {
            if (_targeters.Remove(player))
                HighlightChanged?.Invoke();
        }

        internal void NotifyDenied() => Denied?.Invoke();
    }
}
