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

        private readonly List<PlayerInteractor> _targeters = new List<PlayerInteractor>();

        public abstract IItemContainer Container { get; }

        public Transform ItemAnchor => _itemAnchor;

        public bool IsTargeted => _targeters.Count > 0;

        /// <summary>Player index of the most recent player targeting this, or -1.</summary>
        public int TargeterPlayerIndex => _targeters.Count > 0 ? _targeters[_targeters.Count - 1].PlayerIndex : -1;

        public event Action HighlightChanged;

        /// <summary>Raised when Interact was pressed on this but nothing could happen.</summary>
        public event Action Denied;

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
