namespace Game.Simulation.Items
{
    public enum InteractionOutcome
    {
        /// <summary>Nothing could happen (e.g. full counter, empty hands at an empty counter).</summary>
        Nothing,
        /// <summary>Something went from the station into the player's hands (or into what they hold).</summary>
        PickedUp,
        /// <summary>Something went from the player's hands onto or into the station.</summary>
        Placed,
        /// <summary>Contents moved between two containers (plating, pouring, scraping into the bin).</summary>
        Combined,
    }

    /// <summary>
    /// The contextual Interact rule. In order:
    /// <list type="number">
    /// <item>Empty hands take what's on the station; a pan or plate comes with its food.</item>
    /// <item>Full hands put down if the station accepts what they hold.</item>
    /// <item>Food goes into a pan or onto a plate standing on the station.</item>
    /// <item>A held pan or plate scoops up food standing on the station.</item>
    /// <item>Two containers: contents move between them (plating from a pan, back into a pan).</item>
    /// <item>A held container's contents go into the station if it takes them (scraping into the bin).</item>
    /// </list>
    /// </summary>
    public static class ItemTransfer
    {
        public static InteractionOutcome Interact(ItemSlot hands, IItemContainer target)
        {
            var onStation = (target as IItemHolder)?.Current;

            if (hands.IsEmpty)
            {
                if (!target.CanTake)
                    return InteractionOutcome.Nothing;

                hands.Put(target.Take());
                return InteractionOutcome.PickedUp;
            }

            var held = hands.Current;
            if (target.CanAccept(held))
            {
                target.Put(hands.Take());
                return InteractionOutcome.Placed;
            }

            if (onStation != null)
            {
                if (onStation is IItemCollection standing && standing.CanAccept(held))
                {
                    standing.Put(hands.Take());
                    return InteractionOutcome.Placed;
                }

                if (held is IItemCollection holding)
                {
                    if (holding.CanAccept(onStation))
                    {
                        holding.Put(target.Take());
                        return InteractionOutcome.PickedUp;
                    }

                    if (onStation is IItemCollection other && (Pour(holding, other) > 0 || Pour(other, holding) > 0))
                        return InteractionOutcome.Combined;
                }
            }

            if (held is IItemCollection source && Pour(source, target) > 0)
                return InteractionOutcome.Combined;

            return InteractionOutcome.Nothing;
        }

        // Moves items from the end of `from` into `to` for as long as `to` accepts them.
        private static int Pour(IItemCollection from, IItemContainer to)
        {
            var moved = 0;
            while (from.Items.Count > 0 && to.CanAccept(from.Items[from.Items.Count - 1]))
            {
                to.Put(from.Take());
                moved++;
            }
            return moved;
        }
    }
}
