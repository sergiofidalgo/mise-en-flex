using System;
using Game.Simulation.Cooking;
using Game.Simulation.Items;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A hob. It starts with a pan on it; the pan can be lifted off and carried, and any pan
    /// can be put back on an empty hob. Use steps the heat (placeholder until the close-up
    /// heat dial in milestone 4).
    /// </summary>
    public sealed class HobStation : Interactable, IUsable
    {
        // Temporary heat steps for the Use button: off, low, medium, high.
        private static readonly float[] DialSteps = { 0f, 1f / 3f, 2f / 3f, 1f };

        private HobSlot _slot;
        private int _dialStep;

        public Hob Hob => _slot?.Hob;

        /// <summary>0 = off up to <see cref="DialStepCount"/> - 1 = full heat.</summary>
        public int DialStep => _dialStep;

        public int DialStepCount => DialSteps.Length;

        public override IItemContainer Container => _slot;

        /// <summary>Raised when the dial changes, for visuals.</summary>
        public event Action DialChanged;

        public override void Bind(KitchenServices services)
        {
            var hob = services.Cooking.AddHob(services.Equipment.Hob);
            _slot = new HobSlot(hob);

            var pan = services.Cooking.AddPan(services.Equipment.Pan);
            _slot.Put(pan);
            services.ItemViews.PlaceImmediately(pan, ItemAnchor);
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
    }
}
