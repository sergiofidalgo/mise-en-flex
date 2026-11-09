using Game.Data;
using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using UnityEngine;
using Game.Gameplay.Saving;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A hob. It starts with a pan on it; the pan can be lifted off and carried, and any pan
    /// can be put back on an empty hob. While facing it, the triggers turn the heat and Use
    /// flips the food in the pan. On gas the triggers turn the dial smoothly; on induction each
    /// press is one step up or down (decision 2026-11-07).
    /// </summary>
    public sealed class HobStation : Interactable, IUsable, IHeatControl, IStationPrompt, ISavedContents
    {

        [SerializeField] private HeatKind _kind = HeatKind.Gas;
        [Tooltip("Induction: how far a trigger has to be pulled to count as a press.")]
        [SerializeField, Range(0.1f, 1f)] private float _stepPress = 0.5f;

        private HobSlot _slot;
        private KitchenServices _services;
        private bool _downHeld;
        private bool _upHeld;

        public HeatKind Kind => _kind;

        public Hob Hob => _slot?.Hob;

        /// <summary>Null until bound.</summary>
        public KitchenServices Services => _services;

        public override IItemContainer Container => _slot;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        // Its own pan goes with it, so the pan has to be home and empty.
        public override bool IsClear => Hob?.Pan != null && Hob.Pan.Contents.Count == 0;

        public override void Retire(KitchenServices services)
        {
            var pan = Hob.Pan;
            services.ItemViews.Release(pan);
            services.Cooking.Remove(pan);
            services.Cooking.Remove(Hob);
        }

        public override void Bind(KitchenServices services)
        {
            _services = services;
            var hob = services.Cooking.AddHob(services.Equipment.HobFor(_kind));
            _slot = new HobSlot(hob);

            var pan = services.Cooking.AddPan(services.Equipment.Pan);
            _slot.Put(pan);
            services.ItemViews.PlaceImmediately(pan, ItemAnchor);
        }

        public IEnumerable<(int Slot, Item Item)> SavedItems
        {
            get
            {
                if (Hob?.Pan != null)
                    yield return (0, Hob.Pan);
            }
        }

        // The pan a new hob comes with: the save has its own pans, wherever they were left.
        public void ClearForLoad()
        {
            if (Hob?.Pan == null)
                return;
            var pan = (Simulation.Cooking.Pan)_slot.Take();
            _services.ItemViews.Release(pan);
            _services.Cooking.Remove(pan);
        }

        public bool RestoreItem(int slot, Item item)
        {
            if (!_slot.CanAccept(item))
                return false;
            _slot.Put(item);
            _services.ItemViews.PlaceImmediately(item, ItemAnchor);
            return true;
        }

        public bool TryUse(PlayerInteractor player) => Hob != null && HobControls.TryFlip(Hob);

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            lines.Add(new PromptLine(PromptControl.Triggers, Hob != null && Hob.Kind == HeatKind.Induction ? Tr.Format("heat: {heat}", ("heat", StepName(Hob))) : Tr.Get("heat")));
            var hasFood = Hob?.Pan != null && Hob.Pan.Contents.Count > 0;
            lines.Add(new PromptLine(PromptControl.West, Tr.Get("flip"), hasFood));
            if (Hob?.Pan != null && player.Hands.IsEmpty)
                lines.Add(new PromptLine(PromptControl.South, Tr.Get("take pan")));
            else if (Hob != null && Hob.Pan == null && player.Hands.Current is Pan)
                lines.Add(new PromptLine(PromptControl.South, Tr.Get("put pan")));
        }

        public void AdjustHeat(float heatDown, float heatUp, float realDeltaTime)
        {
            if (Hob == null)
                return;
            if (Hob.Kind != HeatKind.Induction)
            {
                HobControls.AdjustHeat(Hob, heatDown, heatUp, _services.Tuning.HeatDialSpeed, realDeltaTime);
                return;
            }

            // One step per pull: a trigger has to be let go before it counts again.
            var down = heatDown >= _stepPress;
            var up = heatUp >= _stepPress;
            if (down && !_downHeld)
                HobControls.ChangeStep(Hob, -1);
            if (up && !_upHeld)
                HobControls.ChangeStep(Hob, +1);
            _downHeld = down;
            _upHeld = up;
        }

        private static string StepName(Hob hob) => hob.Step switch
        {
            0 => Tr.In("heat", "off"),
            1 => Tr.In("heat", "low"),
            2 => Tr.In("heat", "medium"),
            3 => Tr.In("heat", "high"),
            _ => Tr.Format("step {step}", ("step", hob.Step)),
        };
    }
}
