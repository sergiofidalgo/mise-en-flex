namespace Game.Simulation.Items
{
    public enum InteractionOutcome
    {
        /// <summary>Nothing could happen (e.g. full counter, empty hands at an empty counter).</summary>
        Nothing,
        PickedUp,
        Placed,
    }

    /// <summary>
    /// The contextual Interact rule: empty hands pick up, full hands put down.
    /// Swapping and combining items come later (Phase 1 plating).
    /// </summary>
    public static class ItemTransfer
    {
        public static InteractionOutcome Interact(ItemSlot hands, IItemContainer target)
        {
            if (hands.IsEmpty)
            {
                if (!target.CanTake)
                    return InteractionOutcome.Nothing;

                hands.Put(target.Take());
                return InteractionOutcome.PickedUp;
            }

            if (!target.CanAccept(hands.Current))
                return InteractionOutcome.Nothing;

            target.Put(hands.Take());
            return InteractionOutcome.Placed;
        }
    }
}
