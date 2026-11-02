using System;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A hob with its own pan. Interact puts food into or takes it out of the pan; Use steps
    /// the heat. Milestone 1 placeholder controls: the pan becomes carryable in milestone 2 and
    /// the heat dial moves to the triggers in the close-up in milestone 4.
    /// </summary>
    public sealed class HobStation : Interactable, IUsable
    {
        // Temporary heat steps for the Use button: off, low, medium, high.
        private static readonly float[] DialSteps = { 0f, 1f / 3f, 2f / 3f, 1f };

        [Tooltip("Where each ingredient sits in the pan, in pan order.")]
        [SerializeField] private Transform[] _panSlotAnchors;

        private int _dialStep;

        public Hob Hob { get; private set; }

        public Pan Pan { get; private set; }

        public override IItemContainer Container => _container;

        private SearedSideUpPan _container;

        /// <summary>Raised when the dial changes, for visuals.</summary>
        public event Action DialChanged;

        public override void Bind(KitchenServices services)
        {
            Hob = services.Cooking.AddHob(services.Equipment.Hob);
            Pan = services.Cooking.AddPan(services.Equipment.Pan);
            Hob.PlacePan(Pan);
            _container = new SearedSideUpPan(Pan);
        }

        public override Transform AnchorFor(Item item)
        {
            var index = Pan != null && item is Ingredient ingredient ? IndexInPan(ingredient) : -1;
            return index >= 0 && index < _panSlotAnchors.Length ? _panSlotAnchors[index] : ItemAnchor;
        }

        public bool TryUse(PlayerInteractor player)
        {
            if (Hob == null)
                return false;

            _dialStep = (_dialStep + 1) % DialSteps.Length;
            Hob.Dial = DialSteps[_dialStep];
            DialChanged?.Invoke();
            return true;
        }

        // TODO(Phase 1, milestone 4): remove when A flips in the close-up. Until then, putting
        // food back into the pan lands it with its more-browned side up, which is how a cook
        // would set it down, so a steak can be cooked on both sides.
        private sealed class SearedSideUpPan : IItemContainer
        {
            private readonly Pan _pan;

            public SearedSideUpPan(Pan pan) => _pan = pan;

            public bool CanTake => _pan.CanTake;

            public bool CanAccept(Item item) => _pan.CanAccept(item);

            public Item Take() => _pan.Take();

            public void Put(Item item)
            {
                if (item is Ingredient ingredient)
                {
                    var down = ingredient.Browning(ingredient.DownSide);
                    var up = ingredient.Browning(ingredient.DownSide == IngredientSide.A ? IngredientSide.B : IngredientSide.A);
                    if (down > up)
                        ingredient.Flip();
                }
                _pan.Put(item);
            }
        }

        private int IndexInPan(Ingredient ingredient)
        {
            for (var i = 0; i < Pan.Contents.Count; i++)
            {
                if (Pan.Contents[i] == ingredient)
                    return i;
            }
            return -1;
        }
    }
}
